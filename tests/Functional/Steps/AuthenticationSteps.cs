using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Steps.Common;
using NUnit.Framework.Internal;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Authentication feature tests.
/// </summary>
public abstract class AuthenticationSteps : CommonThenSteps
{
    #region Steps: GIVEN

    /// <summary>
    /// Navigates to the user registration page and verifies the form is displayed.
    /// </summary>
    /// <remarks>
    /// Creates or retrieves the RegisterPage from the object store and ensures
    /// the registration form is visible before proceeding with registration steps.
    /// </remarks>
    [Given("I am on the registration page")]
    protected async Task GivenIAmOnTheRegistrationPage()
    {
        var registerPage = GetOrCreateRegisterPage();
        await registerPage.NavigateAsync();
        Assert.That(await registerPage.IsRegisterFormVisibleAsync(), Is.True, "Should be on registration page");
    }

    /// <summary>
    /// Verifies that a user account exists with the specified email address.
    /// </summary>
    /// <param name="email">The email address of the existing account.</param>
    /// <remarks>
    /// This is a placeholder for future implementation that will create test accounts
    /// via API or database setup. Currently assumes accounts exist.
    /// </remarks>
    [Given("I have an existing account with email {email}")]
    protected async Task GivenIHaveAnExistingAccountWithEmail(string email)
    {
        // TODO: Implement account creation via API or database setup
        // For now, assume test accounts exist
        await Task.CompletedTask;
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
    protected async Task GivenIAmOnAnyPageInTheApplication()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Navigates directly to the workspace dashboard page.
    /// </summary>
    /// <remarks>
    /// Bypasses normal navigation flow to go directly to the dashboard URL.
    /// Used for testing authenticated workspace access scenarios.
    /// </remarks>
    [Given("I am viewing my workspace dashboard")]
    protected async Task GivenIAmViewingMyWorkspaceDashboard()
    {
        await Page.GotoAsync("/workspace/dashboard");
    }

    /// <summary>
    /// Verifies that a user account already exists with the specified email.
    /// </summary>
    /// <param name="email">The email address of the existing account.</param>
    /// <remarks>
    /// Similar to GivenIHaveAnExistingAccountWithEmail but phrased differently.
    /// TODO: Consider consolidating these similar methods or implementing actual account creation.
    /// </remarks>
    [Given("an account already exists with email {email}")]
    protected async Task GivenAnAccountAlreadyExistsWithEmail(string email)
    {
        // TODO: Implement account creation via API or database setup
        await Task.CompletedTask;
    }

    /// <summary>
    /// Navigates to the user's profile page and verifies it loads correctly.
    /// </summary>
    /// <remarks>
    /// Creates or retrieves the ProfilePage from the object store and ensures
    /// the page loads before proceeding with profile-related steps.
    /// </remarks>
    [Given("I am viewing my profile page")]
    protected async Task GivenIAmViewingMyProfilePage()
    {
        var profilePage = GetOrCreateProfilePage();
        await profilePage.NavigateAsync();
        Assert.That(await profilePage.IsOnProfilePageAsync(), Is.True, "Should be on profile page");
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// Enters valid registration details for a new user account.
    /// </summary>
    /// <remarks>
    /// Clears existing test users via Test Control API, generates a unique test user
    /// based on the test ID, stores user details in object store, and fills the
    /// registration form with valid credentials.
    /// </remarks>
    [When("I enter valid registration details")]
    protected async Task WhenIEnterValidRegistrationDetails()
    {
        var registerPage = GetOrCreateRegisterPage();

        // First, clear existing test users via Test Control API
        await testControlClient.DeleteUsersAsync();

        var user = CreateTestUserCredentials("testuser");
        _objectStore.Add("Registration Details", user);

        await registerPage.EnterRegistrationDetailsAsync(user.Email, user.Username, user.Password, user.Password);
    }

    /// <summary>
    /// Submits the registration form and waits for response.
    /// </summary>
    /// <remarks>
    /// Clicks the register button and expects an API call to be made.
    /// Use WhenISubmitTheRegistrationFormForValidation for client-side validation tests.
    /// </remarks>
    [When("I submit the registration form")]
    protected async Task WhenISubmitTheRegistrationForm()
    {
        var registerPage = GetOrCreateRegisterPage();
        await registerPage.ClickRegisterButtonAsync();
    }

    /// <summary>
    /// Submits the registration form for client-side validation testing.
    /// </summary>
    /// <remarks>
    /// Clicks the register button without expecting an API call. Used for testing
    /// client-side validation errors that prevent form submission.
    /// </remarks>
    [When("I submit the registration form \\(for validation\\)")]
    protected async Task WhenISubmitTheRegistrationFormForValidation()
    {
        var registerPage = GetOrCreateRegisterPage();
        await registerPage.ClickRegisterButtonForValidation();
    }

    /// <summary>
    /// Enters user credentials from a DataTable into the login form.
    /// </summary>
    /// <param name="credentialsData">DataTable containing Email and Password keys.</param>
    /// <remarks>
    /// This is the primary method used in Gherkin scenarios. The overload without
    /// DataTable is used internally for programmatic login.
    /// </remarks>
    [When("I enter my credentials")]
    protected async Task WhenIEnterMyCredentials(DataTable credentialsData)
    {
        var loginPage = GetOrCreateLoginPage();
        var email = credentialsData.GetKeyValue("Email");
        var password = credentialsData.GetKeyValue("Password");

        await loginPage.EnterCredentialsAsync(email, password);
    }

    /// <summary>
    /// Enters user credentials programmatically (overload for internal use).
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="password">The user's password.</param>
    /// <remarks>
    /// This overload is used by other step methods that need to enter credentials
    /// programmatically without a DataTable from Gherkin.
    /// </remarks>
    protected async Task WhenIEnterMyCredentials(string email, string password)
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.EnterCredentialsAsync(email, password);
    }

    /// <summary>
    /// Enters invalid credentials from a DataTable into the login form.
    /// </summary>
    /// <param name="credentialsData">DataTable containing Email and Password keys.</param>
    /// <remarks>
    /// Functionally identical to WhenIEnterMyCredentials but semantically distinct
    /// for test readability. Used in negative test scenarios.
    /// </remarks>
    [When("I enter invalid credentials")]
    protected async Task WhenIEnterInvalidCredentials(DataTable credentialsData)
    {
        var loginPage = GetOrCreateLoginPage();
        var email = credentialsData.GetKeyValue("Email");
        var password = credentialsData.GetKeyValue("Password");

        await loginPage.EnterCredentialsAsync(email, password);
    }

    /// <summary>
    /// Enters hardcoded invalid credentials (overload for parameterless scenarios).
    /// </summary>
    /// <remarks>
    /// Uses default invalid credentials when no DataTable is provided.
    /// Useful for simple negative test cases.
    /// </remarks>
    protected async Task WhenIEnterInvalidCredentials()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.EnterCredentialsAsync("invalid@example.com", "WrongPassword123!");
    }

    /// <summary>
    /// Enters only a username without a password.
    /// </summary>
    /// <remarks>
    /// Used to test validation behavior when the password field is left empty.
    /// Fills only the username/email field.
    /// </remarks>
    [When("I enter only a username")]
    protected async Task WhenIEnterOnlyUsername()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.EnterUsernameOnlyAsync("Only Username");
    }

    /// <summary>
    /// Leaves the password field empty during login.
    /// </summary>
    /// <remarks>
    /// This is a no-op step as leaving password empty is the default state.
    /// The actual validation testing is handled by WhenIEnterOnlyUsername.
    /// TODO: Consider removing this redundant step method.
    /// </remarks>
    [When("I leave the password field empty")]
    protected async Task WhenILeaveThePasswordFieldEmpty()
    {
        // This is handled by WhenIEnterOnlyUsername
        await Task.CompletedTask;
    }

    /// <summary>
    /// Clicks the login button for client-side validation testing.
    /// </summary>
    /// <remarks>
    /// Clicks the login button without expecting an API call. Used for testing
    /// client-side validation that prevents form submission.
    /// </remarks>
    [When("I click the login button \\(for validation\\)")]
    protected async Task WhenIClickTheLoginButtonForValidation()
    {
        var loginPage = GetOrCreateLoginPage();
        await loginPage.ClickLoginButtonForValidation();
    }

    /// <summary>
    /// Navigates to the user's profile page and verifies it loads.
    /// </summary>
    /// <remarks>
    /// Creates or retrieves the ProfilePage, navigates to it, and asserts
    /// the page loaded successfully before proceeding.
    /// </remarks>
    [When("I navigate to my profile page")]
    protected async Task WhenINavigateToMyProfilePage()
    {
        var profilePage = GetOrCreateProfilePage();
        await profilePage.NavigateAsync();
        Assert.That(await profilePage.IsOnProfilePageAsync(), Is.True, "Should be on profile page");
    }

    /// <summary>
    /// Clicks the logout button and waits for logout to complete.
    /// </summary>
    /// <remarks>
    /// Waits for logout button to be ready, clicks it, and waits for the home page
    /// to load (with extended timeout of 12 seconds) to ensure logout completes.
    /// </remarks>
    [When("I click the logout button")]
    protected async Task WhenIClickTheLogoutButton()
    {
        var profilePage = GetOrCreateProfilePage();

        await profilePage.WaitForLogoutButtonReadyAsync();

        await profilePage.ClickLogoutAsync();
        // Wait for home page to be ready after logout
        var homePage = new HomePage(Page);
        await homePage.WaitForPageReadyAsync(12000);
    }

    /// <summary>
    /// Enters registration details with a weak password (parameterless version).
    /// </summary>
    /// <remarks>
    /// Clears test users, generates unique user details, but uses a hardcoded
    /// weak password ("weak") to trigger password strength validation.
    /// </remarks>
    [When("I enter registration details with a weak password")]
    protected async Task WhenIEnterRegistrationDetailsWithAWeakPassword()
    {
        var registerPage = GetOrCreateRegisterPage();

        // First, clear existing test users via Test Control API
        await testControlClient.DeleteUsersAsync();

        var user = CreateTestUserCredentials("testuser");
        _objectStore.Add("Registration Details", user);

        // Use a weak password (too short, no special characters, etc.)
        var weakPassword = "weak";
        await registerPage.EnterWeakPasswordDetailsAsync(user.Email, user.Username, weakPassword);
    }

    /// <summary>
    /// Enters registration details with a weak password from DataTable.
    /// </summary>
    /// <param name="registrationData">DataTable containing Email and Password keys.</param>
    /// <remarks>
    /// DataTable-based overload for parameterized weak password testing.
    /// Uses the password from the table rather than a hardcoded value.
    /// </remarks>
    protected async Task WhenIEnterRegistrationDetailsWithAWeakPassword(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetKeyValue("Email");
        var password = registrationData.GetKeyValue("Password");

        await registerPage.EnterWeakPasswordDetailsAsync(email, "newuser", password);
    }

    /// <summary>
    /// Enters registration details with mismatched password and confirm password fields.
    /// </summary>
    /// <remarks>
    /// Clears test users, generates unique user details, and fills the form with
    /// deliberately mismatched password and confirm password values to trigger validation.
    /// </remarks>
    [When("I enter registration details with mismatched passwords")]
    protected async Task WhenIEnterRegistrationDetailsWithMismatchedPasswords()
    {
        var registerPage = GetOrCreateRegisterPage();

        // First, clear existing test users via Test Control API
        await testControlClient.DeleteUsersAsync();

        var user = CreateTestUserCredentials("testuser");
        _objectStore.Add("Registration Details", user);

        // Use mismatched passwords
        var password = user.Password;
        var confirmPassword = "DifferentPassword123!";
        await registerPage.EnterMismatchedPasswordDetailsAsync(user.Email, user.Username, password, confirmPassword);
    }

    /// <summary>
    /// Enters registration details with mismatched passwords from DataTable.
    /// </summary>
    /// <param name="registrationData">DataTable with Email, Password, and Confirm Password keys.</param>
    /// <remarks>
    /// DataTable-based overload for parameterized password mismatch testing.
    /// </remarks>
    protected async Task WhenIEnterRegistrationDetailsWithMismatchedPasswords(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetKeyValue("Email");
        var password = registrationData.GetKeyValue("Password");
        var confirmPassword = registrationData.GetKeyValue("Confirm Password");

        await registerPage.EnterMismatchedPasswordDetailsAsync(email, "newuser", password, confirmPassword);
    }

    /// <summary>
    /// Enters generic registration details from a DataTable.
    /// </summary>
    /// <param name="registrationData">DataTable with Email, Password, and Confirm Password keys.</param>
    /// <remarks>
    /// Generic registration data entry method. Uses hardcoded username "existinguser".
    /// TODO: Consider making username configurable via DataTable.
    /// </remarks>
    [When("I enter registration details")]
    protected async Task WhenIEnterRegistrationDetails(DataTable registrationData)
    {
        var registerPage = GetOrCreateRegisterPage();
        var email = registrationData.GetKeyValue("Email");
        var password = registrationData.GetKeyValue("Password");
        var confirmPassword = registrationData.GetKeyValue("Confirm Password");

        await registerPage.EnterRegistrationDetailsAsync(email, "existinguser", password, confirmPassword);
    }

    /// <summary>
    /// Attempts to register with an email that already exists in the system.
    /// </summary>
    /// <remarks>
    /// Retrieves existing user credentials from object store and attempts to register
    /// a new account using the same email but different username. Used to test
    /// duplicate email validation.
    /// </remarks>
    [When("I enter registration details with the existing email")]
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
    /// Navigates to the login page via normal navigation flow.
    /// </summary>
    /// <remarks>
    /// Simulates user navigation through in-app links/buttons. Distinct from
    /// WhenITryToNavigateDirectlyToTheLoginPageExpectingFailure which tests
    /// direct URL access.
    /// TODO: Implement actual in-app navigation instead of direct NavigateAsync.
    /// </remarks>
    [When("I try to navigate to the login page")]
    protected async Task WhenITryToNavigateToTheLoginPage()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync();
    }

    /// <summary>
    /// Attempts to navigate directly to login page URL, expecting failure.
    /// </summary>
    /// <remarks>
    /// Tests that authenticated users cannot access the login page directly.
    /// Uses NavigateAsync(false) to suppress error expectations.
    /// </remarks>
    [When("I try to navigate directly to the login page")]
    protected async Task WhenITryToNavigateDirectlyToTheLoginPageExpectingFailure()
    {
        var loginPage = new LoginPage(Page);
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
    protected async Task WhenITryToNavigateDirectlyToAProtectedPageLike(string page)
    {
        // Navigate directly - should redirect to login page for anonymous users
        await Page.GotoAsync(page);

        // Wait for redirect to complete by waiting for login page to be ready
        var loginPage = GetOrCreateLoginPage();
        await loginPage.WaitForPageReadyAsync();
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that registration was successful and displays correct user information.
    /// </summary>
    /// <remarks>
    /// Waits for success message to appear (up to 10 seconds), retrieves registered
    /// user details from object store, and validates that displayed email and username
    /// match the registered values.
    /// </remarks>
    [Then("my registration request should be acknowledged")]
    protected async Task ThenMyRegistrationRequestShouldBeAcknowledged()
    {
        var registerPage = GetOrCreateRegisterPage();
        await registerPage.SuccessMessage.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var user = The<TestUserCredentials>("Registration Details");

        var emailDisplayText = await registerPage.EmailDisplay.InnerTextAsync();
        var usernameDisplayText = await registerPage.UsernameDisplay.InnerTextAsync();

        Assert.That(emailDisplayText, Is.EqualTo(user.Email), "Displayed email should match registered email");
        Assert.That(usernameDisplayText, Is.EqualTo(user.Username), "Displayed username should match registered username");
    }

    /// <summary>
    /// Verifies that user login was successful.
    /// </summary>
    /// <remarks>
    /// Delegates to ThenIShouldSeeMyUsernameInTheHeader for actual verification.
    /// TODO: Consider consolidating or enhancing with additional login state checks.
    /// </remarks>
    [Then("I should be successfully logged in")]
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
    /// Verifies that the logged-in user's username is visible in the site header.
    /// </summary>
    /// <remarks>
    /// Retrieves test user credentials from object store and confirms the username
    /// displayed in the header matches the expected value.
    /// </remarks>
    [Then("I should see my username in the header")]
    protected async Task ThenIShouldSeeMyUsernameInTheHeader()
    {
        var basePage = new BasePage(Page);
        var testuser = It<Generated.TestUserCredentials>();
        var usernameInHeader = await basePage.SiteHeader.LoginState.GetUsernameAsync();
        Assert.That(usernameInHeader, Is.EqualTo(testuser.Username), "Username should be visible in the header");
    }

    /// <summary>
    /// Verifies that an error message containing specific text is displayed.
    /// </summary>
    /// <param name="errorMessage">The expected error message text (or substring).</param>
    /// <remarks>
    /// Works for both login and registration pages. Checks object store to determine
    /// which page is active and validates the error message accordingly.
    /// </remarks>
    [Then("I should see an error message containing (.+)")]
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
    /// Verifies that the user remains on the login page (not redirected).
    /// </summary>
    /// <remarks>
    /// Used in negative test scenarios where login should fail and user should
    /// stay on the login page rather than being redirected.
    /// </remarks>
    [Then("I should remain on the login page")]
    protected async Task ThenIShouldRemainOnTheLoginPage()
    {
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should remain on login page");
    }

    /// <summary>
    /// Verifies that a validation error is displayed on the login form.
    /// </summary>
    /// <remarks>
    /// Checks for both HTML5 validation (browser native) and custom error displays.
    /// Prioritizes HTML5 validation check for required password field.
    /// </remarks>
    [Then("I should see a validation error")]
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
    /// Verifies that user account information is correctly displayed on the profile page.
    /// </summary>
    /// <remarks>
    /// Retrieves test user credentials from object store and validates that the
    /// profile page displays the correct email and username.
    /// </remarks>
    [Then("I should see my account information")]
    protected async Task ThenIShouldSeeMyAccountInformation()
    {
        var profilePage = GetOrCreateProfilePage();
        var testuser = It<Generated.TestUserCredentials>();

        Assert.That(await profilePage.HasAccountInformationAsync(testuser.Email, testuser.Username), Is.True,
            "Should display correct account information");
    }

    /// <summary>
    /// Verifies that current workspace information is displayed on the profile page.
    /// </summary>
    /// <remarks>
    /// Checks that workspace-related information section is present and visible
    /// on the user's profile page.
    /// </remarks>
    [Then("I should see my current workspace information")]
    protected async Task ThenIShouldSeeMyCurrentWorkspaceInformation()
    {
        var profilePage = GetOrCreateProfilePage();
        Assert.That(await profilePage.HasWorkspaceInformationAsync(), Is.True,
            "Should display workspace information");
    }

    /// <summary>
    /// Verifies that the user is logged out (not authenticated).
    /// </summary>
    /// <remarks>
    /// Checks the site header login state to confirm the user is no longer
    /// authenticated after logout.
    /// </remarks>
    [Then("I should be logged out")]
    protected async Task ThenIShouldBeLoggedOut()
    {
        var basePage = new BasePage(Page);
        Assert.That(await basePage.SiteHeader.LoginState.IsLoggedInAsync(), Is.False,
            "User should be logged out");
    }

    /// <summary>
    /// Verifies that the user is redirected to the home page.
    /// </summary>
    /// <remarks>
    /// Waits for home page to be ready and confirms the brochure section
    /// (characteristic element of home page) is visible.
    /// </remarks>
    [Then("I should be redirected to the home page")]
    protected async Task ThenIShouldBeRedirectedToTheHomePage()
    {
        var homePage = new HomePage(Page);
        await homePage.WaitForPageReadyAsync();
        Assert.That(await homePage.BrochureSection.IsVisibleAsync(), Is.True, "Should be on home page");
    }

    /// <summary>
    /// Verifies that the login/sign-in option is visible in the navigation menu.
    /// </summary>
    /// <remarks>
    /// Opens the navigation menu and confirms the sign-in menu item is visible,
    /// indicating the user is not currently logged in.
    /// </remarks>
    [Then("I should see the login option in the navigation")]
    protected async Task ThenIShouldSeeTheLoginOptionInTheNavigation()
    {
        var basePage = new BasePage(Page);
        await basePage.SiteHeader.LoginState.OpenMenuAsync();
        Assert.That(await basePage.SiteHeader.LoginState.SignInMenuItem.IsVisibleAsync(), Is.True,
            "Signin option should be visible in navigation");
    }

    /// <summary>
    /// Verifies that no personal information (username) is displayed in the header.
    /// </summary>
    /// <remarks>
    /// Confirms the user is not logged in by checking that no username is displayed
    /// in the site header. Used after logout or in anonymous user scenarios.
    /// </remarks>
    [Then("I should not see any personal information")]
    protected async Task ThenIShouldNotSeeAnyPersonalInformation()
    {
        var basePage = new BasePage(Page);
        var usernameInHeader = await basePage.SiteHeader.LoginState.GetUsernameAsync();
        Assert.That(usernameInHeader, Is.Null, "Username should not be visible in the header");

    }

    /// <summary>
    /// Verifies that the user remains on the registration page (not redirected).
    /// </summary>
    /// <remarks>
    /// Used in negative test scenarios where registration should fail and user
    /// should stay on the registration page.
    /// </remarks>
    [Then("I should remain on the registration page")]
    protected async Task ThenIShouldRemainOnTheRegistrationPage()
    {
        var registerPage = GetOrCreateRegisterPage();
        Assert.That(await registerPage.IsRegisterFormVisibleAsync(), Is.True, "Should remain on registration page");
    }

    /// <summary>
    /// Verifies that the login form is not visible on the current page.
    /// </summary>
    /// <remarks>
    /// Used to confirm that authenticated users cannot see the login form,
    /// or that redirect away from login page was successful.
    /// </remarks>
    [Then("I should not see the login form")]
    protected async Task ThenIShouldNotSeeTheLoginForm()
    {
        var loginPage = GetOrCreateLoginPage();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.False, "Should not see login form");
    }

    /// <summary>
    /// Verifies that the user is redirected to the login page.
    /// </summary>
    /// <remarks>
    /// Common assertion for protected page access scenarios where unauthenticated
    /// users should be redirected to login.
    /// </remarks>
    [Then("I should be redirected to the login page")]
    protected async Task ThenIShouldBeRedirectedToTheLoginPage()
    {
        var loginPage = GetOrCreateLoginPage();
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
    protected async Task ThenIShouldSeeAMessageIndicatingINeedToLogIn()
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
    protected async Task ThenAfterLoggingInIShouldBeRedirectedToTheOriginallyRequestedPage()
    {
        // TODO: Verify redirect after login works correctly
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that registration did not succeed.
    /// </summary>
    /// <remarks>
    /// Confirms user remains on registration page and no success message is shown.
    /// Used in negative registration test scenarios.
    /// </remarks>
    [Then("I should not be registered")]
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
    /// Verifies that the user is redirected to their profile page.
    /// </summary>
    /// <remarks>
    /// Waits for profile page to be ready and confirms the page loaded correctly.
    /// Used after successful login or profile navigation actions.
    /// </remarks>
    [Then("I should be redirected to my profile page")]
    protected async Task ThenIShouldBeRedirectedToMyProfilePage()
    {
        var profilePage = GetOrCreateProfilePage();
        await profilePage.WaitForPageReadyAsync();
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
