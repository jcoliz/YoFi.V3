using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions; // Add this line
namespace YoFi.V3.Tests.Functional.Pages;

public class RegisterPage(IPage _page): BasePage(_page)
{
    private static readonly Regex RegisterApiRegex = new("/api/auth/signup", RegexOptions.Compiled);

    #region Page Elements

    /// <summary>
    /// Main registration form
    /// </summary>
    public ILocator RegisterForm => Page!.GetByTestId("RegisterForm");

    /// <summary>
    /// Email input field
    /// </summary>
    public ILocator EmailInput => RegisterForm.GetByTestId("email");

    /// <summary>
    /// Username input field
    /// </summary>
    public ILocator UsernameInput =>  RegisterForm.GetByTestId("username");

    /// <summary>
    /// Password input field
    /// </summary>
    public ILocator PasswordInput => RegisterForm.GetByTestId("password");

    /// <summary>
    /// Password confirmation input field
    /// </summary>
    public ILocator PasswordAgainInput => RegisterForm.GetByTestId("password-again");

    /// <summary>
    /// Register submit button
    /// </summary>
    public ILocator RegisterButton => RegisterForm.GetByTestId("Register");

    /// <summary>
    /// Error display section
    /// </summary>
    public ILocator ErrorDisplay => RegisterForm.GetByTestId("Errors");

    /// <summary>
    /// Success message container (shown after successful registration)
    /// </summary>
    public ILocator SuccessMessage => Page!.GetByTestId("SuccessMessage");

    /// <summary>
    /// Email display in success message
    /// </summary>
    public ILocator EmailDisplay => SuccessMessage.GetByTestId("display-email");

    /// <summary>
    /// Username display in success message
    /// </summary>
    public ILocator UsernameDisplay =>  SuccessMessage.GetByTestId("display-username");

    /// <summary>
    /// Continue button in success message
    /// </summary>
    public ILocator ContinueButton => SuccessMessage.GetByTestId("ContinueButton");

    /// <summary>
    /// Link to navigate to the login page
    /// </summary>
    public ILocator SignInLink => Page!.GetByTestId("sign-in-link");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates to this page
    /// </summary>
    public async Task NavigateAsync()
    {
        await Page!.GotoAsync("/register");
        await WaitForPageReadyAsync();
    }

    /// <summary>
    /// Navigates to the login page
    /// </summary>
    public async Task NavigateToSignInAsync()
    {
        await SignInLink.ClickAsync();
    }

    #endregion

    #region Registration Actions

    /// <summary>
    /// Performs a complete registration
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="username">Username</param>
    /// <param name="password">Password</param>
    public async Task RegisterAsync(string email, string username, string password)
    {
        await FillRegistrationWithVueWaitAsync(email, username, password, password);
        await ClickRegisterButtonAsync();
    }

    /// <summary>
    /// Enters registration details without submitting
    /// </summary>
    public async Task EnterRegistrationDetailsAsync(string email, string username, string password, string confirmPassword)
    {
        await ClearFormAsync();
        await EmailInput.ClickAsync();
        await FillRegistrationWithVueWaitAsync(email, username, password, confirmPassword);
    }

    /// <summary>
    /// Enters registration details with a weak password
    /// </summary>
    public async Task EnterWeakPasswordDetailsAsync(string email, string username, string weakPassword)
    {
        await FillRegistrationWithVueWaitAsync(email, username, weakPassword, weakPassword);
    }

    /// <summary>
    /// Enters registration details with mismatched passwords
    /// </summary>
    public async Task EnterMismatchedPasswordDetailsAsync(string email, string username, string password, string differentPassword)
    {
        await FillRegistrationWithVueWaitAsync(email, username, password, differentPassword);
    }

    /// <summary>
    /// Clicks the register button and waits for the registration API call
    /// </summary>
    public async Task ClickRegisterButtonAsync()
    {
        await WaitForApi(async () =>
        {
            await RegisterButton.ClickAsync();
        }, RegisterApiRegex);
    }

    /// <summary>
    /// Clicks the register button without waiting for API (for validation testing)
    /// </summary>
    public async Task ClickRegisterButtonForValidation()
    {
        await RegisterButton.ClickAsync();
        await WaitForErrorDisplayAsync();
    }

    /// <summary>
    /// Clears all form fields
    /// </summary>
    public async Task ClearFormAsync()
    {
        await EmailInput.ClearAsync();
        await UsernameInput.ClearAsync();
        await PasswordInput.ClearAsync();
        await PasswordAgainInput.ClearAsync();
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Checks if an error message is displayed
    /// </summary>
    /// <param name="expectedError">Expected error message text</param>
    public async Task<bool> HasErrorMessageAsync(string expectedError)
    {
        await WaitForErrorDisplayAsync();
        var errorText = await ErrorDisplay.InnerTextAsync();
        return errorText.Contains(expectedError);
    }

    /// <summary>
    /// Checks if a validation error is displayed
    /// </summary>
    /// <param name="expectedError">Expected error message text</param>
    public async Task<bool> HasValidationErrorAsync(string expectedError)
    {
        return await HasErrorMessageAsync(expectedError);
    }

    /// <summary>
    /// Checks if password field has validation error
    /// </summary>
    public async Task<bool> HasPasswordRequirementErrorAsync()
    {
        // Check for password validation error in the form itself
        var passwordField = PasswordInput;
        var hasInvalidClass = await passwordField.GetAttributeAsync("class");
        return hasInvalidClass?.Contains("is-invalid") ?? false;
    }

    /// <summary>
    /// Checks if password confirmation has mismatch error
    /// </summary>
    public async Task<bool> HasPasswordMismatchErrorAsync()
    {
        // Check for password mismatch validation error
        var confirmField = PasswordAgainInput;
        var hasInvalidClass = await confirmField.GetAttributeAsync("class");
        return hasInvalidClass?.Contains("is-invalid") ?? false;
    }

    /// <summary>
    /// Checks if the registration form is visible
    /// </summary>
    public async Task<bool> IsRegisterFormVisibleAsync()
    {
        await RegisterForm.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        return await RegisterForm.IsVisibleAsync();
    }

    /// <summary>
    /// Checks if the register button is disabled
    /// </summary>
    public async Task<bool> IsRegisterButtonDisabledAsync()
    {
        return await RegisterButton.IsDisabledAsync();
    }

    /// <summary>
    /// Checks if the register button is enabled
    /// </summary>
    public async Task<bool> IsRegisterButtonEnabledAsync()
    {
        var visible = await RegisterButton.IsVisibleAsync();
        return visible && !await RegisterButton.IsDisabledAsync();
    }

    /// <summary>
    /// Checks if input fields are disabled
    /// </summary>
    public async Task<bool> AreInputsDisabledAsync()
    {
        var emailDisabled = await EmailInput.IsDisabledAsync();
        var usernameDisabled = await UsernameInput.IsDisabledAsync();
        var passwordDisabled = await PasswordInput.IsDisabledAsync();
        var confirmDisabled = await PasswordAgainInput.IsDisabledAsync();
        return emailDisabled && usernameDisabled && passwordDisabled && confirmDisabled;
    }

    /// <summary>
    /// Checks if the form is in loading state
    /// </summary>
    public async Task<bool> IsLoadingAsync()
    {
        // Check if the form is in loading state by looking for spinner or disabled state
        return await IsRegisterButtonDisabledAsync();
    }

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Waits for the error display to become visible
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    public async Task WaitForErrorDisplayAsync(int timeout = 5000)
    {
        await ErrorDisplay.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
    }

    /// <summary>
    /// Waits for the register page to be ready
    /// </summary>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await WaitForRegisterButtonEnabledAsync(timeout);
    }

    /// <summary>
    /// Waits until the register button becomes enabled
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Useful when form validation needs to complete before the button is enabled.
    /// Uses Playwright's WaitForAsync with Enabled state.
    /// </remarks>
    public Task WaitForRegisterButtonEnabledAsync(float timeout = 5000)
        => WaitForButtonToBeEnabledAsync(RegisterButton, timeout);

    #endregion

    #region Helpers

    /// <summary>
    /// Fills registration form fields and verifies their values before submission.
    /// </summary>
    private async Task FillRegistrationWithVueWaitAsync(string email, string username, string password, string confirmPassword)
    {
        // Click each field first to ensure it's ready for input (required for FillAsync to work)
        await EmailInput.ClickAsync();
        await EmailInput.FillAsync(email);
        await EmailInput.BlurAsync();

        await UsernameInput.ClickAsync();
        await UsernameInput.FillAsync(username);
        await UsernameInput.BlurAsync();

        await PasswordInput.ClickAsync();
        await PasswordInput.FillAsync(password);
        await PasswordInput.BlurAsync();

        await PasswordAgainInput.ClickAsync();
        await PasswordAgainInput.FillAsync(confirmPassword);
        await PasswordAgainInput.BlurAsync();

        await Assertions.Expect(EmailInput).ToHaveValueAsync(email);
        await Assertions.Expect(UsernameInput).ToHaveValueAsync(username);
        await Assertions.Expect(PasswordInput).ToHaveValueAsync(password);
        await Assertions.Expect(PasswordAgainInput).ToHaveValueAsync(confirmPassword);
    }

    #endregion
}
