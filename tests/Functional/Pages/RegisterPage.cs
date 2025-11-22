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
    public ILocator SignInLink => Page!.GetByRole(AriaRole.Link, new() { Name = "Sign in here" });

    public async Task RegisterAsync(string email, string username, string password)
    {
        await EmailInput.FillAsync(email);
        await UsernameInput.FillAsync(username);
        await PasswordInput.FillAsync(password);
        await PasswordAgainInput.FillAsync(password);

        await SaveScreenshotAsync("Registering");

        await WaitForApi(async () =>
        {
            await RegisterButton.ClickAsync();
        }, "/api/auth/register*");
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

    public async Task ClickRegisterButtonAsync()
    {
        await SaveScreenshotAsync("Before-registration-attempt");

        await WaitForApi(async () =>
        {
            await RegisterButton.ClickAsync();
        }, RegisterApiRegex);
    }

    public async Task<bool> HasErrorMessageAsync(string expectedError)
    {
        await ErrorDisplay.WaitForAsync();
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
}
