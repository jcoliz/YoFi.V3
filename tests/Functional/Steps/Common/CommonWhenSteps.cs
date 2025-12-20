using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Common;

/// <summary>
/// Common When step definitions shared across all feature tests.
/// </summary>
public abstract class CommonWhenSteps : CommonGivenSteps
{
    #region Steps: WHEN

    /// <summary>
    /// When: User launches site
    /// </summary>
    protected override async Task WhenUserLaunchesSite()
    {
        var pageModel = It<BasePage>();
        var result = await pageModel.LaunchSite();
        _objectStore.Add(pageModel);
        _objectStore.Add(result);
    }

    /// <summary>
    /// When: user visits the (\S+) page, or
    /// Given: user visited the (\S+) page
    /// </summary>
    /// <param name="option">Displayed text of navbar item to click</param>
    protected async Task VisitPage(string option)
    {
        var pageModel = It<BasePage>();
        await pageModel.SiteHeader.Nav.SelectOptionAsync(option);
    }

    /// <summary>
    /// When: I enter my credentials
    /// </summary>
    protected override async Task WhenIEnterMyCredentials()
    {
        var loginPage = GetOrCreateLoginPage();

        var testuser = It<Generated.TestUserCredentials>();

        await loginPage.EnterCredentialsAsync(testuser.Username, testuser.Password);
    }

    /// <summary>
    /// When: I click the login button
    /// </summary>
    protected override async Task WhenIClickTheLoginButton()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.ClickLoginButtonAsync();

    }

    #endregion
}
