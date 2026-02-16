using jcoliz.FunctionalTests;
using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Components;

namespace YoFi.V3.Tests.Functional.Pages;

public class BasePage(IPage _page): PageObjectModel(_page)
{
    public IPage Page { get; } = _page;

    #region Components

    /// <summary>
    /// Site header component with navigation and login state
    /// </summary>
    public SiteHeader SiteHeader => new SiteHeader(Page, Page.Locator("body"));

    #endregion

    #region Common Page Elements

    /// <summary>
    /// Main page heading (h1)
    /// </summary>
    public ILocator Header => Page.Locator("h1");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates using sidebar links
    /// </summary>
    /// <param name="link">The test ID of the sidebar link</param>
    public async Task NavigateToUsingSidebar(string link)
    {
        await Page.Locator("#SideBar").GetByTestId(link).ClickAsync();
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

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Waits for the loading spinner to disappear
    /// </summary>
    public async Task WaitUntilLoaded()
    {
        await Page.GetByTestId("BaseSpinner").WaitForAsync(new LocatorWaitForOptions() { State = WaitForSelectorState.Hidden });
    }

    #endregion

}
