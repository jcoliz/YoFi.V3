using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Common;

/// <summary>
/// Common Then step definitions shared across all feature tests.
/// </summary>
public abstract class CommonThenSteps : CommonWhenSteps
{
    #region Steps: THEN

    /// <summary>
    /// Verifies that the current page loaded successfully with HTTP 200 OK.
    /// </summary>
    /// <remarks>
    /// Retrieves the page response from the object store and asserts that
    /// the Ok property is true, indicating a successful page load.
    /// </remarks>
    [Then("page loaded ok")]
    protected override Task ThenPageLoadedOk()
    {
        var response = It<IResponse>();

        Assert.That(response!.Ok, Is.True);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the page title contains the specified text.
    /// </summary>
    /// <param name="text">The text expected to appear in the page title.</param>
    /// <remarks>
    /// Retrieves the current page title from the BasePage model and performs
    /// a partial match assertion using Does.Contain.
    /// </remarks>
    [Then("page title contains {text}")]
    protected async Task PageTitleContains(string text)
    {
        var pageModel = It<BasePage>();
        var pageTitle = await pageModel.GetPageTitle();
        Assert.That(pageTitle, Does.Contain(text));
    }

    /// <summary>
    /// Verifies that the page's main heading (H1) exactly matches the specified text.
    /// </summary>
    /// <param name="text">The exact text expected as the H1 heading.</param>
    /// <remarks>
    /// Retrieves the H1 heading from the BasePage model and performs
    /// an exact match assertion.
    /// </remarks>
    [Then("page heading is {text}")]
    protected async Task PageHeadingIs(string text)
    {
        var pageModel = It<BasePage>();
        var heading1 = await pageModel.GetPageHeading();
        Assert.That(heading1, Is.EqualTo(text));
    }

    /// <summary>
    /// Verifies that the weather page displays the expected number of forecast rows.
    /// </summary>
    /// <param name="expectedCount">The number of forecast rows expected to be visible.</param>
    /// <remarks>
    /// Creates a WeatherPage instance, counts the visible forecast rows,
    /// and asserts the count matches the expected value.
    /// </remarks>
    [Then("page contains {expectedCount} forecasts")]
    protected async Task WeatherPageDisplaysForecasts(int expectedCount)
    {
        var weatherPage = new WeatherPage(Page);
        _objectStore.Add(weatherPage);
        var actualCount = await weatherPage.ForecastRows.CountAsync();
        Assert.That(actualCount, Is.EqualTo(expectedCount));
    }

    /// <summary>
    /// Verifies that the home page is displayed and fully loaded.
    /// </summary>
    /// <remarks>
    /// Creates a HomePage instance, ensures the page has loaded completely,
    /// and verifies the URL ends with '/' indicating the home page.
    /// </remarks>
    [Then("I should see the home page")]
    protected override async Task ThenIShouldSeeTheHomePage()
    {
        var homePage = new HomePage(Page);
        await homePage.EnsurePageLoaded();

        Assert.That(Page.Url.EndsWith('/'), Is.True, "Should be on home page");
    }

    #endregion
}
