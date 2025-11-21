using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using YoFi.V3.Tests.Functional.Components;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Base test class shared by all functional test classes
/// </summary>
/// <remarks>
/// This is where the steps will live
/// </remarks>
public abstract class FunctionalTest : PageTest
{
    #region Fields

    protected ObjectStore _objectStore = new();

    protected T It<T>() where T : class => _objectStore.Get<T>();
    protected T The<T>(string key) where T : class => _objectStore.Get<T>(key);

    protected Uri? baseUrl { get; private set; }

    #endregion

    #region Properties
    private TestControlClient? _testControlClient;
    protected TestControlClient testControlClient
    {
        get
        {
            if (_testControlClient is null)
            {
                _testControlClient = new TestControlClient(
                    baseUrl:
                        TestContext.Parameters["apiBaseUrl"]
                        ?? throw new NullReferenceException("apiBaseUrl test parameter not set"),
                    httpClient: new HttpClient()
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
            BaseURL = checkEnvironment(
                TestContext.Parameters["webAppUrl"]
                ?? throw new ArgumentNullException("webAppUrl test parameter not set")
            )
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
        _objectStore.Add(new BasePage(Page));

        // Add x-test-name cookie, which will insert test name into logs for easy
        // correlation (in the future)
        await this.Context.AddCookiesAsync(
        [
            new Cookie()
            {
                Name = "x-test-name",
                Value = HttpUtility.UrlEncode(TestContext.CurrentContext.Test.Name),
                Domain = baseUrl!.Host,
                Path = "/"
            }
        ]);
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var url = checkEnvironment(
                TestContext.Parameters["webAppUrl"]
                ?? throw new ArgumentNullException("webAppUrl test parameter not set")
            );
        baseUrl = new(url);
    }
    #endregion

    #region Steps: GIVEN

    /// <summary>
    /// Given has user launched site
    /// </summary>
    protected async Task GivenLaunchedSite()
    {
        await WhenUserLaunchesSite();
        await ThenPageLoadedOk();
    }

    /// <summary>
    /// Given: the application is running
    /// </summary>
    protected async Task GivenTheApplicationIsRunning()
    {
        await GivenLaunchedSite();
    }

    /// <summary>
    /// Given: I am not logged in
    /// </summary>
    protected async Task GivenIAmNotLoggedIn()
    {
        // TODO: Implement logout if already logged in
        // For now, assume we start from a clean state
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given: I have an existing account
    /// </summary>
    protected async Task GivenIHaveAnExistingAccount()
    {
        await testControlClient.DeleteUsersAsync();
        var user = await testControlClient.CreateUserAsync();
        _objectStore.Add(user);
    }

    /// <summary>
    /// Given: I am on the login page
    /// </summary>
    protected virtual async Task GivenIAmOnTheLoginPage()
    {
        await Page.GotoAsync("/login");
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be on login page");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Given: I am logged in
    /// </summary>
    protected async Task GivenIAmLoggedIn()
    {
        await GivenIHaveAnExistingAccount();
        await GivenIAmOnTheLoginPage();
        await WhenIEnterMyCredentials();
        await WhenIClickTheLoginButton();
        await ThenIShouldSeeTheHomePage();
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// When: User launches site
    /// </summary>
    protected async Task WhenUserLaunchesSite()
    {
        var pageModel = It<BasePage>();
        var result = await pageModel.LaunchSite();
        _objectStore.Add(pageModel);
        _objectStore.Add(result);
    }

    /// <summary>
    /// When user visits the (\S+) page, or
    /// Given user visited the (\S+) page, or
    /// </summary>
    /// <param name="option">Displayed text of navbar item to click</param>
    /// <returns></returns>
    protected async Task VisitPage(string option)
    {
        var pageModel = It<BasePage>();
        await pageModel.SiteHeader.Nav.SelectOptionAsync(option);
    }

    /// <summary>
    /// When: I enter my credentials
    /// </summary>
    protected async Task WhenIEnterMyCredentials()
    {
        var loginPage = GetOrCreateLoginPage();

        var testuser = It<Generated.TestUser>();

        await loginPage.EnterCredentialsAsync(testuser.Username, testuser.Password);
    }

    /// <summary>
    /// When: I click the login button
    /// </summary>
    protected async Task WhenIClickTheLoginButton()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.ClickLoginButtonAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Then page loaded ok
    /// </summary>
    protected Task ThenPageLoadedOk()
    {
        var response = It<IResponse>();

        Assert.That(response!.Ok, Is.True);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Then page title contains (\S+)
    /// </summary>
    /// <param name="text">Text expected in page title</param>
    protected async Task PageTitleContains(string text)
    {
        var pageModel = It<BasePage>();
        var pageTitle = await pageModel.GetPageTitle();
        Assert.That(pageTitle, Does.Contain(text));
    }

    /// <summary>
    /// Then page heading is (\S+)
    /// </summary>
    /// <param name="text">Text expected as the H1</param>
    protected async Task PageHeadingIs(string text)
    {
        var pageModel = It<BasePage>();
        var heading1 = await pageModel.GetPageHeading();
        Assert.That(heading1, Is.EqualTo(text));
    }

    /// <summary>
    /// Then page contains (\S+) forecasts
    /// </summary>
    /// <param name="expectedCount"></param>
    /// <returns></returns>
    protected async Task WeatherPageDisplaysForecasts(int expectedCount)
    {
        var weatherPage = new WeatherPage(Page);
        _objectStore.Add(weatherPage);
        var actualCount = await weatherPage.ForecastRows.CountAsync();
        Assert.That(actualCount, Is.EqualTo(expectedCount));
    }

    /// <summary>
    /// Then: I should see the home page
    /// </summary>
    protected virtual async Task ThenIShouldSeeTheHomePage()
    {
        await Task.Delay(1000);
        Assert.That(Page.Url.EndsWith('/'), Is.True, "Should be on home page");
    }

    #endregion

    #region Helpers

    protected string checkEnvironment(string old)
    {
        var result = old;
        var match = findEnvRegex.Match(old);

        // If password is in curly braces, then pull password from the
        // matching environment var
        if (match.Success)
        {
            var env = match.Groups[1].Value;
            var envVar = Environment.GetEnvironmentVariable(env)!;
            result = replaceEnvRegex.Replace(old, envVar);
        }

        return result;
    }
    private static readonly Regex findEnvRegex = new("{(.*?)}");
    private static readonly Regex replaceEnvRegex = new("({.*?})");

    protected async Task SaveScreenshotAsync()
    {
        var pageModel = It<BasePage>();
        await pageModel.SaveScreenshotAsync();
    }

    /// <summary>
    /// Get or create LoginPage and store it in the object store
    /// </summary>
    protected LoginPage GetOrCreateLoginPage()
    {
        if (!_objectStore.Contains<LoginPage>())
        {
            var loginPage = new LoginPage(Page);
            _objectStore.Add(loginPage);
        }
        return It<LoginPage>();
    }

    /// <summary>
    /// Get or create WeatherPage and store it in the object store
    /// </summary>
    protected WeatherPage GetOrCreateWeatherPage()
    {
        if (!_objectStore.Contains<WeatherPage>())
        {
            var weatherPage = new WeatherPage(Page);
            _objectStore.Add(weatherPage);
        }
        return It<WeatherPage>();
    }

    #endregion
}

/// <summary>
/// Holds a store of objects to be shared between tests
/// </summary>
/// <remarks>
/// This is to help make the feature tests be generatable, without having to
/// worry about local vars. All objects generated or needed by the tests are
/// contained here.
/// </remarks>
public class ObjectStore
{
    private readonly Dictionary<string, object> _objects = new();

    public void Add<T>(string key, T obj) where T : class
    {
        _objects[key] = obj;
    }
    public void Add<T>(T obj) where T : class
    {
        _objects[typeof(T).Name] = obj;
    }

    public T Get<T>(string key) where T : class
    {
        return (T)_objects[key];
    }
    public T Get<T>() where T : class
    {
        return (T)_objects[typeof(T).Name];
    }

    public bool Contains<T>() where T : class
    {
        return _objects.ContainsKey(typeof(T).Name);
    }
    public bool Contains<T>(string key) where T : class
    {
        return _objects.ContainsKey(key);
    }
}
