using Microsoft.Playwright;
using NUnit.Framework.Internal;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Common;

/// <summary>
/// Common When step definitions shared across all feature tests.
/// </summary>
public abstract class CommonWhenSteps : CommonGivenSteps
{
    #region Steps: WHEN

    /// <summary>
    /// Launches the application site and stores the page response.
    /// </summary>
    /// <remarks>
    /// Creates a BasePage instance, navigates to the site, and stores both
    /// the page model and response in the object store for verification.
    /// </remarks>
    [When("User launches site")]
    protected override async Task WhenUserLaunchesSite()
    {
        var pageModel = It<BasePage>();
        var result = await pageModel.LaunchSite();
        _objectStore.Add(pageModel);
        _objectStore.Add(result);
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
    protected async Task VisitPage(string option)
    {
        var pageModel = It<BasePage>();
        await pageModel.SiteHeader.Nav.SelectOptionAsync(option);
    }

    /// <summary>
    /// Clicks the login button to submit the login form.
    /// </summary>
    /// <remarks>
    /// Used after WhenIEnterMyCredentials to explicitly submit the form.
    /// Assumes credentials have already been entered into the form fields.
    /// </remarks>
    [When("I click the login button")]
    protected override async Task WhenIClickTheLoginButton()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.ClickLoginButtonAsync();
    }

    #endregion

    #region Helpers

    // NOTE: CreateTestUserCredentials() method moved to FunctionalTestBase
    // to support automatic credential tracking for cleanup

    /// <summary>
    /// Enters test user credentials into the login form without submitting.
    /// </summary>
    /// <remarks>
    /// Retrieves test user credentials from the object store and fills the login form fields.
    /// Does not submit the form - use with WhenIClickTheLoginButton for submission.
    /// This is a helper method, not a Gherkin step. The pattern "I enter my credentials"
    /// is handled by AuthenticationSteps.WhenIEnterMyCredentials(DataTable).
    /// </remarks>
    protected override async Task WhenIEnterMyCredentials()
    {
        var loginPage = GetOrCreateLoginPage();

        var testuser = It<Generated.TestUserCredentials>();

        await loginPage.EnterCredentialsAsync(testuser.Username, testuser.Password);
    }

    /// <summary>
    /// Performs a complete login operation with test user credentials.
    /// </summary>
    /// <remarks>
    /// Retrieves test user credentials from the object store and performs the full
    /// login action (entering credentials and submitting the form in one operation).
    /// This is a helper method, not a Gherkin step. The pattern "I enter my credentials"
    /// is handled by AuthenticationSteps.WhenIEnterMyCredentials(DataTable).
    /// </remarks>
    protected override async Task WhenILoginWithMyCredentials()
    {
        var loginPage = GetOrCreateLoginPage();

        var testuser = It<Generated.TestUserCredentials>();

        await loginPage.LoginAsync(testuser.Username, testuser.Password);
    }

    #endregion
}
