using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Components;

/// <summary>
/// Page Object Model for the LoginState component
/// </summary>
/// <remarks>
/// Represents the login/user state dropdown component that shows authentication status
/// and provides login/logout functionality
/// </remarks>
public class LoginState(IPage page, ILocator parent)
{
    /// <summary>
    /// Root element of the LoginState component
    /// </summary>
    public ILocator Root => parent.GetByTestId("login-state");

    /// <summary>
    /// The dropdown trigger element (user icon and username if logged in)
    /// </summary>
    public ILocator Trigger => Root.Locator(".dropdown-toggle");

    /// <summary>
    /// Username display (only visible when user is logged in)
    /// </summary>
    public ILocator Username => Root.GetByTestId("username");

    /// <summary>
    /// User icon element
    /// </summary>
    public ILocator UserIcon => Root.Locator(".rounded-circle");

    /// <summary>
    /// The dropdown menu containing options
    /// </summary>
    public ILocator Menu => Root.Locator(".dropdown-menu");

    /// <summary>
    /// Profile menu item (visible when logged in)
    /// </summary>
    public ILocator ProfileMenuItem => Menu.GetByTestId("Profile");

    /// <summary>
    /// Sign Out menu item (visible when logged in)
    /// </summary>
    public ILocator SignOutMenuItem => Menu.GetByTestId("SignOut");

    /// <summary>
    /// Sign In menu item (visible when logged out)
    /// </summary>
    public ILocator SignInMenuItem => Menu.GetByTestId("SignIn");

    /// <summary>
    /// Create Account menu item (visible when logged out)
    /// </summary>
    public ILocator CreateAccountMenuItem => Menu.GetByTestId("CreateAccount");

    /// <summary>
    /// Opens the dropdown menu
    /// </summary>
    public async Task OpenMenuAsync()
    {
        await Trigger.ClickAsync();
        await Menu.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Checks if a user is currently logged in
    /// </summary>
    /// <returns>True if username is visible, false otherwise</returns>
    public async Task<bool> IsLoggedInAsync()
    {
        return await Username.IsVisibleAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the displayed username
    /// </summary>
    /// <returns>The username text, or null if not logged in</returns>
    public async Task<string?> GetUsernameAsync()
    {
        if (await IsLoggedInAsync())
        {
            return await Username.TextContentAsync();
        }
        return null;
    }

    /// <summary>
    /// Clicks the Sign In menu item
    /// </summary>
    public async Task ClickSignInAsync()
    {
        await OpenMenuAsync();
        await SignInMenuItem.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Clicks the Sign Out menu item
    /// </summary>
    public async Task ClickSignOutAsync()
    {
        await OpenMenuAsync();
        await SignOutMenuItem.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Clicks the Profile menu item
    /// </summary>
    public async Task ClickProfileAsync()
    {
        await OpenMenuAsync();
        await ProfileMenuItem.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Clicks the Create Account menu item
    /// </summary>
    public async Task ClickCreateAccountAsync()
    {
        await OpenMenuAsync();
        await CreateAccountMenuItem.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
