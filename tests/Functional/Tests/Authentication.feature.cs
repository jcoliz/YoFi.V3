using NUnit.Framework;
using YoFi.V3.Tests.Functional.Steps;
using YoFi.V3.Tests.Functional.Helpers;
using System.Runtime.InteropServices.Marshalling;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// User Authentication
/// As a user of YoFi
/// I want to register, login, and manage my account
/// So that I can securely access my financial data
/// </summary>
public class UserAuthenticationTests : AuthenticationSteps
{
    [SetUp]
    public async Task Background()
    {
        // Given the application is running
        await GivenTheApplicationIsRunning();

        // And I am not logged in
        await GivenIAmNotLoggedIn();
    }

    /// <summary>
    /// User registers for a new account
    /// </summary>
    [Test]
    public async Task UserRegistersForANewAccount()
    {
        // Given I am on the registration page
        await GivenIAmOnTheRegistrationPage();

        // When I enter valid registration details:
        await WhenIEnterValidRegistrationDetails();

        // And I submit the registration form
        await WhenISubmitTheRegistrationForm();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then My registration request should be acknowledged
        await ThenMyRegistrationRequestShouldBeAcknowledged();
    }

    /// <summary>
    /// User logs into an existing account
    /// </summary>
    [Test]
    public async Task UserLogsIntoAnExistingAccount()
    {
        // Given I have an existing account
        await GivenIHaveAnExistingAccount();

        // And I am on the login page
        await GivenIAmOnTheLoginPage();

        // When I enter my credentials
        await WhenIEnterMyCredentials();

        // And I click the login button
        await WhenIClickTheLoginButton();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see the home page
        await ThenIShouldSeeTheHomePage();

        // And I should be successfully logged in
        await ThenIShouldBeSuccessfullyLoggedIn();
    }

    /// <summary>
    /// User login fails with invalid credentials
    /// </summary>
    [Test]
    public async Task UserLoginFailsWithInvalidCredentials()
    {
        // Given I am on the login page
        await GivenIAmOnTheLoginPage();

        // When I enter invalid credentials
        await WhenIEnterInvalidCredentials();

        // And I click the login button
        await WhenIClickTheLoginButton();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see an error message "Invalid credentials"
        await ThenIShouldSeeAnErrorMessage("Invalid credentials");

        // And I should remain on the login page
        await ThenIShouldRemainOnTheLoginPage();
    }

    /// <summary>
    /// User login fails with missing password
    /// </summary>
    [Test]
    public async Task UserLoginFailsWithMissingPassword()
    {
        // Given I am on the login page
        await GivenIAmOnTheLoginPage();

        // When I enter only a username
        await WhenIEnterOnlyUsername();

        // And I leave the password field empty
        await WhenILeaveThePasswordFieldEmpty();

        // And I click the login button (for validation)
        await WhenIClickTheLoginButtonForValidation();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see a validation error
        await ThenIShouldSeeAValidationError();

        // And I should remain on the login page
        await ThenIShouldRemainOnTheLoginPage();
    }

    /// <summary>
    /// User views their account details
    /// </summary>
    [Test]
    public async Task UserViewsTheirAccountDetails()
    {
        // Given I am logged in
        await GivenIAmLoggedIn();

        // And I am on any page in the application
        await GivenIAmOnAnyPageInTheApplication();

        // When I navigate to my profile page
        await WhenINavigateToMyProfilePage();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see my account information
        await ThenIShouldSeeMyAccountInformation();

        // And I should see options to update my profile
        await ThenIShouldSeeOptionsToUpdateMyProfile();

        // And I should see my current workspace information
        await ThenIShouldSeeMyCurrentWorkspaceInformation();
    }

    /// <summary>
    /// User logs out successfully
    /// </summary>
    [Test]
    public async Task UserLogsOutSuccessfully()
    {
        // Given I am logged in
        await GivenIAmLoggedIn();

        // And I am viewing my profile page
        await GivenIAmViewingMyProfilePage();

        // When I click the logout button
        await WhenIClickTheLogoutButton();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should be logged out
        await ThenIShouldBeLoggedOut();

        // And I should be redirected to the home page
        await ThenIShouldBeRedirectedToTheHomePage();

        // And I should see the login option in the navigation
        await ThenIShouldSeeTheLoginOptionInTheNavigation();

        // And I should not see any personal information
        await ThenIShouldNotSeeAnyPersonalInformation();
    }

    /// <summary>
    /// User registration fails with weak password
    /// </summary>
    [Test]
    public async Task UserRegistrationFailsWithWeakPassword()
    {
        // Given I am on the registration page
        await GivenIAmOnTheRegistrationPage();

        // When I enter registration details with a weak password
        await WhenIEnterRegistrationDetailsWithAWeakPassword();

        // And I submit the registration form
        await WhenISubmitTheRegistrationForm();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see an error message containing "Passwords must be"
        await ThenIShouldSeeAnErrorMessage("Passwords must be");

        // And I should remain on the registration page
        await ThenIShouldRemainOnTheRegistrationPage();
    }

    /// <summary>
    /// User registration fails with mismatched passwords
    /// </summary>
    [Test]
    public async Task UserRegistrationFailsWithMismatchedPasswords()
    {
        // Given I am on the registration page
        await GivenIAmOnTheRegistrationPage();

        // When I enter registration details with mismatched passwords
        await WhenIEnterRegistrationDetailsWithMismatchedPasswords();

        // And I submit the registration form (for validation)
        await WhenISubmitTheRegistrationFormForValidation();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see an error message containing "Passwords do not match"
        await ThenIShouldSeeAnErrorMessage("Passwords do not match");

        // And I should remain on the registration page
        await ThenIShouldRemainOnTheRegistrationPage();
    }
    /// <summary>
    /// User registration fails with existing email
    /// </summary>
    [Test]
    public async Task UserRegistrationFailsWithExistingEmail()
    {
        // Given I have an existing account
        await GivenIHaveAnExistingAccount();

        // And I am on the registration page
        await GivenIAmOnTheRegistrationPage();

        // When I enter registration details with the existing email
        await WhenIEnterRegistrationDetailsWithTheExistingEmail();

        // And I submit the registration form
        await WhenISubmitTheRegistrationForm();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see an error message containing "is already taken"
        await ThenIShouldSeeAnErrorMessage("is already taken");

        // And I should remain on the registration page
        await ThenIShouldRemainOnTheRegistrationPage();

        // And I should not be registered
        await ThenIShouldNotBeRegistered();
    }
    /// <summary>
    /// Logged in user cannot access login page
    /// </summary>
    [Test]
    public async Task LoggedInUserCannotAccessLoginPage()
    {
        // Given I am logged in
        await GivenIAmLoggedIn();

        // When I try to navigate directly to the login page
        await WhenITryToNavigateDirectlyToTheLoginPage();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should be redirected to my profile page
        await ThenIShouldBeRedirectedToMyProfilePage();

        // And I should not see the login form
        await ThenIShouldNotSeeTheLoginForm();
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
        await GivenIAmNotLoggedIn();

        // When I try to navigate directly to a protected page like <page>
        await WhenITryToNavigateDirectlyToAProtectedPageLike(page);

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should be redirected to the login page
        await ThenIShouldBeRedirectedToTheLoginPage();

        // And I should see a message indicating I need to log in
        await ThenIShouldSeeAMessageIndicatingINeedToLogIn();

        // And after logging in, I should be redirected to the originally requested page
        await ThenAfterLoggingInIShouldBeRedirectedToTheOriginallyRequestedPage();
    }
}
