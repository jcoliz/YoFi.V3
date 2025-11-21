using System.Text.RegularExpressions;
using Microsoft.Playwright;
namespace YoFi.V3.Tests.Functional.Pages;

public class LoginPage(IPage _page): BasePage(_page)
{
    private static readonly Regex LoginApiRegex = new("/api/auth/login", RegexOptions.Compiled);

    public ILocator View => Page!.GetByTestId("LoginForm");
    public ILocator UsernameInput => View.GetByTestId("username");
    public ILocator PasswordInput => View.GetByTestId("password");
    public ILocator LoginButton => View.GetByTestId("Login");
    public ILocator ErrorDisplay => View.GetByTestId("error-display");
    public ILocator CreateAccountLink => Page!.GetByRole(AriaRole.Link, new() { Name = "Create one here" });

    public async Task EnterCredentialsAsync(string email, string password)
    {
        await UsernameInput.FillAsync(email);
        await PasswordInput.FillAsync(password);
    }

    public async Task EnterUsernameOnlyAsync(string username)
    {
        await UsernameInput.FillAsync(username);
        // Intentionally leave password empty for validation testing
    }

    public async Task ClickLoginButtonAsync()
    {
        await SaveScreenshotAsync("Before-login-attempt");

        await WaitForApi(async () =>
        {
            await LoginButton.ClickAsync();
        }, LoginApiRegex);
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

    public async Task<bool> IsOnLoginPageAsync()
    {
        return await View.IsVisibleAsync();
    }

    public async Task NavigateToCreateAccountAsync()
    {
        await CreateAccountLink.ClickAsync();
    }

    public async Task<bool> IsLoginButtonDisabledAsync()
    {
        return await LoginButton.IsDisabledAsync();
    }

    public async Task<bool> AreInputsDisabledAsync()
    {
        var emailDisabled = await UsernameInput.IsDisabledAsync();
        var passwordDisabled = await PasswordInput.IsDisabledAsync();
        return emailDisabled && passwordDisabled;
    }

    public async Task ClearFormAsync()
    {
        await UsernameInput.ClearAsync();
        await PasswordInput.ClearAsync();
    }
}
