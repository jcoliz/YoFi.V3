using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
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

    #endregion

    #region Steps: WHEN

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

    #endregion
}
