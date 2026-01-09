using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
using DotNetEnv;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using YoFi.V3.Tests.Functional.Generated;

namespace YoFi.V3.Tests.Functional.Infrastructure;

/// <summary>
/// Base test class providing infrastructure for all functional tests.
/// </summary>
/// <remarks>
/// Provides:
/// - Test setup and teardown lifecycle
/// - Playwright configuration
/// - Test correlation headers for distributed tracing
/// - Prerequisite checking (browsers installed, backend health)
/// - Object store access for sharing data between steps
/// - Test Control API client access
/// </remarks>
[GeneratedTestBase(UseNamespace = "YoFi.V3.Tests.Functional.Features")]
public abstract partial class FunctionalTestBase : PageTest, ITestContext
{
    #region Fields

    private static bool _prerequisiteCheckFailed = false;
    private static bool _environmentVariablesLoaded = false;

    protected ObjectStore _objectStore = new();
    protected Activity? _testActivity;

    // Track user credentials by friendly name for lookups AND cleanup
    protected readonly Dictionary<string, TestUserCredentials> _userCredentials = new();

    // Track workspaces for cleanup and later reference by steps
    // (keys are FULL workspace names as returned by API)
    protected readonly Dictionary<string, Guid> _workspaceKeys = new();

    protected T It<T>() where T : class => _objectStore.Get<T>();
    protected T The<T>(string key) where T : class => _objectStore.Get<T>(key);

    protected Uri? baseUrl { get; private set; }

    // Experiment with reusable HttpClient for TestControlClient, which we can change
    // headers on whenever we want
    private readonly HttpClient httpClient = new();

    #endregion

    #region Properties
    private TestControlClient? _testControlClient;

    /// <summary>
    /// Gets the Test Control API client for test data setup and cleanup.
    /// </summary>
    protected TestControlClient testControlClient
    {
        get
        {
            if (_testControlClient is null)
            {
                var httpClient = new HttpClient();

                // Add test correlation headers if test activity exists
                if (_testActivity is not null)
                {
                    var headers = BuildTestCorrelationHeaders();
                    foreach (var header in headers)
                    {
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
                else
                {
                    TestContext.Out.WriteLine("[TestControlClient] Warning: Test activity is null, correlation headers will not be set.");
                }

                _testControlClient = new TestControlClient(
                    baseUrl: GetRequiredParameter("apiBaseUrl"),
                    httpClient: httpClient
                );
            }
            return _testControlClient;
        }
    }
    #endregion

    #region Overrides

    public override BrowserNewContextOptions ContextOptions() =>
        new()
        {
            AcceptDownloads = true,
            ViewportSize = new ViewportSize() { Width = 1280, Height = 720 },
            BaseURL = GetRequiredParameter("webAppUrl")
        };
    #endregion

    #region Setup

    [SetUp]
    public async Task SetUp()
    {
        // By convention, I put data-test-id attributes on important elements
        Playwright.Selectors.SetTestIdAttribute("data-test-id");

        // Note that this does need to be done in setup, because we get a new
        // browser context every time. Is there a place we could tell Playwright
        // this just ONCE??
        var defaultTimeoutParam = TestContext.Parameters["defaultTimeout"];
        if (Int32.TryParse(defaultTimeoutParam, out var val))
            Context.SetDefaultTimeout(val);

        // Need a fresh object store for each test
        _objectStore = new ObjectStore();

        // Clear user credentials for each test
        _userCredentials.Clear();

        // Clear workspace keys for each test
        _workspaceKeys.Clear();

        // Add a basepage object to the object store
        _objectStore.Add(new Pages.BasePage(Page));

        //
        // Create test activity for distributed tracing
        //
        var testName = TestContext.CurrentContext.Test.Name;
        var testClass = TestContext.CurrentContext.Test.ClassName ?? "Unknown";
        var testId = Guid.NewGuid().ToString();

        _testActivity = new Activity("FunctionalTest");
        _testActivity.SetTag("test.name", testName);
        _testActivity.SetTag("test.class", testClass);
        _testActivity.SetTag("test.id", testId);
        _testActivity.Start();

        //
        // Set headers for both W3C trace propagation and direct correlation
        //
        var headers = BuildTestCorrelationHeaders();
        await Context.SetExtraHTTPHeadersAsync(headers);

        //
        // Capture console logs from browser
        //
        Page.Console += (sender, msg) =>
        {
            var message = $"{msg.Type}: {msg.Text} at: {msg.Location} url: {msg.Page?.Url ?? "n/a"}";

            if (!ignoredConsoleMessages.Any(x => message.StartsWith(x)))
                TestContext.Out.WriteLine($"[Browser Console] {message}");
        };

        //
        // Wipe out the test control client, ensuring we get a new one every run
        //
        _testControlClient = null;

        //
        // Identify the test with the backend. This helps us quickly locate the traces for test runs
        //
        await testControlClient.IdentifyAsync();
    }

    private static readonly string[] ignoredConsoleMessages = new[]
    {
        "debug: [vite]",
        "warning: Application Insights disabled",
        "warning: Application Insights connection string not configured",
        "info: <Suspense> is an experimental feature",
        "log: ✨ %cNuxt DevTools",
        "error: DropDownClientOnly",
        "log: 🍍 \"userPreferences\" store"
    };

    [TearDown]
    public async Task TearDown()
    {
        // Capture screenshot only on test failure
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            var pageModel = It<Pages.BasePage>();
            await pageModel.SaveScreenshotAsync($"FAILED");
        }

        // Clean up test-specific users and workspaces
        await CleanupTestResourcesAsync();

        _testActivity?.Stop();
        _testActivity?.Dispose();
    }

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        // Ensure environment variables are loaded (will be no-op if already loaded)
        EnsureEnvironmentVariablesLoaded();

        // If prerequisites already failed in another fixture, skip silently
        if (_prerequisiteCheckFailed)
        {
            Assert.Inconclusive("Prerequisites check failed (see error message from first test fixture)");
            return;
        }

        var url = GetRequiredParameter("webAppUrl");
        baseUrl = new(url);

        // Check prerequisites before running any tests - fail with clear message if they're not met
        try
        {
            await CheckPlaywrightBrowsersInstalled();
            await CheckBackendHealthAsync();
        }
        catch (InvalidOperationException ex)
        {
            var message = $"""
                ================================================================================
                PREREQUISITE CHECK FAILED
                ================================================================================

                {ex.Message}

                ================================================================================
                """;

            // Print detailed message to console (only happens once)
            Console.Error.WriteLine(message);
            TestContext.Error.WriteLine(message);

            // Mark tests as inconclusive with brief message
            Assert.Inconclusive("Prerequisites check failed (see error output above)");
        }
    }

    /// <summary>
    /// Loads environment variables from .env file if it exists in the test project root.
    /// </summary>
    /// <remarks>
    /// This allows test configuration via .env files instead of only using .runsettings.
    /// Environment variables from .env can be referenced in .runsettings using {VAR_NAME} syntax.
    /// Silently continues if .env file doesn't exist.
    /// </remarks>
    private static void LoadEnvironmentVariables()
    {
        try
        {
            // Try multiple paths to find .env file
            var searchPaths = new[]
            {
                // Current directory (where tests are executed from)
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                // Test assembly directory
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
                // Go up to find project root (handles bin/Debug/net10.0 structure)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".env")
            };

            foreach (var envFilePath in searchPaths)
            {
                var normalizedPath = Path.GetFullPath(envFilePath);
                if (File.Exists(normalizedPath))
                {
                    Env.Load(normalizedPath);
                    Console.WriteLine($"[Environment] Loaded environment variables from: {normalizedPath}");
                    return;
                }
            }

            // No .env file found - this is OK, not all environments need it
            Console.WriteLine("[Environment] No .env file found (this is optional)");
        }
        catch (Exception ex)
        {
            // Log warning but don't fail tests if .env loading fails
            Console.WriteLine($"[Environment] Warning: Failed to load .env file: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies that Playwright browsers are installed.
    /// </summary>
    /// <remarks>
    /// This check runs once per test fixture to fail fast if browsers aren't installed,
    /// rather than failing every single test with the same error.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when Playwright browsers are not installed.</exception>
    private async Task CheckPlaywrightBrowsersInstalled()
    {
        try
        {
            // Attempt to create a simple browser instance to verify installation
            var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            await browser.CloseAsync();
            playwright.Dispose();
        }
        catch (Microsoft.Playwright.PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
        {
            // Throw exception to stop all tests from running
            var message = $"""
                Playwright browsers are not installed.

                Please run the following command to install browsers:
                    pwsh bin/Debug/net10.0/playwright.ps1 install chromium

                Or build and install in one step:
                    dotnet build tests/Functional
                    pwsh tests/Functional/bin/Debug/net10.0/playwright.ps1 install chromium

                Original error: {ex.Message}
                """;

            throw new InvalidOperationException(message, ex);
        }
    }

    /// <summary>
    /// Verifies that the backend API is responding to health checks.
    /// </summary>
    /// <remarks>
    /// This check runs once per test fixture to fail fast if the backend is down,
    /// rather than failing every single test with connection errors.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the backend is not responding.</exception>
    private async Task CheckBackendHealthAsync()
    {
        try
        {
            var apiBaseUrl = GetRequiredParameter("apiBaseUrl");
            var normalizedBaseUrl = apiBaseUrl.TrimEnd('/');
            var healthUrl = $"{normalizedBaseUrl}/health";

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync(healthUrl);

            if (!response.IsSuccessStatusCode)
            {
                var message = $"""
                    Backend API health check failed with status {(int)response.StatusCode} {response.StatusCode}.

                    Health endpoint: {healthUrl}

                    Please ensure the backend is running:
                    - For local development: Start the backend with 'pwsh scripts/Start-LocalDev.ps1'
                    - For container: Start with 'pwsh scripts/Start-Container.ps1'

                    Response: {await response.Content.ReadAsStringAsync()}
                    """;

                throw new InvalidOperationException(message);
            }
        }
        catch (HttpRequestException ex)
        {
            // Try to get the parameter for error message, fallback to "unknown" if it fails
            string apiBaseUrl;
            try
            {
                apiBaseUrl = GetRequiredParameter("apiBaseUrl");
            }
            catch
            {
                apiBaseUrl = "unknown";
            }

            var message = $"""
                Cannot connect to backend API at {apiBaseUrl}/health

                Please ensure the backend is running:
                - For local development: Start the backend with 'pwsh scripts/Start-LocalDev.ps1'
                - For container: Start with 'pwsh scripts/Start-Container.ps1'

                Original error: {ex.Message}
                """;

            throw new InvalidOperationException(message, ex);
        }
        catch (TaskCanceledException ex)
        {
            // Try to get the parameter for error message, fallback to "unknown" if it fails
            string apiBaseUrl;
            try
            {
                apiBaseUrl = GetRequiredParameter("apiBaseUrl");
            }
            catch
            {
                apiBaseUrl = "unknown";
            }

            var message = $"""
                Backend API health check timed out (5 seconds) at {apiBaseUrl}/health

                The backend may be starting up or experiencing issues.
                Please ensure the backend is running and responsive:
                - For local development: Start the backend with 'pwsh scripts/Start-LocalDev.ps1'
                - For container: Start with 'pwsh scripts/Start-Container.ps1'
                """;

            throw new InvalidOperationException(message, ex);
        }
    }
    #endregion

    #region Helpers

    /// <summary>
    /// Gets a required test parameter and resolves any environment variable references.
    /// </summary>
    /// <param name="parameterName">Name of the test parameter to retrieve.</param>
    /// <returns>The parameter value with any environment variable references resolved.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the parameter is not set or when a referenced environment variable doesn't exist.
    /// </exception>
    /// <remarks>
    /// Parameters can contain environment variable references using the syntax: {ENV_VAR_NAME}
    /// For example: "https://localhost:5001" or "{WEB_APP_URL}"
    /// </remarks>
    protected static string GetRequiredParameter(string parameterName)
    {
        // Ensure environment variables are loaded before resolving parameters
        EnsureEnvironmentVariablesLoaded();

        var rawValue = TestContext.Parameters[parameterName];

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException(
                $"Required test parameter '{parameterName}' is not set in .runsettings file."
            );
        }

        return ResolveEnvironmentVariables(rawValue, parameterName);
    }

    /// <summary>
    /// Ensures environment variables are loaded from .env file before they're needed.
    /// Uses a static flag to ensure loading happens only once across all test instances.
    /// </summary>
    private static void EnsureEnvironmentVariablesLoaded()
    {
        if (_environmentVariablesLoaded)
            return;

        lock (typeof(FunctionalTestBase))
        {
            if (_environmentVariablesLoaded)
                return;

            LoadEnvironmentVariables();
            _environmentVariablesLoaded = true;
        }
    }

    /// <summary>
    /// Resolves environment variable references in curly braces (e.g., {ENV_VAR}).
    /// </summary>
    /// <param name="value">String that may contain {ENV_VAR} references.</param>
    /// <param name="contextName">Name of the parameter/setting being resolved (for error messages).</param>
    /// <returns>String with all environment variable references resolved.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a referenced environment variable doesn't exist.
    /// </exception>
    private static string ResolveEnvironmentVariables(string value, string contextName)
    {
        var result = value;

        // Find all {ENV_VAR} patterns
        foreach (Match match in EnvVarRegex().Matches(value))
        {
            var envVarName = match.Groups[1].Value;
            var envVarValue = Environment.GetEnvironmentVariable(envVarName);

            if (envVarValue is null)
            {
                throw new InvalidOperationException(
                    $"Environment variable '{envVarName}' referenced in test parameter '{contextName}' is not set. " +
                    $"Original value: {value}"
                );
            }

            result = result.Replace(match.Value, envVarValue);
        }

        return result;
    }

    [GeneratedRegex(@"\{(.*?)\}")]
    private static partial Regex EnvVarRegex();

    /// <summary>
    /// Saves a screenshot for debugging purposes.
    /// </summary>
    protected async Task SaveScreenshotAsync()
    {
        var pageModel = It<Pages.BasePage>();
        await pageModel.SaveScreenshotAsync();
    }

    /// <summary>
    /// Builds test correlation headers for distributed tracing.
    /// </summary>
    /// <returns>Dictionary of HTTP headers including W3C Trace Context and custom test correlation headers.</returns>
    private Dictionary<string, string> BuildTestCorrelationHeaders()
    {
        if (_testActivity is null)
        {
            TestContext.Out.WriteLine("[TestControlClient] Warning: Test activity is null, correlation headers will not be set.");
            return new Dictionary<string, string>();
        }

        var testName = TestContext.CurrentContext.Test.Name;
        var testClass = TestContext.CurrentContext.Test.ClassName ?? "Unknown";
        var testId = _testActivity.GetTagItem("test.id")?.ToString() ?? Guid.NewGuid().ToString();
        var traceParent = $"00-{_testActivity.TraceId}-{_testActivity.SpanId}-01";

        // Enable this for additional correlation debugging
#if false
        TestContext.Out.WriteLine($"[TestControlClient] Building test correlation headers: TestName={testName}, TestId={testId}, TestClass={testClass}, TraceParent={traceParent}");
#endif

        return new Dictionary<string, string>
        {
            // W3C Trace Context standard
            ["traceparent"] = traceParent,

            // Direct test correlation (fallback and convenience)
            ["X-Test-Name"] = HttpUtility.UrlEncode(testName),
            ["X-Test-Id"] = testId,
            ["X-Test-Class"] = testClass
        };
    }

    /// <summary>
    /// Generates unique test user credentials based on test context.
    /// </summary>
    /// <param name="friendlyName">Friendly name for the user (e.g., "alice", "bob").</param>
    /// <returns>Test user credentials with unique username, email, and password.</returns>
    /// <remarks>
    /// Credentials are automatically tracked for cleanup in TearDown.
    /// Username format: __TEST__{friendlyName}_{testId:X8}
    /// </remarks>
    public TestUserCredentials CreateTestUserCredentials(string friendlyName)
    {
        var testId = TestContext.CurrentContext.Test.ID.GetHashCode();
        var username = $"__TEST__{friendlyName}_{testId:X8}";
        var password = $"Test_{testId:X8}!";

        var creds = new TestUserCredentials
        {
            ShortName = friendlyName,
            Username = username,
            Email = $"{username}@test.local",
            Password = password
        };

        // Store immediately for lookup and cleanup
        _userCredentials[friendlyName] = creds;

        return creds;
    }

    public async Task<TestUserCredentials> CreateTestUserCredentialsOnServer(string friendlyName)
    {
        var userCreds = CreateTestUserCredentials(friendlyName);  // Auto-tracked
        try
        {
            var created = await testControlClient.CreateUsersV2Async(new[] { userCreds });

            // Update with server-populated ID
            var createdUser = created.Single();
            _userCredentials[createdUser.ShortName] = createdUser;
            return createdUser;
        }
        catch (ApiException<ProblemDetails> ex)
        {
            // Re-throw with formatted ProblemDetails for better troubleshooting
            throw new InvalidOperationException(FormatApiException(ex), ex);
        }
    }

    /// <summary>
    /// Formats an ApiException with ProblemDetails for detailed error reporting.
    /// </summary>
    /// <param name="ex">The API exception to format.</param>
    /// <returns>A formatted error message including all ProblemDetails information.</returns>
    private static string FormatApiException(ApiException<ProblemDetails> ex)
    {
        var pd = ex.Result;
        var message = $"""
            TestControlClient API call failed:

            Status: {ex.StatusCode} ({pd?.Status})
            Title: {pd?.Title ?? "N/A"}
            Detail: {pd?.Detail ?? "N/A"}
            Type: {pd?.Type ?? "N/A"}
            Instance: {pd?.Instance ?? "N/A"}

            Raw Response (first 512 chars):
            {ex.Response?.Substring(0, Math.Min(512, ex.Response?.Length ?? 0)) ?? "(empty)"}
            """;

        return message;
    }

    /// <summary>
    /// Tracks a created workspace for cleanup in TearDown.
    /// </summary>
    /// <param name="workspaceName">The full workspace name (including __TEST__ prefix).</param>
    /// <param name="workspaceKey">The unique identifier of the workspace.</param>
    public void TrackCreatedWorkspace(string workspaceName, Guid workspaceKey)
    {
        _workspaceKeys[workspaceName] = workspaceKey;
    }

    /// <summary>
    /// Removes a workspace from cleanup tracking.
    /// </summary>
    /// <param name="workspaceName">The full workspace name (including __TEST__ prefix).</param>
    /// <remarks>
    /// Use this when a test explicitly deletes a workspace to prevent "already deleted"
    /// errors during TearDown cleanup. If the workspace was not being tracked, this
    /// method does nothing (idempotent operation).
    /// </remarks>
    public void UntrackWorkspace(string workspaceName)
    {
        _workspaceKeys.Remove(workspaceName);
    }

    /// <summary>
    /// Cleans up test resources (users and workspaces) created during this test execution.
    /// </summary>
    private async Task CleanupTestResourcesAsync()
    {
        try
        {
            // Clean up workspaces first (cascade will delete transactions)
            if (_workspaceKeys.Count > 0)
            {
                try
                {
                    await testControlClient.DeleteWorkspacesAsync(_workspaceKeys.Values);
                }
                catch (ApiException<ProblemDetails> ex)
                {
                    // Log but don't fail test if cleanup fails
                    TestContext.Out.WriteLine($"[Cleanup] Failed to delete workspaces: {ex.Message} | {System.Text.Json.JsonSerializer.Serialize(ex.Result)}");
                }
                catch (Exception ex)
                {
                    // Log but don't fail test if cleanup fails
                    TestContext.Out.WriteLine($"[Cleanup] Failed to delete workspaces: {ex.Message}");
                }
            }

            // Clean up users in bulk using V2 endpoint
            if (_userCredentials.Count > 0)
            {
                try
                {
                    var usernames = _userCredentials.Values.Select(u => u.Username).ToList();
                    await testControlClient.DeleteUsersV2Async(usernames);
                }
                catch (Exception ex)
                {
                    // Log but don't fail test if cleanup fails
                    TestContext.Out.WriteLine($"[Cleanup] Failed to delete users: {ex.Message}");
                }
            }

            if (_objectStore.Contains<string>(ObjectStoreKeys.OfxFilePath))
            {
                var ofxFilePath = _objectStore.Get<string>(ObjectStoreKeys.OfxFilePath);
                try
                {
                    if (!string.IsNullOrWhiteSpace(ofxFilePath) && File.Exists(ofxFilePath))
                    {
                        File.Delete(ofxFilePath);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail test if cleanup fails
                    TestContext.Out.WriteLine($"[Cleanup] Failed to delete OFX file '{ofxFilePath}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Swallow exceptions during cleanup to avoid masking test failures
            TestContext.Error.WriteLine($"[Cleanup] Unexpected error during cleanup: {ex}");
        }
    }

    #endregion

    #region ITestContext Implementation

    /// <inheritdoc />
    public ObjectStore ObjectStore => _objectStore;

    /// <inheritdoc />
    public TestControlClient TestControlClient => testControlClient;

    /// <inheritdoc />
    IPage ITestContext.Page => Page;

    /// <inheritdoc />
    public TestUserCredentials GetUserCredentials(string friendlyName)
    {
        if (!_userCredentials.TryGetValue(friendlyName, out var credentials))
        {
            throw new KeyNotFoundException($"User credentials for '{friendlyName}' not found. Did you forget to create the user?");
        }
        return credentials;
    }

    /// <inheritdoc />
    public TestUserCredentials GetOtherUserCredentials(string excludeFriendlyName)
    {
        // Find first user that isn't the excluded user
        var otherUser = _userCredentials
            .Where(kvp => kvp.Key != excludeFriendlyName)
            .Select(kvp => kvp.Value)
            .FirstOrDefault();

        if (otherUser == null)
        {
            throw new InvalidOperationException(
                $"No other test users available besides '{excludeFriendlyName}'. " +
                "Ensure at least two users are created in Background (e.g., via GivenTheseUsersExist).");
        }

        return otherUser;
    }

    /// <inheritdoc />
    public Guid GetWorkspaceKey(string workspaceName)
    {
        if (!_workspaceKeys.TryGetValue(workspaceName, out var key))
        {
            throw new KeyNotFoundException($"Workspace key for '{workspaceName}' not found. Did you forget to track the workspace?");
        }
        return key;
    }

    /// <inheritdoc />
    public T GetOrCreatePage<T>() where T : class
    {
        if (_objectStore.Contains<T>())
        {
            return _objectStore.Get<T>();
        }

        // Create new page using constructor that takes IPage
        var page = (T)Activator.CreateInstance(typeof(T), Page)!;
        _objectStore.Add(page);
        return page;
    }

    // CreateTestUserCredentials - already implemented as protected method (lines 550-568)
    // CreateTestUserCredentialsOnServer - already implemented as protected method (lines 570-587)
    // TrackCreatedWorkspace - already implemented as protected method (lines 617-620)

    #endregion
}
