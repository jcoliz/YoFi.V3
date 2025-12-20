using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions; // Add this line
namespace YoFi.V3.Tests.Functional.Pages;

public class RegisterPage(IPage _page): BasePage(_page)
{
    private static readonly Regex RegisterApiRegex = new("/api/auth/signup", RegexOptions.Compiled);

    public ILocator RegisterForm => Page!.GetByTestId("RegisterForm");
    public ILocator EmailInput => RegisterForm.GetByTestId("email");
    public ILocator UsernameInput =>  RegisterForm.GetByTestId("username");
    public ILocator PasswordInput => RegisterForm.GetByTestId("password");
    public ILocator PasswordAgainInput => RegisterForm.GetByTestId("password-again");
    public ILocator RegisterButton => RegisterForm.GetByTestId("Register");
    public ILocator ErrorDisplay => RegisterForm.GetByTestId("Errors");
    public ILocator SuccessMessage => Page!.GetByTestId("SuccessMessage");
    public ILocator EmailDisplay => SuccessMessage.GetByTestId("display-email");
    public ILocator UsernameDisplay =>  SuccessMessage.GetByTestId("display-username");
    public ILocator ContinueButton => SuccessMessage.GetByTestId("ContinueButton");

    #region Navigation

    /// <summary>
    /// Navigates to this page
    /// </summary>
    public async Task NavigateAsync()
    {
        await Page!.GotoAsync("/register");
        await RegisterForm.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    #endregion

    /// <summary>
    /// Link to navigate to the login page
    /// </summary>
    public ILocator SignInLink => Page!.GetByTestId("sign-in-link");

    public async Task RegisterAsync(string email, string username, string password)
    {
        await EmailInput.FillAsync(email);
        await UsernameInput.FillAsync(username);
        await PasswordInput.FillAsync(password);
        await PasswordAgainInput.FillAsync(password);

        await ClickRegisterButtonAsync();
    }

    public async Task EnterRegistrationDetailsAsync(string email, string username, string password, string confirmPassword)
    {
        await ClearFormAsync();
        await EmailInput.ClickAsync();
        await EmailInput.FillAsync(email);
        await UsernameInput.FillAsync(username);
        await PasswordInput.FillAsync(password);
        await PasswordAgainInput.FillAsync(confirmPassword);
    }

    public async Task EnterWeakPasswordDetailsAsync(string email, string username, string weakPassword)
    {
        await EmailInput.FillAsync(email);
        await UsernameInput.FillAsync(username);
        await PasswordInput.FillAsync(weakPassword);
        await PasswordAgainInput.FillAsync(weakPassword);
    }

    public async Task EnterMismatchedPasswordDetailsAsync(string email, string username, string password, string differentPassword)
    {
        await EmailInput.FillAsync(email);
        await UsernameInput.FillAsync(username);
        await PasswordInput.FillAsync(password);
        await PasswordAgainInput.FillAsync(differentPassword);
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
    /// Waits for the error display to become visible
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    public async Task WaitForErrorDisplayAsync(int timeout = 5000)
    {
        await ErrorDisplay.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
    }

    public async Task<bool> HasErrorMessageAsync(string expectedError)
    {
        await WaitForErrorDisplayAsync();
        var errorText = await ErrorDisplay.InnerTextAsync();
        return errorText.Contains(expectedError);
    }

    public async Task<bool> HasValidationErrorAsync(string expectedError)
    {
        return await HasErrorMessageAsync(expectedError);
    }

    public async Task<bool> HasPasswordRequirementErrorAsync()
    {
        // Check for password validation error in the form itself
        var passwordField = PasswordInput;
        var hasInvalidClass = await passwordField.GetAttributeAsync("class");
        return hasInvalidClass?.Contains("is-invalid") ?? false;
    }

    public async Task<bool> HasPasswordMismatchErrorAsync()
    {
        // Check for password mismatch validation error
        var confirmField = PasswordAgainInput;
        var hasInvalidClass = await confirmField.GetAttributeAsync("class");
        return hasInvalidClass?.Contains("is-invalid") ?? false;
    }

    public async Task<bool> IsRegisterFormVisibleAsync()
    {
        await RegisterForm.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        return await RegisterForm.IsVisibleAsync();
    }

    public async Task NavigateToSignInAsync()
    {
        await SignInLink.ClickAsync();
    }

    public async Task<bool> IsRegisterButtonDisabledAsync()
    {
        return await RegisterButton.IsDisabledAsync();
    }

    public async Task<bool> IsRegisterButtonEnabledAsync()
    {
        var visible = await RegisterButton.IsVisibleAsync();
        return visible && !await RegisterButton.IsDisabledAsync();
    }

    public async Task<bool> AreInputsDisabledAsync()
    {
        var emailDisabled = await EmailInput.IsDisabledAsync();
        var usernameDisabled = await UsernameInput.IsDisabledAsync();
        var passwordDisabled = await PasswordInput.IsDisabledAsync();
        var confirmDisabled = await PasswordAgainInput.IsDisabledAsync();
        return emailDisabled && usernameDisabled && passwordDisabled && confirmDisabled;
    }

    public async Task ClearFormAsync()
    {
        await EmailInput.ClearAsync();
        await UsernameInput.ClearAsync();
        await PasswordInput.ClearAsync();
        await PasswordAgainInput.ClearAsync();
    }

    public async Task<bool> IsLoadingAsync()
    {
        // Check if the form is in loading state by looking for spinner or disabled state
        return await IsRegisterButtonDisabledAsync();
    }

    /// <summary>
    /// Waits for the register page to be ready
    /// </summary>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await RegisterForm.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
    }
}
