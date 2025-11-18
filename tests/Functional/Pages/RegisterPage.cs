using Microsoft.Playwright;
namespace YoFi.V3.Tests.Functional.Pages;

public class RegisterPage(IPage _page): BasePage(_page)
{
    public ILocator View => Page!.GetByTestId("RegisterForm");
    public ILocator EmailInput => View.GetByTestId("email");
    public ILocator UsernameInput =>  View.GetByTestId("username");
    public ILocator PasswordInput => View.GetByTestId("password");
    public ILocator PasswordAgainInput => View.GetByTestId("password-again");
    public ILocator RegisterButton => View.GetByTestId("Register");
    public ILocator ErrorDisplay => View.GetByTestId("Errors");
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
        await RegisterButton.ClickAsync();
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

    public async Task<bool> IsOnRegistrationPageAsync()
    {
        return await View.IsVisibleAsync();
    }

    public async Task NavigateToSignInAsync()
    {
        await SignInLink.ClickAsync();
    }

    public async Task<bool> IsRegisterButtonDisabledAsync()
    {
        return await RegisterButton.IsDisabledAsync();
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
        await EmailInput.FillAsync("");
        await UsernameInput.FillAsync("");
        await PasswordInput.FillAsync("");
        await PasswordAgainInput.FillAsync("");
    }

    public async Task<bool> IsLoadingAsync()
    {
        // Check if the form is in loading state by looking for spinner or disabled state
        return await IsRegisterButtonDisabledAsync();
    }
}