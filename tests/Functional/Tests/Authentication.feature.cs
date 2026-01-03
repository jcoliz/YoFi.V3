using YoFi.V3.Tests.Functional.Steps;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// User Authentication
/// As a user of YoFi
/// I want to register, login, and manage my account
/// So that I can securely access my financial data
/// </summary>
public class UserAuthenticationTests : FunctionalTestBase
{
    protected NavigationSteps NavigationSteps => _navigationSteps ??= new(this);
    private NavigationSteps? _navigationSteps;

    protected AuthSteps AuthSteps => _authSteps ??= new(this);
    private AuthSteps? _authSteps;

    protected RegistrationSteps RegistrationSteps => _registrationSteps ??= new(this);
    private RegistrationSteps? _registrationSteps;

    [SetUp]
    public async Task Background()
    {
        // Given the application is running
        await NavigationSteps.GivenLaunchedSite();

        // And I am not logged in
        await AuthSteps.GivenIAmNotLoggedIn();
    }

    #region Rule: User Registration
    // Users can create new accounts with valid credentials

    /// <summary>
    /// User registers for a new account
    /// </summary>
    [Test]
    public async Task UserRegistersForANewAccount()
    {
        // Given I am on the registration page
        await RegistrationSteps.GivenIAmOnTheRegistrationPage();

        // When I enter valid registration details:
        await RegistrationSteps.WhenIEnterValidRegistrationDetails();

        // And I submit the registration form
        await RegistrationSteps.WhenISubmitTheRegistrationForm();

        // Then My registration request should be acknowledged
        await RegistrationSteps.ThenMyRegistrationRequestShouldBeAcknowledged();
    }

    /// <summary>
    /// User registration fails with weak password
    /// </summary>
    [Test]
    public async Task UserRegistrationFailsWithWeakPassword()
    {
        // Given I am on the registration page
        await RegistrationSteps.GivenIAmOnTheRegistrationPage();

        // When I enter registration details with a weak password
        await RegistrationSteps.WhenIEnterRegistrationDetailsWithAWeakPassword();

        // And I submit the registration form
        await RegistrationSteps.WhenISubmitTheRegistrationForm();

        // Then I should see an error message containing "Passwords must be"
        await AuthSteps.ThenIShouldSeeAnErrorMessage("Passwords must be");

        // And I should not be registered
        await RegistrationSteps.ThenIShouldNotBeRegistered();
    }

    /// <summary>
    /// User registration fails with mismatched passwords
    /// </summary>
    [Test]
    public async Task UserRegistrationFailsWithMismatchedPasswords()
    {
        // Given I am on the registration page
        await RegistrationSteps.GivenIAmOnTheRegistrationPage();

        // When I enter registration details with mismatched passwords
        await RegistrationSteps.WhenIEnterRegistrationDetailsWithMismatchedPasswords();

        // And I submit the registration form (for validation)
        await RegistrationSteps.WhenISubmitTheRegistrationFormForValidation();

        // Then I should see an error message containing "Passwords do not match"
        await AuthSteps.ThenIShouldSeeAnErrorMessage("Passwords do not match");

        // And I should not be registered
        await RegistrationSteps.ThenIShouldNotBeRegistered();
    }

    /// <summary>
    /// User registration fails with existing email
    /// </summary>
    [Test]
    public async Task UserRegistrationFailsWithExistingEmail()
    {
        // Given I have an existing account
        await AuthSteps.GivenIHaveAnExistingAccount();

        // And I am on the registration page
        await RegistrationSteps.GivenIAmOnTheRegistrationPage();

        // When I enter registration details with the existing email
        await RegistrationSteps.WhenIEnterRegistrationDetailsWithTheExistingEmail();

        // And I submit the registration form
        await RegistrationSteps.WhenISubmitTheRegistrationForm();

        // Then I should see an error message containing "is already taken"
        await AuthSteps.ThenIShouldSeeAnErrorMessage("is already taken");

        // And I should remain on the registration page
        await RegistrationSteps.ThenIShouldRemainOnTheRegistrationPage();

        // And I should not be registered
        await RegistrationSteps.ThenIShouldNotBeRegistered();
    }

    #endregion

    #region Rule: User Login and Logout
    // Users can authenticate and end their sessions

    /// <summary>
    /// User logs into an existing account
    /// </summary>
    [Test]
    public async Task UserLogsIntoAnExistingAccount()
    {
        // Given I have an existing account
        await AuthSteps.GivenIHaveAnExistingAccount();

        // And I am on the login page
        await AuthSteps.GivenIAmOnTheLoginPage();

        // When I login with my credentials
        await AuthSteps.WhenILoginWithMyCredentials();

        // Then I should see the home page
        await NavigationSteps.ThenIShouldSeeTheHomePage();

        // And I should be successfully logged in
        await AuthSteps.ThenIShouldSeeMyUsernameInTheHeader();
    }

    /// <summary>
    /// User login fails with invalid credentials
    /// </summary>
    [Test]
    public async Task UserLoginFailsWithInvalidCredentials()
    {
        // Given I am on the login page
        await AuthSteps.GivenIAmOnTheLoginPage();

        // When I enter invalid credentials
        await AuthSteps.WhenIEnterInvalidCredentials();

        // And I click the login button
        await AuthSteps.WhenIClickTheLoginButton();

        // Then I should see an error message "Invalid credentials"
        await AuthSteps.ThenIShouldSeeAnErrorMessage("Invalid credentials");

        // And I should remain on the login page
        await AuthSteps.ThenIShouldRemainOnTheLoginPage();
    }

    /// <summary>
    /// User login fails with missing password
    /// </summary>
    [Test]
    public async Task UserLoginFailsWithMissingPassword()
    {
        // Given I am on the login page
        await AuthSteps.GivenIAmOnTheLoginPage();

        // When I enter only a username
        await AuthSteps.WhenIEnterOnlyUsername();

        // And I leave the password field empty
        await AuthSteps.WhenILeaveThePasswordFieldEmpty();

        // And I click the login button (for validation)
        await AuthSteps.WhenIClickTheLoginButtonForValidation();

        // Then I should see a validation error
        await AuthSteps.ThenIShouldSeeAValidationError();

        // And I should remain on the login page
        await AuthSteps.ThenIShouldRemainOnTheLoginPage();
    }

    /// <summary>
    /// User logs out successfully
    /// </summary>
    [Test]
    public async Task UserLogsOutSuccessfully()
    {
        // Given I am logged in
        await AuthSteps.GivenIAmLoggedIn();

        // And I am viewing my profile page
        await NavigationSteps.GivenIAmViewingMyProfilePage();

        // When I click the logout button
        await AuthSteps.WhenIClickTheLogoutButton();

        // Then I should be logged out
        await AuthSteps.ThenIShouldBeLoggedOut();

        // And I should be redirected to the home page
        await NavigationSteps.ThenIShouldBeRedirectedToTheHomePage();

        // And I should see the login option in the navigation
        await AuthSteps.ThenIShouldSeeTheLoginOptionInTheNavigation();

        // And I should not see any personal information
        await AuthSteps.ThenIShouldNotSeeAnyPersonalInformation();
    }

    #endregion

    #region Rule: Account Management
    // Authenticated users can view their profile

    /// <summary>
    /// User views their account details
    /// </summary>
    [Test]
    public async Task UserViewsTheirAccountDetails()
    {
        // Given I am logged in
        await AuthSteps.GivenIAmLoggedIn();

        // And I am on any page in the application
        await NavigationSteps.GivenIAmOnAnyPageInTheApplication();

        // When I navigate to my profile page
        await NavigationSteps.GivenIAmViewingMyProfilePage();

        // Then I should see my account information
        await AuthSteps.ThenIShouldSeeMyAccountInformation();
    }

    #endregion

    #region Rule: Access Control
    // The system enforces authentication requirements for protected resources

    /// <summary>
    /// Logged in user cannot access login page
    /// </summary>
    /// <remarks>
    /// It seems the application doesn't always redirect logged-in users away from the login page.
    /// This could be due to caching issues or session management quirks.
    ///
    /// UPDATE: Have worked in this area, so it seems to be working now. Will try
    /// removing the Explicit attribute to see if the test is stable.
    /// </remarks>
    [Test]
    public async Task LoggedInUserCannotAccessLoginPage()
    {
        // Given I am logged in
        await AuthSteps.GivenIAmLoggedIn();

        // When: I try to navigate directly to the login page, expecting it to fail
        await NavigationSteps.WhenITryToNavigateDirectlyToTheLoginPageExpectingFailure();

        // Then I should be redirected to my profile page
        await NavigationSteps.ThenIShouldBeRedirectedToMyProfilePage();

        // And I should not see the login form
        await AuthSteps.ThenIShouldNotSeeTheLoginForm();
    }

    /// <summary>
    /// Anonymous user cannot access protected pages
    /// </summary>
    [TestCase("/weather")]
    [TestCase("/counter")]
    [TestCase("/about")]
    [TestCase("/profile")]
    public async Task AnonymousUserCannotAccessProtectedPages(string page)
    {
        // Given I am not logged in
        await AuthSteps.GivenIAmNotLoggedIn();

        // When I try to navigate directly to a protected page like <page>
        await NavigationSteps.WhenITryToNavigateDirectlyToAProtectedPageLike(page);

        // Then I should be redirected to the login page
        await NavigationSteps.ThenIShouldBeRedirectedToTheLoginPage();

        // And I should see a message indicating I need to log in
        await NavigationSteps.ThenIShouldSeeAMessageIndicatingINeedToLogIn();

        // And after logging in, I should be redirected to the originally requested page
        await NavigationSteps.ThenAfterLoggingInIShouldBeRedirectedToTheOriginallyRequestedPage();
    }

    #endregion
}
