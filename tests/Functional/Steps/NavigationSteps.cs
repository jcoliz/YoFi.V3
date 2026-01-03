using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for site navigation operations.
/// </summary>
/// <param name="_context">The test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles navigation patterns including launching the site, navigating to pages
/// via the navigation bar, and page-to-page transitions.
/// </remarks>
public class NavigationSteps(ITestContext _context)
{
    #region Steps: GIVEN

    /// <summary>
    /// Ensures the application is running and the home page has loaded successfully.
    /// </summary>
    /// <remarks>
    /// This step launches the site and verifies the page loads correctly.
    /// Used as a common precondition for many test scenarios.
    /// </remarks>
    //[Given("has user launched site")]
    //[Given("the application is running")]
    //[Given("I am on the home page")]
    public async Task GivenLaunchedSite()
    {
        await WhenUserLaunchesSite();
        await ThenPageLoadedOk();
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// Launches the application site and stores the page response.
    /// </summary>
    /// <remarks>
    /// Creates a BasePage instance, navigates to the site, and stores both
    /// the page model and response in the object store for verification.
    /// </remarks>
    //[When("User launches site")]
    public async Task WhenUserLaunchesSite()
    {
        var pageModel = _context.GetOrCreatePage<BasePage>();
        var result = await pageModel.LaunchSite();
        _context.ObjectStore.Add(pageModel);
        _context.ObjectStore.Add(result);
    }

    /// <summary>
    /// Navigates to a specific page by selecting an option from the navigation bar.
    /// </summary>
    /// <param name="option">The displayed text of the navbar item to click.</param>
    /// <remarks>
    /// Uses the site header navigation component to select and navigate to the specified page.
    /// Can be used as either a When or Given step depending on test context.
    /// </remarks>
    //[When("user visits the {option} page")]
    //[Given("user visited the {option} page")]
    public async Task UserVisitsPage(string option)
    {
        var pageModel = _context.GetOrCreatePage<BasePage>();
        await pageModel.SiteHeader.Nav.SelectOptionAsync(option);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that the current page loaded successfully with HTTP 200 OK.
    /// </summary>
    /// <remarks>
    /// Retrieves the page response from the object store and asserts that
    /// the Ok property is true, indicating a successful page load.
    /// </remarks>
    //[Then("page loaded ok")]
    public Task ThenPageLoadedOk()
    {
        var response = _context.ObjectStore.Get<IResponse>();
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
    //[Then("page title contains {text}")]
    public async Task PageTitleContains(string text)
    {
        var pageModel = _context.GetOrCreatePage<BasePage>();
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
    //[Then("page heading is {text}")]
    public async Task PageHeadingIs(string text)
    {
        var pageModel = _context.GetOrCreatePage<BasePage>();
        var heading1 = await pageModel.GetPageHeading();
        Assert.That(heading1, Is.EqualTo(text));
    }

    /// <summary>
    /// Verifies that the home page is displayed and fully loaded.
    /// </summary>
    /// <remarks>
    /// Creates a HomePage instance, ensures the page has loaded completely,
    /// and verifies the URL ends with '/' indicating the home page.
    /// </remarks>
    //[Then("I should see the home page")]
    public async Task ThenIShouldSeeTheHomePage()
    {
        var homePage = _context.GetOrCreatePage<HomePage>();
        await homePage.EnsurePageLoaded();
        Assert.That(_context.Page.Url.EndsWith('/'), Is.True, "Should be on home page");
    }

    #endregion
}
