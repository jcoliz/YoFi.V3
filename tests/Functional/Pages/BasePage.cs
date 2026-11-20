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

    /// <summary>
    /// App-level toast notifications available on every page.
    /// </summary>
    public ILocator AppToast => Page.GetByTestId("app-toast");

    /// <summary>
    /// The first category picker component currently mounted by the application.
    /// </summary>
    /// <remarks>
    /// Not ALL pages support the category picker, but enough do that we are generalizing its access here.
    /// </remarks>
    public ILocator CategoryPicker => Page.GetByTestId("category-picker").First;

    /// <summary>
    /// The open category suggestion listbox.
    /// </summary>
    public ILocator CategorySuggestionList => CategoryPicker.GetByTestId("suggestions");

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

    /// <summary>
    /// Waits for the expected heading after client-side navigation.
    /// </summary>
    public Task ExpectPageHeadingAsync(string text) =>
        Assertions.Expect(Header).ToHaveTextAsync(text);

    /// <summary>
    /// Finds and fills the currently visible category picker input.
    /// </summary>
    /// <param name="value">The category text or prefix to enter.</param>
    public async Task EnterCategoryPickerAsync(string value)
    {
        await Assertions.Expect(CategoryPicker).ToBeVisibleAsync();
        await CategoryPicker.Locator("input").FillAsync(value);
    }

    /// <summary>
    /// Waits for an exact category suggestion to become visible in the open listbox.
    /// </summary>
    /// <param name="category">The exact category text expected from the suggestion lookup.</param>
    public async Task ExpectCategorySuggestionAsync(string category)
    {
        await Assertions.Expect(CategorySuggestionList).ToBeVisibleAsync();
        await Assertions.Expect(CategorySuggestionList.GetByRole(
            AriaRole.Option,
            new() { Name = category, Exact = true })).ToBeVisibleAsync();
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

    /// <summary>
    /// Waits for the specified button to become visible and enabled within the given timeout.
    /// </summary>
    /// <param name="button">The button locator to wait for</param>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    public async Task WaitForButtonToBeEnabledAsync(ILocator button, float timeout = 5000)
    {
        await Assertions.Expect(button)
            .ToBeVisibleAsync(new() { Timeout = timeout });

        await Assertions.Expect(button)
            .ToBeEnabledAsync(new() { Timeout = timeout });
    }

    #endregion

}
