using NUnit.Framework;
using YoFi.V3.Tests.Functional.Steps;
using YoFi.V3.Tests.Functional.Helpers;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// User Authentication
/// As a user of YoFi
/// I want to register, login, and manage my account
/// So that I can securely access my financial data
/// </summary>
[Explicit]
public class UserAuthenticationTests : AuthenticationSteps
{
    [SetUp]
    public async Task SetupAsync()
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
        var table = new DataTable();
        table.AddRow("Email", "newuser@example.com");
        table.AddRow("Username", "newuser");
        table.AddRow("Password", "SecurePassword123!");
        table.AddRow("Confirm Password", "SecurePassword123!");
        await WhenIEnterValidRegistrationDetails(table);

        // And I submit the registration form
        await WhenISubmitTheRegistrationForm();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should be successfully registered
        await ThenIShouldBeSuccessfullyRegistered();
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

        // When I enter my existing credentials
        await WhenIEnterMyExistingCredentials();

        // And I click the login button
        await WhenIClickTheLoginButton();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should be successfully logged in
        await ThenIShouldBeSuccessfullyLoggedIn();

        // And I should see my profile page
        await ThenIShouldSeeMyProfilePage();

        // And I should see my username on the profile page
        await ThenIShouldSeeMyUsernameOnTheProfilePage();

        // And I should see my username in the header
        await ThenIShouldSeeMyUsernameInTheHeader();
    }
}