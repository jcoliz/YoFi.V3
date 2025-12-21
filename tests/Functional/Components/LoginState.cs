using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;

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
    #region Component Elements

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

    #endregion

    #region Actions

    /// <summary>
    /// Opens the dropdown menu
    /// </summary>
    public async Task OpenMenuAsync()
    {
        await Trigger.ClickAsync();
        await Menu.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Clicks the Sign In menu item and waits for login page to be ready
    /// </summary>
    public async Task ClickSignInAsync()
    {
        await OpenMenuAsync();
        await SignInMenuItem.ClickAsync();
        // Wait for login page to be ready using its own encapsulated logic
        var loginPage = new LoginPage(page);
        await loginPage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Clicks the Sign Out menu item and waits for navigation to complete
    /// </summary>
    public async Task ClickSignOutAsync()
    {
        await OpenMenuAsync();
        await SignOutMenuItem.ClickAsync();
        // Wait for home page to be ready (sign out redirects to home)
        var homePage = new HomePage(page);
        await homePage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Clicks the Profile menu item and waits for profile page to be ready
    /// </summary>
    public async Task ClickProfileAsync()
    {
        await OpenMenuAsync();
        await ProfileMenuItem.ClickAsync();
        // Wait for profile page to be ready using its own encapsulated logic
        var profilePage = new ProfilePage(page);
        await profilePage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Clicks the Create Account menu item and waits for register page to be ready
    /// </summary>
    public async Task ClickCreateAccountAsync()
    {
        await OpenMenuAsync();
        await CreateAccountMenuItem.ClickAsync();
        // Wait for register page to be ready using its own encapsulated logic
        var registerPage = new RegisterPage(page);
        await registerPage.WaitForPageReadyAsync();
    }

    #endregion

    #region Query Methods

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

    #endregion
}
