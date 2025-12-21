using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework.Internal;
using YoFi.V3.Tests.Functional.Components;

namespace YoFi.V3.Tests.Functional.Pages;

public class BasePage(IPage? _page)
{
    private static readonly string _apiBaseUrl = TestContext.Parameters["apiBaseUrl"] ?? throw new NotImplementedException("API Base URL not configured in test parameters.");

    public IPage? Page { get; set; } = _page;

    #region Components

    /// <summary>
    /// Site header component with navigation and login state
    /// </summary>
    public SiteHeader SiteHeader => new SiteHeader(Page!, Page!.Locator("body"));

    #endregion

    #region Common Page Elements

    /// <summary>
    /// Main page heading (h1)
    /// </summary>
    public ILocator Header => Page!.Locator("h1");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates to the site root
    /// </summary>
    public async Task<IResponse> LaunchSite()
    {
        var result = await Page!.GotoAsync("/");

        return result!;
    }

    /// <summary>
    /// Navigates using sidebar links
    /// </summary>
    /// <param name="link">The test ID of the sidebar link</param>
    public async Task NavigateToUsingSidebar(string link)
    {
        await Page!.Locator("#SideBar").GetByTestId(link).ClickAsync();
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the main page heading text
    /// </summary>
    public async Task<string> GetPageHeading()
    {
        return await Header.InnerTextAsync();
    }

    /// <summary>
    /// Gets the page title from the browser
    /// </summary>
    public async Task<string> GetPageTitle()
    {
        return await Page!.TitleAsync();
    }

    /// <summary>
    /// Checks if a control is available for interaction (both visible and enabled).
    /// </summary>
    /// <param name="locator">The locator for the control to check</param>
    /// <returns>True if the control is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// This method abstracts the implementation detail of whether a control is unavailable
    /// due to being hidden or disabled. It provides a unified way to check if a control
    /// can be interacted with, which is particularly useful for permission-based scenarios
    /// where controls may be hidden or disabled based on user roles.
    /// </remarks>
    public async Task<bool> IsAvailableAsync(ILocator locator)
    {
        var isVisible = await locator.IsVisibleAsync();
        if (!isVisible) return false;
        return await locator.IsEnabledAsync();
    }

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Waits for the loading spinner to disappear
    /// </summary>
    public async Task WaitUntilLoaded()
    {
        await Page!.GetByTestId("BaseSpinner").WaitForAsync(new LocatorWaitForOptions() { State = WaitForSelectorState.Hidden });
    }

    // TODO: Work out duplication with base functional test versions of these!
    public async Task WaitForApi(Func<Task> action, string? endpoint = null)
    {
        throw new NotImplementedException("Prefer regex version of WaitForApi");
    }

    /// <summary>
    /// Executes an action and waits for a matching API response
    /// </summary>
    /// <param name="action">Action that triggers the API call</param>
    /// <param name="regex">Regex pattern to match the API endpoint</param>
    public async Task WaitForApi(Func<Task> action, Regex regex)
    {
        var response = await Page!.RunAndWaitForResponseAsync(action, regex);
        TestContext.Out.WriteLine("API request {0}", response.Url);

        // We also test failure cases, so we don't assert here
        // TODO: Consider returning the response for further checking
        // Assert.That(response!.Ok, Is.True);
    }

    #endregion

    #region Screenshot Helpers

    /// <summary>
    /// Captures a screenshot of the current page
    /// </summary>
    /// <param name="moment">Optional moment identifier for the screenshot filename</param>
    /// <param name="fullPage">Whether to capture the full page or just the viewport</param>
    public async Task SaveScreenshotAsync(string? moment = null, bool fullPage = true)
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

    #endregion

    #region Helpers

    // https://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
    private static string MakeValidFileName( string name )
    {
        var invalidChars = System.Text.RegularExpressions.Regex.Escape( new string( System.IO.Path.GetInvalidFileNameChars() ) );
        var invalidRegStr = string.Format( @"([{0}]*\.+$)|([{0}]+)", invalidChars );

        return System.Text.RegularExpressions.Regex.Replace( name, invalidRegStr, "_" );
    }

    #endregion

}
