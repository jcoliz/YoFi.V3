using System.Text.RegularExpressions;
using Microsoft.Playwright;
namespace YoFi.V3.Tests.Functional.Pages;

public class ProfilePage(IPage _page): BasePage(_page)
{
    private static readonly Regex RefreshTokenApiRegex = new("/api/auth/refresh", RegexOptions.Compiled);

    #region Page Elements

    /// <summary>
    /// Account information section
    /// </summary>
    public ILocator AccountInfoSection => Page!.GetByTestId("AccountInfo");

    /// <summary>
    /// Workspace information section
    /// </summary>
    public ILocator WorkspaceInfoSection => Page!.GetByTestId("WorkspaceInfo");

    /// <summary>
    /// Email display in account information
    /// </summary>
    public ILocator EmailDisplay => AccountInfoSection.GetByTestId("Email");

    /// <summary>
    /// Username display in account information
    /// </summary>
    public ILocator UsernameDisplay => AccountInfoSection.GetByTestId("Username");

    /// <summary>
    /// Logout button
    /// </summary>
    public ILocator LogoutButton => Page!.GetByTestId("Logout");

    /// <summary>
    /// Refresh token button
    /// </summary>
    public ILocator RefreshButton => Page!.GetByTestId("refresh-token");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates to this page
    /// </summary>
    public async Task NavigateAsync()
    {
        await Page!.GotoAsync("/profile");
        // Wait for the main content to be visible instead of NetworkIdle
        await AccountInfoSection.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    #endregion

    #region Actions

    /// <summary>
    /// Clicks the refresh button and waits for the refresh token API call
    /// </summary>
    public async Task ClickRefreshButtonAsync()
    {
        await WaitForApi(async () =>
        {
            await RefreshButton.ClickAsync();
        }, RefreshTokenApiRegex);
    }

    /// <summary>
    /// Clicks the logout button
    /// </summary>
    public async Task ClickLogoutAsync()
    {
        await LogoutButton.ClickAsync();
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the displayed email
    /// </summary>
    public async Task<string> GetEmailAsync()
    {
        return await EmailDisplay.InnerTextAsync();
    }

    /// <summary>
    /// Gets the displayed username
    /// </summary>
    public async Task<string> GetUsernameAsync()
    {
        return await UsernameDisplay.InnerTextAsync();
    }

    /// <summary>
    /// Checks if account information matches expected values
    /// </summary>
    /// <param name="email">Expected email</param>
    /// <param name="username">Expected username</param>
    public async Task<bool> HasAccountInformationAsync(string email, string username)
    {
        var displayedEmail = await GetEmailAsync();
        var displayedUsername = await GetUsernameAsync();
        return displayedEmail == email && displayedUsername == username;
    }

    /// <summary>
    /// Checks if workspace information section is visible
    /// </summary>
    public async Task<bool> HasWorkspaceInformationAsync()
    {
        return await WorkspaceInfoSection.IsVisibleAsync();
    }

    /// <summary>
    /// Checks if currently on the profile page
    /// </summary>
    public async Task<bool> IsOnProfilePageAsync()
    {
        return await AccountInfoSection.IsVisibleAsync();
    }

    /// <summary>
    /// Waits for and checks if on the profile page
    /// </summary>
    public async Task<bool> WaitForOnProfilePageAsync()
    {
        await AccountInfoSection.WaitForAsync(new LocatorWaitForOptions() { State = WaitForSelectorState.Visible });
        return await AccountInfoSection.IsVisibleAsync();
    }

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Waits for the profile page to be ready
    /// </summary>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await WaitForLogoutButtonReadyAsync(timeout);
    }

    /// <summary>
    /// Waits until the logout button becomes enabled
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Useful when the page needs time to fully load before the button is enabled.
    /// Uses Playwright's WaitForAsync with polling to check the enabled state.
    /// </remarks>
    public async Task WaitForLogoutButtonReadyAsync(float timeout = 5000)
    {
        await LogoutButton.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = timeout });
        await LogoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

        // Poll until the button is enabled
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var isDisabled = await LogoutButton.IsDisabledAsync();
            if (!isDisabled)
            {
                return;
            }
            await Task.Delay(50);
        }

        throw new TimeoutException($"Logout button did not become enabled within {timeout}ms");
    }

    #endregion

}
