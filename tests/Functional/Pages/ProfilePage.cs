using System.Text.RegularExpressions;
using Microsoft.Playwright;
namespace YoFi.V3.Tests.Functional.Pages;

public class ProfilePage(IPage _page): BasePage(_page)
{
    private static readonly Regex RefreshTokenApiRegex = new("/api/auth/refresh", RegexOptions.Compiled);

    // Main sections
    public ILocator AccountInfoSection => Page!.GetByTestId("AccountInfo");
    public ILocator WorkspaceInfoSection => Page!.GetByTestId("WorkspaceInfo");

    // Account information display
    public ILocator EmailDisplay => AccountInfoSection.GetByTestId("Email");
    public ILocator UsernameDisplay => AccountInfoSection.GetByTestId("Username");

    // Account action buttons
    public ILocator LogoutButton => Page!.GetByTestId("Logout");

    public ILocator RefreshButton => Page!.GetByTestId("refresh-token");

    #region Navigation

    /// <summary>
    /// Navigates to the workspaces page
    /// </summary>
    public async Task NavigateAsync()
    {
        await Page!.GotoAsync("/profile");
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion


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

    public async Task<string> GetEmailAsync()
    {
        return await EmailDisplay.InnerTextAsync();
    }

    public async Task<string> GetUsernameAsync()
    {
        return await UsernameDisplay.InnerTextAsync();
    }

    public async Task<bool> HasAccountInformationAsync(string email, string username)
    {
        var displayedEmail = await GetEmailAsync();
        var displayedUsername = await GetUsernameAsync();
        return displayedEmail == email && displayedUsername == username;
    }


    public async Task<bool> HasWorkspaceInformationAsync()
    {
        return await WorkspaceInfoSection.IsVisibleAsync();
    }

    public async Task ClickLogoutAsync()
    {
        await SaveScreenshotAsync("Before-logout");
        await LogoutButton.ClickAsync();
    }

    public async Task<bool> IsOnProfilePageAsync()
    {
        return await AccountInfoSection.IsVisibleAsync();
    }

    public async Task<bool> WaitForOnProfilePageAsync()
    {
        await AccountInfoSection.WaitForAsync(new LocatorWaitForOptions() { State = WaitForSelectorState.Visible });
        return await AccountInfoSection.IsVisibleAsync();
    }

}
