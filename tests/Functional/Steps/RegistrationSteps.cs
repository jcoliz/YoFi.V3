using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for user registration operations.
/// </summary>
/// <param name="_context">The test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles user registration flows including form validation, account creation,
/// and registration error scenarios.
/// </remarks>
public class RegistrationSteps(ITestContext _context)
{
    #region Steps: GIVEN

    /// <summary>
    /// Navigates to the user registration page and verifies the form is displayed.
    /// </summary>
    /// <remarks>
    /// Creates or retrieves the RegisterPage from the context and ensures
    /// the registration form is visible before proceeding with registration steps.
    /// </remarks>
    //[Given("I am on the registration page")]
    public async Task GivenIAmOnTheRegistrationPage()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();
        await registerPage.NavigateAsync();
        Assert.That(await registerPage.IsRegisterFormVisibleAsync(), Is.True, "Should be on registration page");
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// Enters valid registration details for a new user account.
    /// </summary>
    /// <remarks>
    /// Generates a unique test user based on the test ID, stores user details in
    /// context, and fills the registration form with valid credentials.
    /// </remarks>
    //[When("I enter valid registration details")]
    public async Task WhenIEnterValidRegistrationDetails()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();

        var user = _context.CreateTestUserCredentials("register");

        await registerPage.EnterRegistrationDetailsAsync(user.Email, user.Username, user.Password, user.Password);
    }

    /// <summary>
    /// Submits the registration form and waits for response.
    /// </summary>
    /// <remarks>
    /// Clicks the register button and expects an API call to be made.
    /// Use WhenISubmitTheRegistrationFormForValidation for client-side validation tests.
    /// </remarks>
    //[When("I submit the registration form")]
    public async Task WhenISubmitTheRegistrationForm()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();
        await registerPage.ClickRegisterButtonAsync();
    }

    /// <summary>
    /// Submits the registration form for client-side validation testing.
    /// </summary>
    /// <remarks>
    /// Clicks the register button without expecting an API call. Used for testing
    /// client-side validation errors that prevent form submission.
    /// </remarks>
    //[When("I submit the registration form (for validation)")]
    public async Task WhenISubmitTheRegistrationFormForValidation()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();
        await registerPage.ClickRegisterButtonForValidation();
    }

    /// <summary>
    /// Enters registration details with a weak password.
    /// </summary>
    /// <remarks>
    /// Generates unique user details, but uses a hardcoded
    /// weak password ("weak") to trigger password strength validation.
    /// </remarks>
    //[When("I enter registration details with a weak password")]
    public async Task WhenIEnterRegistrationDetailsWithAWeakPassword()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();

        var user = _context.CreateTestUserCredentials("register");

        // Use a weak password (too short, no special characters, etc.)
        var weakPassword = "weak";
        await registerPage.EnterWeakPasswordDetailsAsync(user.Email, user.Username, weakPassword);
    }

    /// <summary>
    /// Enters registration details with mismatched password and confirm password fields.
    /// </summary>
    /// <remarks>
    /// Generates unique user details, and fills the form with
    /// deliberately mismatched password and confirm password values to trigger validation.
    /// </remarks>
    //[When("I enter registration details with mismatched passwords")]
    public async Task WhenIEnterRegistrationDetailsWithMismatchedPasswords()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();

        var user = _context.CreateTestUserCredentials("register");

        // Use mismatched passwords
        var password = user.Password;
        var confirmPassword = "DifferentPassword123!";
        await registerPage.EnterMismatchedPasswordDetailsAsync(user.Email, user.Username, password, confirmPassword);
    }

    /// <summary>
    /// Attempts to register with an email that already exists in the system.
    /// </summary>
    /// <remarks>
    /// Retrieves existing user credentials from context and attempts to register
    /// a new account using the same email but different username. Used to test
    /// duplicate email validation.
    /// </remarks>
    //[When("I enter registration details with the existing email")]
    public async Task WhenIEnterRegistrationDetailsWithTheExistingEmail()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();

        // Get the existing user from context
        var existingUser = _context.GetUserCredentials("I");

        // Create a new username but use the existing email
        var newUsername = $"__DUPLICATE__{existingUser.Username}";

        // Use existing email with different username and password
        await registerPage.EnterRegistrationDetailsAsync(
            existingUser.Email,
            newUsername,
            existingUser.Password,
            existingUser.Password);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that registration was successful and displays correct user information.
    /// </summary>
    /// <remarks>
    /// Waits for success message to appear (up to 10 seconds), retrieves registered
    /// user details from context, and validates that displayed email and username
    /// match the registered values.
    /// </remarks>
    //[Then("my registration request should be acknowledged")]
    public async Task ThenMyRegistrationRequestShouldBeAcknowledged()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();
        await registerPage.SuccessMessage.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var user = _context.GetUserCredentials("register");

        var emailDisplayText = await registerPage.EmailDisplay.InnerTextAsync();
        var usernameDisplayText = await registerPage.UsernameDisplay.InnerTextAsync();

        Assert.That(emailDisplayText, Is.EqualTo(user.Email), "Displayed email should match registered email");
        Assert.That(usernameDisplayText, Is.EqualTo(user.Username), "Displayed username should match registered username");
    }

    /// <summary>
    /// Verifies that registration did not succeed.
    /// </summary>
    /// <remarks>
    /// Confirms user remains on registration page and no success message is shown.
    /// Used in negative registration test scenarios.
    /// </remarks>
    //[Then("I should not be registered")]
    public async Task ThenIShouldNotBeRegistered()
    {
        // Verify that registration did not succeed by checking we're still on registration page
        // and no success message is shown
        var registerPage = _context.GetOrCreatePage<RegisterPage>();
        Assert.That(await registerPage.IsRegisterFormVisibleAsync(), Is.True,
            "Should still be on registration page");

        // Verify no success confirmation is displayed
        var hasSuccessMessage = await registerPage.SuccessMessage.IsVisibleAsync();
        Assert.That(hasSuccessMessage, Is.False,
            "Should not show success message for failed registration");
    }

    /// <summary>
    /// Verifies that the user remains on the registration page (not redirected).
    /// </summary>
    /// <remarks>
    /// Used in negative test scenarios where registration should fail and user
    /// should stay on the registration page.
    /// </remarks>
    //[Then("I should remain on the registration page")]
    public async Task ThenIShouldRemainOnTheRegistrationPage()
    {
        var registerPage = _context.GetOrCreatePage<RegisterPage>();
        Assert.That(await registerPage.IsRegisterFormVisibleAsync(), Is.True, "Should remain on registration page");
    }

    #endregion
}
