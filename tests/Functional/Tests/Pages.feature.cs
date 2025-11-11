using YoFi.V3.Tests.Functional.Steps;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// (Pages) All pages load and display successfully
/// </summary>
/// <remarks>
/// The idea here is one test per site page. We are not testing functionality.
/// We just want it to load, and take a nice screen shot. In the future, this could be
/// turned into an image-compare tests where we make sure the screen shots don't change.
/// </remarks>
public partial class PagesFeature_Tests : FunctionalTest
{
    /// <summary>
    /// Scenario: Root loads OK
    /// </summary>
    [Test]
    public async Task RootLoadsOK()
    {
        // When user launches site
        await WhenUserLaunchesSite();

        // Hook Before first Then Step
        await SaveScreenshotAsync();

        // Then page loaded ok
        await ThenPageLoadedOk();
    }

    /// <summary>
    /// Scenario: Every page loads OK
    /// </summary>
    [TestCase("Home")]
    [TestCase("Counter")]
    [TestCase("Weather")]
    [TestCase("About")]
    public async Task EveryPageLoadsOK(string page)
    {
        // Given user has launched site
        await GivenLaunchedSite();

        // When user selects option <page> in nav bar
        await VisitPage(page);

        // Hook Before first Then Step
        await SaveScreenshotAsync();

        // Then page loaded ok
        await ThenPageLoadedOk();

        // And page heading is <page>
        await PageHeadingIs(page);

        // And page title contains <page>
        await PageTitleContains(page);
    }
}
