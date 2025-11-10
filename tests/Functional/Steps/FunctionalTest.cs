using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using YoFi.V3.Tests.Functional.Components;
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

    protected Uri? baseUrl;

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
    /// When user selects option (\S+) in nav bar, or
    /// Given user selected option (\S+) in nav bar
    /// </summary>
    /// <param name="option">Displayed text of navbar item to click</param>
    /// <returns></returns>
    protected async Task SelectOptionInNavbar(string option)
    {
        var pageModel = It<BasePage>();
        await pageModel.NavBar.SelectOptionAsync(option);
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
