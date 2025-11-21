using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Generated;
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
public abstract class AuthenticationSteps : FunctionalTest
{
    private TestControlClient? _testControlClient;
    protected TestControlClient testControlClient
    {
        get
        {
            if (_testControlClient is null)
            {
                _testControlClient = new TestControlClient(
                    baseUrl:
                        TestContext.Parameters["apiBaseUrl"]
                        ?? throw new NullReferenceException("apiBaseUrl test parameter not set"),
                    httpClient: new HttpClient()
                );
            }
            return _testControlClient;
        }
    }
    #region Steps: GIVEN

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
    /// Given: I am on the registration page
    /// </summary>
    protected async Task GivenIAmOnTheRegistrationPage()
    {
        await Page.GotoAsync("/register");
        var registerPage = GetOrCreateRegisterPage();
        Assert.That(await registerPage.IsOnRegistrationPageAsync(), Is.True, "Should be on registration page");

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
    /// Given: I have an existing account
    /// </summary>
    protected async Task GivenIHaveAnExistingAccount()
    {
        // TODO: Implement account creation via Test Control API

        var testUser = new TestUser(1);
        _objectStore.Add(testUser);
        // TODO: Send request to create account via API
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given: I am on the login page
    /// </summary>
    protected async Task GivenIAmOnTheLoginPage()
    {
        await Page.GotoAsync("/login");
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be on login page");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    }

    /// <summary>
    /// Given: I am logged in as {email}
    /// </summary>
    protected async Task GivenIAmLoggedInAs(string email)
    {
        // TODO: Implement login via API or direct authentication
        // For now, go through UI login
        await GivenIAmOnTheLoginPage();
        await WhenIEnterMyCredentials(email, "MyPassword123!");
        await WhenIClickTheLoginButton();
        await ThenIShouldBeSuccessfullyLoggedIn();
    }

    /// <summary>
    /// Given: I am on any page in the application
    /// </summary>
    protected async Task GivenIAmOnAnyPageInTheApplication()
    {
        await Page.GotoAsync("/");
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
        _objectStore.Add(user);

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
    /// When: I enter my existing credentials
    /// </summary>
    protected async Task WhenIEnterMyExistingCredentials()
    {
        var loginPage = GetOrCreateLoginPage();

        var testuser = It<TestUser>();

        await loginPage.EnterCredentialsAsync(testuser.Email, testuser.Password);
    }

    /// <summary>
    /// When: I enter my credentials
    /// </summary>
    protected async Task WhenIEnterMyCredentials(DataTable credentialsData)
    {
        var loginPage = GetOrCreateLoginPage();
        var email = GetTableValue(credentialsData, "Email");
        var password = GetTableValue(credentialsData, "Password");

        await loginPage.EnterCredentialsAsync(email, password);
    }

    // Overload for cases where email/password are passed directly (like from other steps)
    protected async Task WhenIEnterMyCredentials(string email, string password)
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.EnterCredentialsAsync(email, password);
    }

    /// <summary>
    /// When: I click the login button
    /// </summary>
    protected async Task WhenIClickTheLoginButton()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.ClickLoginButtonAsync();
    }

    /// <summary>
    /// When: I enter invalid credentials
    /// </summary>
    protected async Task WhenIEnterInvalidCredentials(DataTable credentialsData)
    {
        var loginPage = GetOrCreateLoginPage();
        var email = GetTableValue(credentialsData, "Email");
        var password = GetTableValue(credentialsData, "Password");

        await loginPage.EnterCredentialsAsync(email, password);
    }

    /// <summary>
    /// When: I enter only an email address {email}
    /// </summary>
    protected async Task WhenIEnterOnlyAnEmailAddress(string email)
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.EnterEmailOnlyAsync(email);
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
    /// When: I navigate to my profile page
    /// </summary>
    protected async Task WhenINavigateToMyProfilePage()
    {
        await Page.GotoAsync("/profile");
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
    }

    /// <summary>
    /// When: I enter registration details with a weak password
    /// </summary>
    protected async Task WhenIEnterRegistrationDetailsWithAWeakPassword(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetValue("Email");
        var password = registrationData.GetValue("Password");

        await registerPage.EnterWeakPasswordDetailsAsync(email, "newuser", password);
    }

    /// <summary>
    /// When: I enter registration details with mismatched passwords
    /// </summary>
    protected async Task WhenIEnterRegistrationDetailsWithMismatchedPasswords(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetValue("Email");
        var password = registrationData.GetValue("Password");
        var confirmPassword = registrationData.GetValue("Confirm Password");

        await registerPage.EnterMismatchedPasswordDetailsAsync(email, "newuser", password, confirmPassword);
    }

    /// <summary>
    /// When: I enter registration details
    /// </summary>
    protected async Task WhenIEnterRegistrationDetails(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetValue("Email");
        var password = registrationData.GetValue("Password");
        var confirmPassword = registrationData.GetValue("Confirm Password");

        await registerPage.EnterRegistrationDetailsAsync(email, "existinguser", password, confirmPassword);
    }

    /// <summary>
    /// When: I try to navigate to the login page
    /// </summary>
    protected async Task WhenITryToNavigateToTheLoginPage()
    {
        await Page.GotoAsync("/login");
    }

    /// <summary>
    /// When: I try to navigate to a protected page like {page}
    /// </summary>
    protected async Task WhenITryToNavigateToAProtectedPageLike(string page)
    {
        await Page.GotoAsync(page);
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

        var user = It<TestUser>();

        var emailDisplayText = await registerPage.EmailDisplay.InnerTextAsync();
        var usernameDisplayText = await registerPage.UsernameDisplay.InnerTextAsync();

        Assert.That(emailDisplayText, Is.EqualTo(user.Email), "Displayed email should match registered email");
        Assert.That(usernameDisplayText, Is.EqualTo(user.Username), "Displayed username should match registered username");
    }

    /// <summary>
    /// Then: I should be automatically logged in
    /// </summary>
    protected async Task ThenIShouldBeAutomaticallyLoggedIn()
    {
        // TODO: Verify login state - check for user info in navigation
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should be redirected to my default workspace
    /// </summary>
    protected async Task ThenIShouldBeRedirectedToMyDefaultWorkspace()
    {
        // TODO: Verify redirect to workspace dashboard
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should be successfully logged in
    /// </summary>
    protected async Task ThenIShouldBeSuccessfullyLoggedIn()
    {
        // TODO: Verify successful login state
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should see my profile page
    /// </summary>
    protected async Task ThenIShouldSeeMyProfilePage()
    {
        var profilePage = GetOrCreateProfilePage();
        await profilePage.WaitForOnProfilePageAsync();
        Assert.That(await profilePage.IsOnProfilePageAsync(), Is.True, "Should be on profile page");
    }

    /// <summary>
    /// Then: I should see my username in the header
    /// </summary>
    protected async Task ThenIShouldSeeMyUsernameInTheHeader()
    {
        var testuser = It<TestUser>();
        var usernameInHeader = await Page.GetByTestId("site-header").GetByTestId("login-state").GetByTestId("username").TextContentAsync();
        Assert.That(usernameInHeader, Is.EqualTo(testuser.Username), "Username should be visible in the header");
    }

    /// <summary>
    /// Then: I should see my username on the profile page
    /// </summary>
    protected async Task ThenIShouldSeeMyUsernameOnTheProfilePage()
    {
        var testuser = It<TestUser>();
        var profilePage = GetOrCreateProfilePage();
        var usernameText = await profilePage.UsernameDisplay.TextContentAsync();
        Assert.That(usernameText, Does.Contain(testuser.Username), "Username should be visible on the profile page");
    }

    /// <summary>
    /// Then: I should see my workspace dashboard
    /// </summary>
    protected async Task ThenIShouldSeeMyWorkspaceDashboard()
    {
        // TODO: Verify workspace dashboard is displayed
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should see my email address in the navigation
    /// </summary>
    protected async Task ThenIShouldSeeMyEmailAddressInTheNavigation()
    {
        // TODO: Check navigation for user email display
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should see an error message {errorMessage}
    /// </summary>
    protected async Task ThenIShouldSeeAnErrorMessage(string errorMessage)
    {
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.HasErrorMessageAsync(errorMessage), Is.True,
            $"Should display error message: {errorMessage}");
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
    /// Then: I should not be logged in
    /// </summary>
    protected async Task ThenIShouldNotBeLoggedIn()
    {
        // TODO: Verify not logged in state
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should see a validation error {errorMessage}
    /// </summary>
    protected async Task ThenIShouldSeeAValidationError(string errorMessage)
    {
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.HasValidationErrorAsync(errorMessage), Is.True,
            $"Should display validation error: {errorMessage}");
    }

    /// <summary>
    /// Then: I should see my account information
    /// </summary>
    protected async Task ThenIShouldSeeMyAccountInformation(DataTable expectedData)
    {
        var profilePage = GetOrCreateProfilePage();
        var expectedEmail = expectedData.GetValue("Email");
        var expectedUsername = expectedData.GetValue("Username");

        Assert.That(await profilePage.HasAccountInformationAsync(expectedEmail, expectedUsername), Is.True,
            "Should display correct account information");
    }

    /// <summary>
    /// Then: I should see options to update my profile
    /// </summary>
    protected async Task ThenIShouldSeeOptionsToUpdateMyProfile()
    {
        var profilePage = GetOrCreateProfilePage();
        Assert.That(await profilePage.HasUpdateProfileOptionsAsync(), Is.True,
            "Should see profile update options");
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
        // TODO: Verify logged out state
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should be redirected to the home page
    /// </summary>
    protected async Task ThenIShouldBeRedirectedToTheHomePage()
    {
        Assert.That(Page.Url.EndsWith("/"), Is.True, "Should be redirected to home page");
    }

    /// <summary>
    /// Then: I should see the login option in the navigation
    /// </summary>
    protected async Task ThenIShouldSeeTheLoginOptionInTheNavigation()
    {
        // TODO: Check navigation for login option
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should not see any personal information
    /// </summary>
    protected async Task ThenIShouldNotSeeAnyPersonalInformation()
    {
        // TODO: Verify personal info is not displayed
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should see a validation error about password requirements
    /// </summary>
    protected async Task ThenIShouldSeeAValidationErrorAboutPasswordRequirements()
    {
        var registerPage = GetOrCreateRegisterPage();
        Assert.That(await registerPage.HasPasswordRequirementErrorAsync(), Is.True,
            "Should display password requirement validation error");
    }

    /// <summary>
    /// Then: I should remain on the registration page
    /// </summary>
    protected async Task ThenIShouldRemainOnTheRegistrationPage()
    {
        var registerPage = GetOrCreateRegisterPage();
        Assert.That(await registerPage.IsOnRegistrationPageAsync(), Is.True, "Should remain on registration page");
    }

    /// <summary>
    /// Then: I should not be registered
    /// </summary>
    protected async Task ThenIShouldNotBeRegistered()
    {
        // TODO: Verify registration did not succeed
        await Task.CompletedTask;
    }

    /// <summary>
    /// Then: I should be automatically redirected to my workspace dashboard
    /// </summary>
    protected async Task ThenIShouldBeAutomaticallyRedirectedToMyWorkspaceDashboard()
    {
        // TODO: Verify redirect to workspace dashboard
        await Task.CompletedTask;
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

    #endregion

    #region Helpers

    /// <summary>
    /// Extract a value from a data table by field name
    /// </summary>
    private string GetTableValue(DataTable table, string fieldName)
    {
        foreach (var row in table.Rows)
        {
            if (row["Field"] == fieldName)
            {
                return row["Value"];
            }
        }
        throw new ArgumentException($"Field '{fieldName}' not found in table data");
    }

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
    /// Get or create LoginPage and store it in the object store
    /// </summary>
    private LoginPage GetOrCreateLoginPage()
    {
        if (!_objectStore.Contains<LoginPage>())
        {
            var loginPage = new LoginPage(Page);
            _objectStore.Add(loginPage);
        }
        return It<LoginPage>();
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
