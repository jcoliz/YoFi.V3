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
    public async Task NavigateAsync(bool waitForReady = true)
    {
        await Page!.GotoAsync("/login");
        if (waitForReady)
        {
            await LoginButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }

        // Lots of tests fail in development environment if we take this away
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion

    public async Task LoginAsync(string email, string password)
    {
        await FillCredentialsWithVueWaitAsync(email, password);
        await ClickLoginButtonAsync();
    }

    public async Task EnterCredentialsAsync(string email, string password)
    {
        await FillCredentialsWithVueWaitAsync(email, password);
    }

    /// <summary>
    /// Fills login credentials and waits for Vue reactivity to complete
    /// </summary>
    /// <remarks>
    /// This method handles the timing issue where Vue.js needs time to process
    /// input events before the form is ready for submission. It fills fields,
    /// triggers blur events, and polls until the username field contains a value.
    /// </remarks>
    private async Task FillCredentialsWithVueWaitAsync(string email, string password)
    {
        // Click username field first to ensure it's ready for input (required for FillAsync to work)
        await UsernameInput.ClickAsync();
        await UsernameInput.FillAsync(email);
        await UsernameInput.BlurAsync(); // Trigger blur event for Vue reactivity

        // Click password field first to ensure it's ready for input
        await PasswordInput.ClickAsync();
        await PasswordInput.FillAsync(password);
        await PasswordInput.BlurAsync(); // Trigger blur event for Vue reactivity

        // Wait for fields to actually contain the values (Vue reactivity completion)
        // This prevents race condition where button is clicked before form is ready
        await UsernameInput.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var maxRetries = 20; // Increased from 10 to handle slower production environments
        for (int i = 0; i < maxRetries; i++)
        {
            var usernameValue = await UsernameInput.InputValueAsync();
            if (!string.IsNullOrEmpty(usernameValue))
            {
                if (i > 0)
                {
                    TestContext.Out.WriteLine($"[LOGIN] Username field populated after {i + 1} retries");
                }
                return;
            }

            // If we've tried 5 times unsuccessfully, try filling again
            if (i == 4)
            {
                TestContext.Out.WriteLine("[LOGIN] First fill may have failed, attempting to fill fields again...");
                await UsernameInput.FillAsync(email);
                await UsernameInput.BlurAsync();
                await PasswordInput.FillAsync(password);
                await PasswordInput.BlurAsync();
            }

            TestContext.Out.WriteLine($"[LOGIN] Retry {i + 1}/{maxRetries}: Username field still empty, waiting for Vue reactivity...");
            await Task.Delay(50);
        }

        // If we get here, field never populated - log warning but continue
        TestContext.Out.WriteLine($"[LOGIN] WARNING: Username field still empty after {maxRetries} retries. Form may not submit properly.");

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
