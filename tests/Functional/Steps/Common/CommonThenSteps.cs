using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Common;

/// <summary>
/// Common Then step definitions shared across all feature tests.
/// </summary>
public abstract class CommonThenSteps : CommonWhenSteps
{
    #region Steps: THEN

    /// <summary>
    /// Then: page loaded ok
    /// </summary>
    protected override Task ThenPageLoadedOk()
    {
        var response = It<IResponse>();

        Assert.That(response!.Ok, Is.True);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Then: page title contains (\S+)
    /// </summary>
    /// <param name="text">Text expected in page title</param>
    protected async Task PageTitleContains(string text)
    {
        var pageModel = It<BasePage>();
        var pageTitle = await pageModel.GetPageTitle();
        Assert.That(pageTitle, Does.Contain(text));
    }

    /// <summary>
    /// Then: page heading is (\S+)
    /// </summary>
    /// <param name="text">Text expected as the H1</param>
    protected async Task PageHeadingIs(string text)
    {
        var pageModel = It<BasePage>();
        var heading1 = await pageModel.GetPageHeading();
        Assert.That(heading1, Is.EqualTo(text));
    }

    /// <summary>
    /// Then: page contains (\S+) forecasts
    /// </summary>
    /// <param name="expectedCount"></param>
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
    protected override async Task ThenIShouldSeeTheHomePage()
    {
        await Task.Delay(1000);
        Assert.That(Page.Url.EndsWith('/'), Is.True, "Should be on home page");
    }

    #endregion
}
