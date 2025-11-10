using System.Text.RegularExpressions;
using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Components;

namespace YoFi.V3.Tests.Functional.Pages;

public class BasePage(IPage? _page)
{
    public IPage? Page { get; set; } = _page;

    public ILocator Header => Page!.Locator("h1");

    public NavBar NavBar => new NavBar(Page!);

    public async Task<IResponse> LaunchSite()
    {
        var result = await Page!.GotoAsync("/");

        return result!;
    }
    
    public async Task<string> GetPageHeading()
    {
        return await Header.InnerTextAsync();
    }

    public async Task<string> GetPageTitle()
    {
        return await Page!.TitleAsync();
    }

    public async Task WaitUntilLoaded()
    {
        await Page!.GetByTestId("BaseSpinner").WaitForAsync(new LocatorWaitForOptions() { State = WaitForSelectorState.Hidden });
    }

    public async Task NavigateToUsingSidebar(string link)
    {
        await Page!.Locator("#SideBar").GetByTestId(link).ClickAsync();
    }

    // TODO: Work out duplication with base functional test versions of these!
    public async Task WaitForApi(Func<Task> action, string? endpoint = null)
    {
        var response = await Page!.RunAndWaitForResponseAsync(action, endpoint ?? "/api/**");             
        TestContext.Out.WriteLine("API request {0}", response.Url);
        Assert.That(response!.Ok, Is.True);
    } 

    public async Task WaitForApi(Func<Task> action, Regex regex)
    {
        var response = await Page!.RunAndWaitForResponseAsync(action, regex);             
        TestContext.Out.WriteLine("API request {0}", response.Url);
        Assert.That(response!.Ok, Is.True);
    }

    protected async Task SaveScreenshotAsync(string? moment = null, bool fullPage = true)
    {
        var context = TestContext.Parameters["screenshotContext"] ?? "Local";
        var testclassfull = $"{TestContext.CurrentContext.Test.ClassName}";
        var testclass = testclassfull.Split(".").Last();
        var testname = MakeValidFileName($"{TestContext.CurrentContext.Test.Name}");
        var displaymoment = string.IsNullOrEmpty(moment) ? string.Empty : $"-{moment.Replace('/','-')}";
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

}