using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Common;

/// <summary>
/// Common Given step definitions shared across all feature tests.
/// </summary>
public abstract class CommonGivenSteps : FunctionalTestBase
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
    protected async Task GivenLaunchedSite()
    {
        await WhenUserLaunchesSite();
        await ThenPageLoadedOk();
    }

    /// <summary>
    /// Verifies that no user is currently logged into the application.
    /// </summary>
    /// <remarks>
    /// Currently assumes a clean state. Future implementation may include
    /// explicit logout if a user session is detected.
    /// </remarks>
    [Given("I am not logged in")]
    protected async Task GivenIAmNotLoggedIn()
    {
        // TODO: Implement logout if already logged in
        // For now, assume we start from a clean state
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a test user account if one doesn't already exist.
    /// </summary>
    /// <remarks>
    /// Checks the object store for existing credentials before creating a new account.
    /// The created user credentials are stored in the object store for use in subsequent steps.
    /// </remarks>
    [Given("I have an existing account")]
    protected async Task GivenIHaveAnExistingAccount()
    {
        if (_objectStore.Contains<Generated.TestUserCredentials>())
            return;
        await testControlClient.DeleteUsersAsync();
        var user = await testControlClient.CreateUserAsync();
        _objectStore.Add(user);
    }

    /// <summary>
    /// Navigates to the login page and verifies it loads correctly.
    /// </summary>
    /// <remarks>
    /// Marked as virtual to allow derived classes to customize login page navigation.
    /// Creates and stores the LoginPage object in the object store for reuse.
    /// </remarks>
    [Given("I am on the login page")]
    protected virtual async Task GivenIAmOnTheLoginPage()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.NavigateAsync();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be on login page");
    }

    /// <summary>
    /// Sets up a complete authenticated session with an existing test user.
    /// </summary>
    /// <remarks>
    /// This composite step performs the full login flow:
    /// 1. Creates an existing account (if needed)
    /// 2. Navigates to the login page
    /// 3. Logs in with test credentials
    /// 4. Verifies the home page loads
    /// </remarks>
    [Given("I am logged in")]
    protected async Task GivenIAmLoggedIn()
    {
        await GivenIHaveAnExistingAccount();
        await GivenIAmOnTheLoginPage();
        await WhenILoginWithMyCredentials();
        await ThenIShouldSeeTheHomePage();
    }

    #endregion

    #region Helper Methods (used by GIVEN steps)

    // These are forward references to WHEN and THEN steps that will be implemented in other classes
    protected abstract Task WhenUserLaunchesSite();
    protected abstract Task WhenIEnterMyCredentials();
    protected abstract Task WhenILoginWithMyCredentials();
    protected abstract Task WhenIClickTheLoginButton();
    protected abstract Task ThenPageLoadedOk();
    protected abstract Task ThenIShouldSeeTheHomePage();

    /// <summary>
    /// Get or create LoginPage and store it in the object store
    /// </summary>
    protected LoginPage GetOrCreateLoginPage()
    {
        if (!_objectStore.Contains<LoginPage>())
        {
            var loginPage = new LoginPage(Page);
            _objectStore.Add(loginPage);
        }
        return It<LoginPage>();
    }

    /// <summary>
    /// Get or create WeatherPage and store it in the object store
    /// </summary>
    protected WeatherPage GetOrCreateWeatherPage()
    {
        if (!_objectStore.Contains<WeatherPage>())
        {
            var weatherPage = new WeatherPage(Page);
            _objectStore.Add(weatherPage);
        }
        return It<WeatherPage>();
    }

    #endregion
}
