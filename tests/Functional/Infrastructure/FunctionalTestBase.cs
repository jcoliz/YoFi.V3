using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
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
public abstract class FunctionalTestBase : PageTest
{
    #region Fields

    private static bool _prerequisiteCheckFailed = false;

    protected ObjectStore _objectStore = new();
    protected Activity? _testActivity;

    protected T It<T>() where T : class => _objectStore.Get<T>();
    protected T The<T>(string key) where T : class => _objectStore.Get<T>(key);

    protected Uri? baseUrl { get; private set; }

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
        await Context.SetExtraHTTPHeadersAsync(BuildTestCorrelationHeaders());

        //
        // Capture console logs from browser
        //
        Page.Console += (sender, msg) =>
        {
            var message = $"{msg.Type}: {msg.Text} at: {msg.Location} url: {msg.Page?.Url ?? "n/a"}";

            if (! ignoredConsoleMessages.Any(x=> message.StartsWith(x)))
                TestContext.Out.WriteLine($"[Browser Console] {message}");
        };
    }

    private static readonly string[] ignoredConsoleMessages = new[]
    {
        "debug: [vite]",
        "warning: Application Insights disabled",
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

        _testActivity?.Stop();
        _testActivity?.Dispose();
    }

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
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
    protected string GetRequiredParameter(string parameterName)
    {
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
    /// Resolves environment variable references in curly braces (e.g., {ENV_VAR}).
    /// </summary>
    /// <param name="value">String that may contain {ENV_VAR} references.</param>
    /// <param name="contextName">Name of the parameter/setting being resolved (for error messages).</param>
    /// <returns>String with all environment variable references resolved.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a referenced environment variable doesn't exist.
    /// </exception>
    private string ResolveEnvironmentVariables(string value, string contextName)
    {
        var result = value;

        // Find all {ENV_VAR} patterns
        foreach (Match match in findEnvRegex.Matches(value))
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

    private static readonly Regex findEnvRegex = new("{(.*?)}");

    /// <summary>
    /// Checks for environment variable references in curly braces and replaces them.
    /// </summary>
    /// <param name="old">String that may contain {ENV_VAR} references.</param>
    /// <returns>String with environment variables resolved.</returns>
    [Obsolete("Use GetRequiredParameter() instead. This method will be removed in a future version.")]
    protected string checkEnvironment(string old)
    {
        return ResolveEnvironmentVariables(old, "unknown");
    }

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
            return new Dictionary<string, string>();
        }

        var testName = TestContext.CurrentContext.Test.Name;
        var testClass = TestContext.CurrentContext.Test.ClassName ?? "Unknown";
        var testId = _testActivity.GetTagItem("test.id")?.ToString() ?? Guid.NewGuid().ToString();
        var traceParent = $"00-{_testActivity.TraceId}-{_testActivity.SpanId}-01";

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

    #endregion
}
