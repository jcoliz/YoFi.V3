using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Steps.Common;
using NUnit.Framework.Internal;

namespace YoFi.V3.Tests.Functional.Steps;

public class TestUser(int id)
{
    public string Email { get; init; } = $"__TEST__{id:X8}@example.com";
    public string Username { get; init; } = $"__TEST__{id:X8}";
    public string Password { get; init; } = "MyPassword123!";
}

/// <summary>
/// Step definitions for Authentication feature tests
/// </summary>
public abstract class AuthenticationSteps : CommonThenSteps
{
    #region Steps: GIVEN

    /// <summary>
    /// Given: I am on the registration page
    /// </summary>
    protected async Task GivenIAmOnTheRegistrationPage()
    {
        await Page.GotoAsync("/register");
        var registerPage = GetOrCreateRegisterPage();
        Assert.That(await registerPage.IsRegisterFormVisibleAsync(), Is.True, "Should be on registration page");

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Given: I have an existing account with email {email}
    /// </summary>
    protected async Task GivenIHaveAnExistingAccountWithEmail(string email)
    {
        // TODO: Implement account creation via API or database setup
        // For now, assume test accounts exist
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given: I am on any page in the application
    /// </summary>
    protected async Task GivenIAmOnAnyPageInTheApplication()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given: I am viewing my workspace dashboard
    /// </summary>
    protected async Task GivenIAmViewingMyWorkspaceDashboard()
    {
        await Page.GotoAsync("/workspace/dashboard");
    }

    /// <summary>
    /// Given: an account already exists with email {email}
    /// </summary>
    protected async Task GivenAnAccountAlreadyExistsWithEmail(string email)
    {
        // TODO: Implement account creation via API or database setup
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given: I am viewing my profile page
    /// </summary>
    protected async Task GivenIAmViewingMyProfilePage()
    {
        await Page.GotoAsync("/profile");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var profilePage = GetOrCreateProfilePage();
        Assert.That(await profilePage.IsOnProfilePageAsync(), Is.True, "Should be on profile page");
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// When: I enter valid registration details
    /// </summary>
    protected async Task WhenIEnterValidRegistrationDetails()
    {
        var registerPage = GetOrCreateRegisterPage();

        // First, clear existing test users via Test Control API
        await testControlClient.DeleteUsersAsync();

        var user = new TestUser(TestContext.CurrentContext.Test.ID.GetHashCode());
        _objectStore.Add("Registration Details", user);

        await registerPage.EnterRegistrationDetailsAsync(user.Email, user.Username, user.Password, user.Password);
    }

    /// <summary>
    /// When: I submit the registration form
    /// </summary>
    protected async Task WhenISubmitTheRegistrationForm()
    {
        var registerPage = GetOrCreateRegisterPage();
        await registerPage.ClickRegisterButtonAsync();
    }

    /// <summary>
    /// When: I submit the registration form (for validation)
    /// </summary>
    protected async Task WhenISubmitTheRegistrationFormForValidation()
    {
        var registerPage = GetOrCreateRegisterPage();
        await registerPage.ClickRegisterButtonWithoutApiWaitAsync();
    }

    /// <summary>
    /// When: I enter my credentials
    /// </summary>
    protected async Task WhenIEnterMyCredentials(DataTable credentialsData)
    {
        var loginPage = GetOrCreateLoginPage();
        var email = credentialsData.GetKeyValue("Email");
        var password = credentialsData.GetKeyValue("Password");

        await loginPage.EnterCredentialsAsync(email, password);
    }

    // Overload for cases where email/password are passed directly (like from other steps)
    protected async Task WhenIEnterMyCredentials(string email, string password)
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.EnterCredentialsAsync(email, password);
    }


    /// <summary>
    /// When: I enter invalid credentials
    /// </summary>
    protected async Task WhenIEnterInvalidCredentials(DataTable credentialsData)
    {
        var loginPage = GetOrCreateLoginPage();
        var email = credentialsData.GetKeyValue("Email");
        var password = credentialsData.GetKeyValue("Password");

        await loginPage.EnterCredentialsAsync(email, password);
    }

    /// <summary>
    /// When: I enter invalid credentials
    /// </summary>
    protected async Task WhenIEnterInvalidCredentials()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.EnterCredentialsAsync("invalid@example.com", "WrongPassword123!");
    }

    /// <summary>
    /// When: I enter only a username
    /// </summary>
    protected async Task WhenIEnterOnlyUsername()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.EnterUsernameOnlyAsync("Only Username");
    }

    /// <summary>
    /// When: I leave the password field empty
    /// </summary>
    protected async Task WhenILeaveThePasswordFieldEmpty()
    {
        // This is handled by WhenIEnterOnlyAnEmailAddress
        await Task.CompletedTask;
    }

    /// <summary>
    /// When: I click the login button (for validation)
    /// </summary>
    /// <remarks>
    /// Used in scenarios where no API call is expected (e.g., client-side validation)
    /// </remarks>
    protected async Task WhenIClickTheLoginButtonForValidation()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.ClickLoginButtonWithoutApiWaitAsync();
    }

    /// <summary>
    /// When: I navigate to my profile page
    /// </summary>
    protected async Task WhenINavigateToMyProfilePage()
    {
        await Page.GotoAsync("/profile");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var profilePage = GetOrCreateProfilePage();
        Assert.That(await profilePage.IsOnProfilePageAsync(), Is.True, "Should be on profile page");
    }

    /// <summary>
    /// When: I click the logout button
    /// </summary>
    protected async Task WhenIClickTheLogoutButton()
    {
        var profilePage = GetOrCreateProfilePage();
        await profilePage.ClickLogoutAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// When: I enter registration details with a weak password
    /// </summary>
    protected async Task WhenIEnterRegistrationDetailsWithAWeakPassword()
    {
        var registerPage = GetOrCreateRegisterPage();

        // First, clear existing test users via Test Control API
        await testControlClient.DeleteUsersAsync();

        var user = new TestUser(TestContext.CurrentContext.Test.ID.GetHashCode());
        _objectStore.Add("Registration Details", user);

        // Use a weak password (too short, no special characters, etc.)
        var weakPassword = "weak";
        await registerPage.EnterWeakPasswordDetailsAsync(user.Email, user.Username, weakPassword);
    }

    /// <summary>
    /// When: I enter registration details with a weak password (overload with DataTable)
    /// </summary>
    protected async Task WhenIEnterRegistrationDetailsWithAWeakPassword(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetKeyValue("Email");
        var password = registrationData.GetKeyValue("Password");

        await registerPage.EnterWeakPasswordDetailsAsync(email, "newuser", password);
    }

    /// <summary>
    /// When: I enter registration details with mismatched passwords
    /// </summary>
    protected async Task WhenIEnterRegistrationDetailsWithMismatchedPasswords()
    {
        var registerPage = GetOrCreateRegisterPage();

        // First, clear existing test users via Test Control API
        await testControlClient.DeleteUsersAsync();

        var user = new TestUser(TestContext.CurrentContext.Test.ID.GetHashCode());
        _objectStore.Add("Registration Details", user);

        // Use mismatched passwords
        var password = user.Password;
        var confirmPassword = "DifferentPassword123!";
        await registerPage.EnterMismatchedPasswordDetailsAsync(user.Email, user.Username, password, confirmPassword);
    }

    /// <summary>
    /// When: I enter registration details with mismatched passwords (overload with DataTable)
    /// </summary>
    protected async Task WhenIEnterRegistrationDetailsWithMismatchedPasswords(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetKeyValue("Email");
        var password = registrationData.GetKeyValue("Password");
        var confirmPassword = registrationData.GetKeyValue("Confirm Password");

        await registerPage.EnterMismatchedPasswordDetailsAsync(email, "newuser", password, confirmPassword);
    }

    /// <summary>
    /// When: I enter registration details
    /// </summary>
    protected async Task WhenIEnterRegistrationDetails(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetKeyValue("Email");
        var password = registrationData.GetKeyValue("Password");
        var confirmPassword = registrationData.GetKeyValue("Confirm Password");

        await registerPage.EnterRegistrationDetailsAsync(email, "existinguser", password, confirmPassword);
    }

    /// <summary>
    /// When: I enter registration details with the existing email
    /// </summary>
    protected async Task WhenIEnterRegistrationDetailsWithTheExistingEmail()
    {
        var registerPage = GetOrCreateRegisterPage();

        // Get the existing user from object store
        var existingUser = It<Generated.TestUserCredentials>();

        // Create a new username but use the existing email
        var newUsername = $"__DUPLICATE__{existingUser.Username}";

        // Use existing email with different username and password
        await registerPage.EnterRegistrationDetailsAsync(
            existingUser.Email,
            newUsername,
            existingUser.Password,
            existingUser.Password);
    }

    /// <summary>
    /// When: I try to navigate to the login page
    /// </summary>
    /// <remarks>
    /// Note this step is distinct from "When I try to navigate directly to the login page".
    /// TODO: This step should simulate navigation via in-app links/buttons.
    /// </remarks>
    protected async Task WhenITryToNavigateToTheLoginPage()
    {
        await Page.GotoAsync("/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// When: I try to navigate directly to the login page
    /// </summary>
    protected async Task WhenITryToNavigateDirectlyToTheLoginPage()
    {
        await Page.GotoAsync("/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// When: I try to navigate directly to a protected page like {page}
    /// </summary>
    protected async Task WhenITryToNavigateDirectlyToAProtectedPageLike(string page)
    {
        await Page.GotoAsync(page);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Then: My registration request should be acknowledged
    /// </summary>
    protected async Task ThenMyRegistrationRequestShouldBeAcknowledged()
    {
        var registerPage = GetOrCreateRegisterPage();
        await registerPage.SuccessMessage.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var user = The<TestUser>("Registration Details");

        var emailDisplayText = await registerPage.EmailDisplay.InnerTextAsync();
        var usernameDisplayText = await registerPage.UsernameDisplay.InnerTextAsync();

        Assert.That(emailDisplayText, Is.EqualTo(user.Email), "Displayed email should match registered email");
        Assert.That(usernameDisplayText, Is.EqualTo(user.Username), "Displayed username should match registered username");
    }

    /// <summary>
    /// Then: I should be successfully logged in
    /// </summary>
    protected async Task ThenIShouldBeSuccessfullyLoggedIn()
    {

        // And I should see my username in the header
        await ThenIShouldSeeMyUsernameInTheHeader();

        // Actually I think there is nothing to do here other than verify no error occurred
        // Following steps will verify username in header
        //
        // Hmm, maybe I should move the "username in header" check here?

        // TODO: Verify successful login state
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should see my username in the header
    /// </summary>
    protected async Task ThenIShouldSeeMyUsernameInTheHeader()
    {
        var basePage = new BasePage(Page);
        var testuser = It<Generated.TestUserCredentials>();
        var usernameInHeader = await basePage.SiteHeader.LoginState.GetUsernameAsync();
        Assert.That(usernameInHeader, Is.EqualTo(testuser.Username), "Username should be visible in the header");
    }

    /// <summary>
    /// Then: I should see an error message containing (.+)
    /// </summary>
    /// <remarks>
    /// Same step used for both login and registration error checks?
    /// </remarks>
    protected async Task ThenIShouldSeeAnErrorMessage(string errorMessage)
    {
        // This method works for both login and registration pages

        // The way to tell which page we're on is to ask the object store.
        if (_objectStore.Contains<RegisterPage>())
        {
            var registerPage = It<RegisterPage>();
            if (await registerPage.IsRegisterFormVisibleAsync())
            {
                Assert.That(await registerPage.HasErrorMessageAsync(errorMessage), Is.True,
                    $"Should display error message containing: {errorMessage}");
            }
        }
        else if (_objectStore.Contains<LoginPage>())
        {
            var loginPage = It<LoginPage>();
            if (await loginPage.IsOnLoginPageAsync())
            {
                Assert.That(await loginPage.HasErrorMessageAsync(errorMessage), Is.True,
                    $"Should display error message containing: {errorMessage}");
            }
        }
        else
        {
            throw new InvalidOperationException("No page object for login or registration found in object store");
        }
    }

    /// <summary>
    /// Then: I should remain on the login page
    /// </summary>
    protected async Task ThenIShouldRemainOnTheLoginPage()
    {
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should remain on login page");
    }

    /// <summary>
    /// Then: I should see a validation error {errorMessage}
    /// </summary>
    protected async Task ThenIShouldSeeAValidationError()
    {
        var loginPage = GetOrCreateLoginPage();

        // First check for HTML5 validation (browser native)
        if (await loginPage.HasPasswordRequiredValidationAsync())
        {
            var validationMessage = await loginPage.GetPasswordValidationMessageAsync();
            Assert.That(validationMessage, Is.Not.Empty,
                "Should have HTML5 validation message for required password field");
            return;
        }

        // Fall back to checking custom error display
        Assert.That(await loginPage.HasValidationErrorAsync(), Is.True,
            $"Should display validation error");
    }

    /// <summary>
    /// Then: I should see my account information
    /// </summary>
    protected async Task ThenIShouldSeeMyAccountInformation()
    {
        var profilePage = GetOrCreateProfilePage();
        var testuser = It<Generated.TestUserCredentials>();

        Assert.That(await profilePage.HasAccountInformationAsync(testuser.Email, testuser.Username), Is.True,
            "Should display correct account information");
    }


    /// <summary>
    /// Then: I should see my current workspace information
    /// </summary>
    protected async Task ThenIShouldSeeMyCurrentWorkspaceInformation()
    {
        var profilePage = GetOrCreateProfilePage();
        Assert.That(await profilePage.HasWorkspaceInformationAsync(), Is.True,
            "Should display workspace information");
    }

    /// <summary>
    /// Then: I should be logged out
    /// </summary>
    protected async Task ThenIShouldBeLoggedOut()
    {
        var basePage = new BasePage(Page);
        Assert.That(await basePage.SiteHeader.LoginState.IsLoggedInAsync(), Is.False,
            "User should be logged out");
    }

    /// <summary>
    /// Then: I should be redirected to the home page
    /// </summary>
    protected async Task ThenIShouldBeRedirectedToTheHomePage()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var homePage = new HomePage(Page);
        await homePage.BrochureSection.WaitForAsync(new LocatorWaitForOptions { Timeout = 3000 });
        Assert.That(await homePage.BrochureSection.IsVisibleAsync(), Is.True, "Should be on home page");
    }

    /// <summary>
    /// Then: I should see the login option in the navigation
    /// </summary>
    protected async Task ThenIShouldSeeTheLoginOptionInTheNavigation()
    {
        var basePage = new BasePage(Page);
        await basePage.SiteHeader.LoginState.OpenMenuAsync();
        Assert.That(await basePage.SiteHeader.LoginState.SignInMenuItem.IsVisibleAsync(), Is.True,
            "Signin option should be visible in navigation");
    }

    /// <summary>
    /// Then: I should not see any personal information
    /// </summary>
    protected async Task ThenIShouldNotSeeAnyPersonalInformation()
    {
        var basePage = new BasePage(Page);
        var usernameInHeader = await basePage.SiteHeader.LoginState.GetUsernameAsync();
        Assert.That(usernameInHeader, Is.Null, "Username should not be visible in the header");

    }

    /// <summary>
    /// Then: I should remain on the registration page
    /// </summary>
    protected async Task ThenIShouldRemainOnTheRegistrationPage()
    {
        var registerPage = GetOrCreateRegisterPage();
        Assert.That(await registerPage.IsRegisterFormVisibleAsync(), Is.True, "Should remain on registration page");
    }

    /// <summary>
    /// Then: I should not see the login form
    /// </summary>
    protected async Task ThenIShouldNotSeeTheLoginForm()
    {
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.False, "Should not see login form");
    }

    /// <summary>
    /// Then: I should be redirected to the login page
    /// </summary>
    protected async Task ThenIShouldBeRedirectedToTheLoginPage()
    {
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be redirected to login page");
    }

    /// <summary>
    /// Then: I should see a message indicating I need to log in
    /// </summary>
    protected async Task ThenIShouldSeeAMessageIndicatingINeedToLogIn()
    {
        // TODO: Check for login required message
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: after logging in, I should be redirected to the originally requested page
    /// </summary>
    protected async Task ThenAfterLoggingInIShouldBeRedirectedToTheOriginallyRequestedPage()
    {
        // TODO: Verify redirect after login works correctly
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should not be registered
    /// </summary>
    protected async Task ThenIShouldNotBeRegistered()
    {
        // Verify that registration did not succeed by checking we're still on registration page
        // and no success message is shown
        var registerPage = GetOrCreateRegisterPage();
        Assert.That(await registerPage.IsRegisterFormVisibleAsync(), Is.True,
            "Should still be on registration page");

        // Verify no success confirmation is displayed
        var hasSuccessMessage = await registerPage.SuccessMessage.IsVisibleAsync();
        Assert.That(hasSuccessMessage, Is.False,
            "Should not show success message for failed registration");
    }

    /// <summary>
    /// Then: I should be redirected to my profile page
    /// </summary>
    protected async Task ThenIShouldBeRedirectedToMyProfilePage()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var profilePage = GetOrCreateProfilePage();
        Assert.That(await profilePage.IsOnProfilePageAsync(), Is.True,
            "Should be redirected to profile page");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Get or create RegisterPage and store it in the object store
    /// </summary>
    private RegisterPage GetOrCreateRegisterPage()
    {
        if (!_objectStore.Contains<RegisterPage>())
        {
            var registerPage = new RegisterPage(Page);
            _objectStore.Add(registerPage);
        }
        return It<RegisterPage>();
    }

    /// <summary>
    /// Get or create ProfilePage and store it in the object store
    /// </summary>
    private ProfilePage GetOrCreateProfilePage()
    {
        if (!_objectStore.Contains<ProfilePage>())
        {
            var profilePage = new ProfilePage(Page);
            _objectStore.Add(profilePage);
        }
        return It<ProfilePage>();
    }

    #endregion
}
