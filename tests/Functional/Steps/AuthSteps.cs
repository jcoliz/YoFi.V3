using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for authentication operations including login, logout, and account management.
/// </summary>
/// <param name="_context">The test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles basic authentication flows extracted from Common steps:
/// - Account setup and login
/// - Login page navigation
/// - Credential management
///
/// Composes NavigationSteps for page verification functionality.
/// </remarks>
public class AuthSteps(ITestContext _context)
{
    private readonly NavigationSteps _navigationSteps = new(_context);
    private readonly RegistrationSteps _registrationSteps = new(_context);

    #region Steps: GIVEN

    /// <summary>
    /// Verifies that no user is currently logged into the application.
    /// </summary>
    /// <remarks>
    /// Currently assumes a clean state. Future implementation may include
    /// explicit logout if a user session is detected.
    /// </remarks>
    //[Given("I am not logged in")]
    public async Task GivenIAmNotLoggedIn()
    {
        // TODO: Implement logout if already logged in
        // For now, assume we start from a clean state
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a test user account if one doesn't already exist.
    /// </summary>
    /// <remarks>
    /// Checks if credentials for "I" already exist in the context before creating.
    /// If they exist, does nothing. Otherwise, creates user credentials and registers
    /// them on the server. The created user credentials are automatically tracked for cleanup.
    /// </remarks>
    //[Given("I have an existing account")]
    public async Task GivenIHaveAnExistingAccount()
    {
        // Check if "I" already has credentials
        try
        {
            _ = _context.GetUserCredentials("I");

            // Credentials already exist, nothing to do
        }
        catch (KeyNotFoundException)
        {
            // Credentials don't exist yet, create them
            await _context.CreateTestUserCredentialsOnServer("I");  // Auto-tracked
        }
    }

    /// <summary>
    /// Navigates to the login page and verifies it loads correctly.
    /// </summary>
    /// <remarks>
    /// Creates and stores the LoginPage object in the object store for reuse.
    /// </remarks>
    //[Given("I am on the login page")]
    public async Task GivenIAmOnTheLoginPage()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.NavigateAsync();
        var isOnLoginPage = await loginPage.IsOnLoginPageAsync();
        Assert.That(isOnLoginPage, Is.True, "Should be on login page");
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
    //[Given("I am logged in")]
    public async Task GivenIAmLoggedIn()
    {
        await GivenIHaveAnExistingAccount();
        await GivenIAmOnTheLoginPage();
        await WhenILoginWithMyCredentials();
        await _navigationSteps.ThenIShouldSeeTheHomePage();
    }

    /// <summary>
    /// Logs in as the specified user.
    /// </summary>
    /// <param name="shortName">The username (friendly name, defaults to "I").</param>
    /// <remarks>
    /// Navigates to login page, performs login, waits for redirect, and stores
    /// the full username in object store for future reference.
    /// Requires user to have been created beforehand.
    /// </remarks>
    //[Given("I am logged in as {username}")]
    //[Given("I am logged into my existing account")]
    public async Task GivenIAmLoggedInAs(string shortName = "I")
    {
        var cred = _context.GetUserCredentials(shortName);

        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.NavigateAsync();

        await loginPage.LoginAsync(cred.Username, cred.Password);

        // Wait for redirect after successful login
        await _context.Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });

        // Store FULL username for future reference
        _context.ObjectStore.Add(ObjectStoreKeys.LoggedInAs, cred.Username);
    }

    /// <summary>
    /// Creates multiple test users for multi-user test scenarios.
    /// </summary>
    /// <param name="usersTable">DataTable containing user names (single column named "Username").</param>
    /// <remarks>
    /// Creates all users in bulk via Test Control API. Credentials are automatically
    /// tracked for cleanup. This is a test setup operation used in multi-user scenarios
    /// like workspace collaboration tests.
    /// </remarks>
    //[Given("these users exist")]
    public async Task GivenTheseUsersExist(DataTable usersTable)
    {
        var friendlyNames = usersTable.ToSingleColumnList().ToList();

        // Generate credentials for all users (auto-tracked)
        var credentialsList = friendlyNames.Select(name => _context.CreateTestUserCredentials(name)).ToList();

        // Create all users in bulk
        var created = await _context.TestControlClient.CreateUsersV2Async(credentialsList);

        // Credentials are already tracked by CreateTestUserCredentials
        // Verify they were created successfully
        Assert.That(created.Count, Is.EqualTo(friendlyNames.Count),
            $"Expected to create {friendlyNames.Count} users but only {created.Count} were created");
    }

    /// <summary>
    /// Registers a new user and logs them in (for user registration flow tests).
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <remarks>
    /// Composes RegistrationSteps for the complete registration workflow, then performs login.
    /// New users automatically get a personalized workspace named after their username.
    /// This is useful for testing the new user experience.
    /// </remarks>
    //[When("a new user {username} registers and logs in")]
    public async Task WhenANewUserRegistersAndLogsIn(string shortName)
    {
        // Compose: Perform complete registration (navigate, enter, submit, continue)
        await _registrationSteps.WhenIRegisterANewUser(shortName);

        // Retrieve the credentials that were created during registration
        var credentials = _context.GetUserCredentials(shortName);

        // New users get a workspace containing their name
        // NOTE: This "works" because workspace lookups are substring lookups
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, credentials.Username);

        // Perform login with the newly registered credentials
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.LoginAsync(credentials.Username, credentials.Password);

        // Store logged in user for future reference
        _context.ObjectStore.Add(ObjectStoreKeys.LoggedInAs, credentials.Username);
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// Enters hardcoded invalid credentials for testing failed login scenarios.
    /// </summary>
    /// <remarks>
    /// Uses default invalid credentials (invalid@example.com / WrongPassword123!).
    /// Useful for simple negative test cases.
    /// </remarks>
    [When("I enter invalid credentials")]
    public async Task WhenIEnterInvalidCredentials()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.EnterCredentialsAsync("invalid@example.com", "WrongPassword123!");
    }

    /// <summary>
    /// Clicks the login button to submit the login form.
    /// </summary>
    /// <remarks>
    /// Used after credentials have been entered into the form fields.
    /// Assumes credentials have already been entered into the form fields.
    /// </remarks>
    //[When("I click the login button")]
    public async Task WhenIClickTheLoginButton()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.ClickLoginButtonAsync();
    }

    /// <summary>
    /// Performs a complete login operation with test user credentials.
    /// </summary>
    /// <remarks>
    /// Retrieves test user credentials from the context and performs the full
    /// login action (entering credentials and submitting the form in one operation).
    /// This is a helper method used by composite Given steps.
    /// </remarks>
    public async Task WhenILoginWithMyCredentials()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        var testUser = _context.GetUserCredentials("I");
        await loginPage.LoginAsync(testUser.Username, testUser.Password);
    }

    /// <summary>
    /// Clicks the logout button and waits for logout to complete.
    /// </summary>
    /// <remarks>
    /// Waits for logout button to be ready, clicks it, and waits for the home page
    /// to load (with extended timeout of 12 seconds) to ensure logout completes.
    /// </remarks>
    //[When("I click the logout button")]
    public async Task WhenIClickTheLogoutButton()
    {
        var profilePage = _context.GetOrCreatePage<ProfilePage>();

        await profilePage.WaitForLogoutButtonReadyAsync();

        await profilePage.ClickLogoutAsync();
        // Wait for home page to be ready after logout
        var homePage = _context.GetOrCreatePage<HomePage>();
        await homePage.WaitForPageReadyAsync(12000);
    }

    /// <summary>
    /// Enters only a username without a password.
    /// </summary>
    /// <remarks>
    /// Used to test validation behavior when the password field is left empty.
    /// Fills only the username/email field.
    /// </remarks>
    //[When("I enter only a username")]
    public async Task WhenIEnterOnlyUsername()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.EnterUsernameOnlyAsync("Only Username");
    }

    /// <summary>
    /// Leaves the password field empty during login.
    /// </summary>
    /// <remarks>
    /// This is a no-op step as leaving password empty is the default state.
    /// The actual validation testing is handled by WhenIEnterOnlyUsername.
    /// </remarks>
    //[When("I leave the password field empty")]
    public async Task WhenILeaveThePasswordFieldEmpty()
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
    //[When("I click the login button (for validation)")]
    public async Task WhenIClickTheLoginButtonForValidation()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        await loginPage.ClickLoginButtonForValidation();
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that user login was successful by checking username in header.
    /// </summary>
    /// <remarks>
    /// Retrieves test user credentials from context and confirms the username
    /// displayed in the header matches the expected value. This serves as verification
    /// that the user is successfully logged in.
    /// </remarks>
    [Then("I should be successfully logged in")]
    [Then("I should see my username in the header")]
    public async Task ThenIShouldSeeMyUsernameInTheHeader()
    {
        var basePage = _context.GetOrCreatePage<BasePage>();
        var testUser = _context.GetUserCredentials("I");
        var usernameInHeader = await basePage.SiteHeader.LoginState.GetUsernameAsync();
        Assert.That(usernameInHeader, Is.EqualTo(testUser.Username), "Username should be visible in the header");
    }

    /// <summary>
    /// Verifies that an error message containing specific text is displayed.
    /// </summary>
    /// <param name="errorMessage">The expected error message text (or substring).</param>
    /// <remarks>
    /// Works for both login and registration pages. Checks object store to determine
    /// which page is active and validates the error message accordingly.
    /// </remarks>
    [Then("I should see an error message containing {errorMessage}")]
    [Then("I should see an error message {errorMessage}")]
    public async Task ThenIShouldSeeAnErrorMessage(string errorMessage)
    {
        // This method works for both login and registration pages

        // The way to tell which page we're on is to ask the object store.
        if (_context.ObjectStore.Contains<RegisterPage>())
        {
            var registerPage = _context.ObjectStore.Get<RegisterPage>();
            if (await registerPage.IsRegisterFormVisibleAsync())
            {
                Assert.That(await registerPage.HasErrorMessageAsync(errorMessage), Is.True,
                    $"Should display error message containing: {errorMessage}");
                return;
            }
        }

        if (_context.ObjectStore.Contains<LoginPage>())
        {
            var loginPage = _context.ObjectStore.Get<LoginPage>();
            if (await loginPage.IsOnLoginPageAsync())
            {
                Assert.That(await loginPage.HasErrorMessageAsync(errorMessage), Is.True,
                    $"Should display error message containing: {errorMessage}");
                return;
            }
        }

        throw new InvalidOperationException("No page object for login or registration found in object store");
    }

    /// <summary>
    /// Verifies that the user remains on the login page (not redirected).
    /// </summary>
    /// <remarks>
    /// Used in negative test scenarios where login should fail and user should
    /// stay on the login page rather than being redirected.
    /// </remarks>
    [Then("I should remain on the login page")]
    public async Task ThenIShouldRemainOnTheLoginPage()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should remain on login page");
    }

    /// <summary>
    /// Verifies that the user is logged out (not authenticated).
    /// </summary>
    /// <remarks>
    /// Checks the site header login state to confirm the user is no longer
    /// authenticated after logout.
    /// </remarks>
    //[Then("I should be logged out")]
    public async Task ThenIShouldBeLoggedOut()
    {
        var basePage = _context.GetOrCreatePage<BasePage>();
        Assert.That(await basePage.SiteHeader.LoginState.IsLoggedInAsync(), Is.False,
            "User should be logged out");
    }

    /// <summary>
    /// Verifies that the login/sign-in option is visible in the navigation menu.
    /// </summary>
    /// <remarks>
    /// Opens the navigation menu and confirms the sign-in menu item is visible,
    /// indicating the user is not currently logged in.
    /// </remarks>
    //[Then("I should see the login option in the navigation")]
    public async Task ThenIShouldSeeTheLoginOptionInTheNavigation()
    {
        var basePage = _context.GetOrCreatePage<BasePage>();
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
    //[Then("I should not see any personal information")]
    public async Task ThenIShouldNotSeeAnyPersonalInformation()
    {
        var basePage = _context.GetOrCreatePage<BasePage>();
        var usernameInHeader = await basePage.SiteHeader.LoginState.GetUsernameAsync();
        Assert.That(usernameInHeader, Is.Null, "Username should not be visible in the header");
    }

    /// <summary>
    /// Verifies that user account information is correctly displayed on the profile page.
    /// </summary>
    /// <remarks>
    /// Retrieves test user credentials from context and validates that the
    /// profile page displays the correct email and username.
    /// </remarks>
    //[Then("I should see my account information")]
    public async Task ThenIShouldSeeMyAccountInformation()
    {
        var profilePage = _context.GetOrCreatePage<ProfilePage>();
        var testUser = _context.GetUserCredentials("I");
        var hasAccountInformation = await profilePage.HasAccountInformationAsync(testUser.Email, testUser.Username);

        Assert.That(hasAccountInformation, Is.True,
            "Should display correct account information");
    }

    /// <summary>
    /// Verifies that the login form is not visible on the current page.
    /// </summary>
    /// <remarks>
    /// Used to confirm that authenticated users cannot see the login form,
    /// or that redirect away from login page was successful.
    /// </remarks>
    //[Then("I should not see the login form")]
    public async Task ThenIShouldNotSeeTheLoginForm()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();
        var isOnLoginPage = await loginPage.IsOnLoginPageAsync();
        Assert.That(isOnLoginPage, Is.False, "Should not see login form");
    }

    /// <summary>
    /// Verifies that a validation error is displayed on the login form.
    /// </summary>
    /// <remarks>
    /// Checks for both HTML5 validation (browser native) and custom error displays.
    /// Prioritizes HTML5 validation check for required password field.
    /// </remarks>
    //[Then("I should see a validation error")]
    public async Task ThenIShouldSeeAValidationError()
    {
        var loginPage = _context.GetOrCreatePage<LoginPage>();

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

    #endregion
}
