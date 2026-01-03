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
    [Given("has user launched site")]
    [Given("the application is running")]
    [Given("I am on the home page")]
    public async Task GivenLaunchedSite()
    {
        await WhenUserLaunchesSite();
        await ThenPageLoadedOk();
    }

    /// <summary>
    /// Establishes that the user is on any page within the application.
    /// </summary>
    /// <remarks>
    /// This is a no-op step used to set context in scenarios where the specific
    /// page doesn't matter. Currently no action is needed as navigation is handled
    /// by other steps.
    /// </remarks>
    [Given("I am on any page in the application")]
    public async Task GivenIAmOnAnyPageInTheApplication()
    {
        await Task.CompletedTask;
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// Launches the application site and stores the page response.
    /// </summary>
    /// <remarks>
    /// Creates a BasePage instance, navigates to the site, and stores both
    /// the page model and response in the object store for verification.
    ///
    /// Provides Objects:
    /// - BasePage (page model)
    /// - IResponse (page response)
    /// </remarks>
    [When("User launches site")]
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
    [When("user visits the {option} page")]
    [Given("user visited the {option} page")]
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
    ///
    /// Requires Objects:
    /// - IResponse (from WhenUserLaunchesSite)
    /// </remarks>
    [Then("page loaded ok")]
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
    [Then("page title contains {text}")]
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
    [Then("page heading is {text}")]
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
    [Then("I should see the home page")]
    public async Task ThenIShouldSeeTheHomePage()
    {
        var homePage = _context.GetOrCreatePage<HomePage>();
        await homePage.EnsurePageLoaded();
        Assert.That(_context.Page.Url.EndsWith('/'), Is.True, "Should be on home page");
    }

    /// <summary>
    /// Navigates to the user's profile page and verifies it loads correctly.
    /// </summary>
    /// <remarks>
    /// Creates or retrieves the ProfilePage from the context and ensures
    /// the page loads before proceeding with profile-related steps.
    /// </remarks>
    [Given("I am viewing my profile page")]
    [When("I navigate to my profile page")]
    public async Task GivenIAmViewingMyProfilePage()
    {
        var profilePage = _context.GetOrCreatePage<ProfilePage>();
        await profilePage.NavigateAsync();
        var isOnProfile = await profilePage.IsOnProfilePageAsync();
        Assert.That(isOnProfile, Is.True, "Should be on profile page");
    }

    /// <summary>
    /// Verifies that the user is redirected to the home page.
    /// </summary>
    /// <remarks>
    /// Waits for home page to be ready and confirms the brochure section
    /// (characteristic element of home page) is visible.
    /// </remarks>
    [Then("I should be redirected to the home page")]
    public async Task ThenIShouldBeRedirectedToTheHomePage()
    {
        var homePage = _context.GetOrCreatePage<HomePage>();
        await homePage.WaitForPageReadyAsync();
        var isVisible = await homePage.BrochureSection.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Should be on home page");
    }

    /// <summary>
    /// Verifies that the user is redirected to their profile page.
    /// </summary>
    /// <remarks>
    /// Waits for profile page to be ready and confirms the page loaded correctly.
    /// Used after successful login or profile navigation actions.
    /// </remarks>
    [Then("I should be redirected to my profile page")]
    public async Task ThenIShouldBeRedirectedToMyProfilePage()
    {
        var profilePage = _context.GetOrCreatePage<ProfilePage>();
        await profilePage.WaitForPageReadyAsync();
        var isOnProfile = await profilePage.IsOnProfilePageAsync();
        Assert.That(isOnProfile, Is.True,
            "Should be redirected to profile page");
    }

    /// <summary>
    /// Attempts to navigate directly to login page URL, expecting failure/redirect.
    /// </summary>
    /// <remarks>
    /// Tests that authenticated users cannot access the login page directly.
    /// Navigates without expecting the page to load successfully (expecting redirect).
    /// </remarks>
    [When("I try to navigate directly to the login page, expecting it to fail")]
    public async Task WhenITryToNavigateDirectlyToTheLoginPageExpectingFailure()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.NavigateAsync(false);
    }

    /// <summary>
    /// Attempts to navigate directly to a protected page by URL.
    /// </summary>
    /// <param name="page">The URL path of the protected page.</param>
    /// <remarks>
    /// Tests that unauthenticated users are redirected to login when accessing
    /// protected pages. Waits for redirect to login page to complete.
    /// </remarks>
    [When("I try to navigate directly to a protected page like {page}")]
    public async Task WhenITryToNavigateDirectlyToAProtectedPageLike(string page)
    {
        // Navigate directly - should redirect to login page for anonymous users
        await _context.Page.GotoAsync(page);

        // Wait for redirect to complete by waiting for login page to be ready
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Verifies that the user is redirected to the login page.
    /// </summary>
    /// <remarks>
    /// Common assertion for protected page access scenarios where unauthenticated
    /// users should be redirected to login.
    /// </remarks>
    [Then("I should be redirected to the login page")]
    public async Task ThenIShouldBeRedirectedToTheLoginPage()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be redirected to login page");
    }

    /// <summary>
    /// Verifies that a message indicating login is required is displayed.
    /// </summary>
    /// <remarks>
    /// TODO: Implement check for explicit "login required" message display.
    /// Currently a placeholder.
    /// </remarks>
    [Then("I should see a message indicating I need to log in")]
    public async Task ThenIShouldSeeAMessageIndicatingINeedToLogIn()
    {
        // TODO: Check for login required message
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that after logging in, the user is redirected to the originally requested page.
    /// </summary>
    /// <remarks>
    /// Tests the return URL functionality where users attempting to access protected
    /// pages are redirected back after successful login.
    /// TODO: Implement verification of return URL redirect behavior.
    /// </remarks>
    [Then("after logging in, I should be redirected to the originally requested page")]
    public async Task ThenAfterLoggingInIShouldBeRedirectedToTheOriginallyRequestedPage()
    {
        // TODO: Verify redirect after login works correctly
        await Task.CompletedTask;
    }

    #endregion
}
