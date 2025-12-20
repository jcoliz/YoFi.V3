using Microsoft.Playwright;
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
    /// Given: has user launched site
    /// </summary>
    protected async Task GivenLaunchedSite()
    {
        await WhenUserLaunchesSite();
        await ThenPageLoadedOk();
    }

    /// <summary>
    /// Given: the application is running
    /// </summary>
    protected async Task GivenTheApplicationIsRunning()
    {
        await GivenLaunchedSite();
    }

    /// <summary>
    /// Given: I am not logged in
    /// </summary>
    protected async Task GivenIAmNotLoggedIn()
    {
        // TODO: Implement logout if already logged in
        // For now, assume we start from a clean state
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given: I have an existing account
    /// </summary>
    protected async Task GivenIHaveAnExistingAccount()
    {
        if (_objectStore.Contains<Generated.TestUserCredentials>())
            return;
        await testControlClient.DeleteUsersAsync();
        var user = await testControlClient.CreateUserAsync();
        _objectStore.Add(user);
    }

    /// <summary>
    /// Given: I am on the login page
    /// </summary>
    protected virtual async Task GivenIAmOnTheLoginPage()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.NavigateAsync();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be on login page");
    }

    /// <summary>
    /// Given: I am logged in
    /// </summary>
    protected async Task GivenIAmLoggedIn()
    {
        await GivenIHaveAnExistingAccount();
        await GivenIAmOnTheLoginPage();
        await WhenIEnterMyCredentials();
        await WhenIClickTheLoginButton();
        await ThenIShouldSeeTheHomePage();
    }

    #endregion

    #region Helper Methods (used by GIVEN steps)

    // These are forward references to WHEN and THEN steps that will be implemented in other classes
    protected abstract Task WhenUserLaunchesSite();
    protected abstract Task WhenIEnterMyCredentials();
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
