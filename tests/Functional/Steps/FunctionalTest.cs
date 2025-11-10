using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using YoFi.V3.Tests.Functional.Components;

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
            BaseURL = checkEnvironment(TestContext.Parameters["webAppUrl"]!)
        };
    #endregion

    #region Setup

    [SetUp]
    public async Task SetUp()
    {
        Playwright.Selectors.SetTestIdAttribute("data-test-id");

        // Note that this does need to be done in setup, because we get a new
        // browser context every time. Is there a place we could tell Playwright
        // this just ONCE??
        if (Int32.TryParse(TestContext.Parameters["defaultTimeout"],out var val))
            Context.SetDefaultTimeout(val);

        // Need a fresh object store for each test
        _objectStore = new ObjectStore();
        
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
        var url = checkEnvironment(TestContext.Parameters["webAppUrl"]!);
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
        // TODO: Refactor to use Page Object Models
        var result = await Page!.GotoAsync("/");
        _objectStore.Add<IResponse>(result!);
    }

    /// <summary>
    /// When user selects option (\S+) in nav bar, or
    /// Given user selected option (\S+) in nav bar
    /// </summary>
    /// <param name="option">Displayed text of navbar item to click</param>
    /// <returns></returns>
    protected async Task SelectOptionInNavbar(string option)
    {
        var navBar = new NavBar(Page);

        await navBar.SelectOptionAsync(option);

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Then page loaded ok
    /// </summary>
    protected Task ThenPageLoadedOk()
    {
        var response = _objectStore.Get<IResponse>();

        Assert.That(response!.Ok, Is.True);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Then page title contains (\S+)
    /// </summary>
    /// <param name="text">Text expected in page title</param>
    protected async Task PageTitleContains(string text)
    {
        var pageTitle = await Page.TitleAsync();
        Assert.That(pageTitle, Does.Contain(text));
    }

    /// <summary>
    /// Then page heading is (\S+)
    /// </summary>
    /// <param name="text">Text expected as the H1</param>
    protected async Task PageHeadingIs(string text)
    {
        var heading1 = await Page.Locator("h1").InnerTextAsync();
        Assert.That(heading1, Is.EqualTo(text));
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

    protected async Task SaveScreenshotAsync(string? moment = null, bool fullPage = true)
    {
        var context = TestContext.Parameters["screenshotContext"] ?? "Local";
        var testclassfull = $"{TestContext.CurrentContext.Test.ClassName}";
        var testclass = testclassfull.Split(".").Last();
        var testname = MakeValidFileName($"{TestContext.CurrentContext.Test.Name}");
        var displaymoment = string.IsNullOrEmpty(moment) ? string.Empty : $"-{moment.Replace('/', '-')}";
        var filename = $"Screenshot/{context}/{testclass}/{testname}{displaymoment}.png";
        await Page!.ScreenshotAsync(new PageScreenshotOptions() { Path = filename, OmitBackground = true, FullPage = fullPage });
        TestContext.AddTestAttachment(filename);
    }

    // https://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
    private static string MakeValidFileName( string name )
    {
        var invalidChars = System.Text.RegularExpressions.Regex.Escape( new string( System.IO.Path.GetInvalidFileNameChars() ) );
        var invalidRegStr = string.Format( @"([{0}]*\.+$)|([{0}]+)", invalidChars );

        return System.Text.RegularExpressions.Regex.Replace( name, invalidRegStr, "_" );
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
}
