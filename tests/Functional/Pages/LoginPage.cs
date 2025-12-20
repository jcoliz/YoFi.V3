using System.Text.RegularExpressions;
using Microsoft.Playwright;
namespace YoFi.V3.Tests.Functional.Pages;

public partial class LoginPage(IPage _page): BasePage(_page)
{
    [GeneratedRegex("/api/auth/login")]
    private static partial Regex LoginApiRegex();

    public ILocator View => Page!.GetByTestId("LoginForm");
    public ILocator UsernameInput => View.GetByTestId("username");
    public ILocator PasswordInput => View.GetByTestId("password");
    public ILocator LoginButton => View.GetByTestId("Login");
    public ILocator ErrorDisplay => View.GetByTestId("error-display");

    /// <summary>
    /// Link to navigate to the registration page
    /// </summary>
    public ILocator CreateAccountLink => Page!.GetByTestId("create-account-link");

    #region Navigation

    /// <summary>
    /// Navigates to this page
    /// </summary>
    public async Task NavigateAsync()
    {
        await Page!.GotoAsync("/login");
        await LoginButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    #endregion

    public async Task LoginAsync(string email, string password)
    {
        // Fill username field and wait for Vue to process the input
        await UsernameInput.FillAsync(email);
        await UsernameInput.BlurAsync(); // Trigger blur event for Vue reactivity

        // Fill password field and wait for Vue to process the input
        await PasswordInput.FillAsync(password);
        await PasswordInput.BlurAsync(); // Trigger blur event for Vue reactivity

        // Wait for fields to actually contain the values (Vue reactivity completion)
        // This prevents race condition where button is clicked before form is ready
        await UsernameInput.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var maxRetries = 10;
        for (int i = 0; i < maxRetries; i++)
        {
            var usernameValue = await UsernameInput.InputValueAsync();
            if (!string.IsNullOrEmpty(usernameValue))
            {
                break;
            }
            TestContext.WriteLine("Waiting for username input to be populated...");
            await Task.Delay(50);
        }

        await ClickLoginButtonAsync();
    }

    public async Task EnterCredentialsAsync(string email, string password)
    {
        await UsernameInput.FillAsync(email);
        await PasswordInput.FillAsync(password);
    }

    public async Task EnterUsernameOnlyAsync(string username)
    {
        await UsernameInput.FillAsync(username);
        await PasswordInput.ClearAsync();
        // Intentionally leave password empty for validation testing
    }

    /// <summary>
    /// Clicks the login button and waits for the login API call
    /// </summary>
    public async Task ClickLoginButtonAsync()
    {
        await WaitForApi(async () =>
        {
            await LoginButton.ClickAsync();
        }, LoginApiRegex());
    }

    public async Task<bool> HasErrorMessageAsync(string expectedError)
    {
        await WaitForErrorDisplayAsync();
        var errorText = await ErrorDisplay.InnerTextAsync();
        return errorText.Contains(expectedError);
    }

    /// <summary>
    /// Waits for the error display to become visible
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    public async Task WaitForErrorDisplayAsync(int timeout = 5000)
    {
        await ErrorDisplay.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
    }

    public async Task<bool> HasValidationErrorAsync()
    {
        await ErrorDisplay.WaitForAsync();
        var errorText = await ErrorDisplay.InnerTextAsync();
        return !string.IsNullOrEmpty(errorText);
    }

    public async Task<bool> IsOnLoginPageAsync()
    {
        return await View.IsVisibleAsync();
    }

    /// <summary>
    /// Waits for the login page to be ready
    /// </summary>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await LoginButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
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

    /// <summary>
    /// Check if the password field has HTML5 validation error (required field)
    /// </summary>
    public async Task<bool> HasPasswordRequiredValidationAsync()
    {
        // Check the HTML5 validity state
        var validationMessage = await PasswordInput.EvaluateAsync<string>("el => el.validationMessage");
        return !string.IsNullOrEmpty(validationMessage);
    }

    /// <summary>
    /// Get the HTML5 validation message for the password field
    /// </summary>
    public async Task<string> GetPasswordValidationMessageAsync()
    {
        return await PasswordInput.EvaluateAsync<string>("el => el.validationMessage");
    }

    /// <summary>
    /// Clicks the login button without waiting for API (for validation testing)
    /// </summary>
    public async Task ClickLoginButtonForValidation()
    {
        await LoginButton.ClickAsync();
    }
}
