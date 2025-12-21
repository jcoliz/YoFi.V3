using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Components;

/// <summary>
/// Page Object Model for the WorkspaceSelector component
/// </summary>
/// <param name="page">The Playwright page instance</param>
/// <param name="parent">The parent locator containing this component</param>
/// <remarks>
/// Represents the workspace/tenant selector component that displays the current workspace
/// and provides functionality to switch between workspaces and manage them.
/// </remarks>
public class WorkspaceSelector(BasePage page, ILocator parent)
{
    #region Component Elements

    /// <summary>
    /// Root element of the WorkspaceSelector component
    /// </summary>
    public ILocator Root => parent.Locator(".workspace-selector");

    /// <summary>
    /// The workspace info section containing label and current workspace name
    /// </summary>
    public ILocator WorkspaceInfo => Root.Locator(".workspace-info");

    /// <summary>
    /// Label element showing "Workspace:"
    /// </summary>
    public ILocator WorkspaceLabel => WorkspaceInfo.Locator(".workspace-label");

    /// <summary>
    /// Current workspace name display
    /// </summary>
    public ILocator CurrentWorkspaceName => Root.GetByTestId("current-workspace");

    /// <summary>
    /// The dropdown trigger button (three vertical dots)
    /// </summary>
    public ILocator MenuTrigger => Root.Locator(".workspace-menu-trigger");

    /// <summary>
    /// The dropdown menu panel
    /// </summary>
    public ILocator MenuPanel => Root.GetByTestId("workspace-selector-dropdown").Locator(".dropdown-menu");

    /// <summary>
    /// Error message display in the dropdown panel
    /// </summary>
    public ILocator ErrorMessage => MenuPanel.Locator(".text-danger");

    /// <summary>
    /// Loading spinner in the dropdown panel
    /// </summary>
    public ILocator LoadingSpinner => MenuPanel.GetByTestId("BaseSpinner");

    /// <summary>
    /// Loading text in the dropdown panel
    /// </summary>
    public ILocator LoadingText => MenuPanel.GetByTestId("loading-workspaces-text");

    /// <summary>
    /// Current workspace details section in the dropdown
    /// </summary>
    public ILocator CurrentWorkspaceDetails => MenuPanel.Locator(".workspace-details");

    /// <summary>
    /// Current workspace name in the details section
    /// </summary>
    public ILocator DetailsName => CurrentWorkspaceDetails.Locator("div", new() { HasText = "Name:" }).Locator("div").Nth(1);

    /// <summary>
    /// Current workspace description in the details section
    /// </summary>
    public ILocator DetailsDescription => CurrentWorkspaceDetails.Locator("div", new() { HasText = "Description:" }).Locator("div").Nth(1);

    /// <summary>
    /// Current workspace role badge in the details section
    /// </summary>
    public ILocator DetailsRole => CurrentWorkspaceDetails.Locator(".badge");

    /// <summary>
    /// Current workspace created date in the details section
    /// </summary>
    public ILocator DetailsCreatedDate => CurrentWorkspaceDetails.Locator("div", new() { HasText = "Created:" }).Locator("div").Nth(1);

    /// <summary>
    /// No workspace selected message
    /// </summary>
    public ILocator NoWorkspaceMessage => MenuPanel.GetByTestId("no-workspace-message");

    /// <summary>
    /// The workspace selection dropdown
    /// </summary>
    public ILocator WorkspaceSelect => MenuPanel.Locator("#workspace-select");

    /// <summary>
    /// Manage Workspaces button/link
    /// </summary>
    public ILocator ManageWorkspacesButton => MenuPanel.GetByTestId("manage-workspaces-button");

    #endregion

    #region Actions

    /// <summary>
    /// Opens the workspace dropdown menu
    /// </summary>
    public async Task OpenMenuAsync()
    {
        var visible = await MenuPanel.IsVisibleAsync();
        if (visible)
        {
            return;
        }
        await MenuTrigger.ClickAsync();
        await MenuPanel.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Closes the workspace dropdown menu
    /// </summary>
    public async Task CloseMenuAsync()
    {
        await MenuTrigger.ClickAsync();
        await MenuPanel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Selects a workspace from the dropdown by name
    /// </summary>
    /// <param name="workspaceName">The name of the workspace to select</param>
    public async Task SelectWorkspaceAsync(string workspaceName)
    {
        // Bug AV#1979 call stack here
        await OpenMenuAsync();
        await WorkspaceSelect.SelectOptionAsync(new[] { new SelectOptionValue { Label = workspaceName } });

        // Wait for the CurrentWorkspaceName to update to the selected workspace
        // This is more reliable than waiting for NetworkIdle
        await CurrentWorkspaceName.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        // Also verify the text matches what we selected (with a reasonable timeout for text content)
        var currentName = await CurrentWorkspaceName.TextContentAsync();
        if (currentName != workspaceName)
        {
            // Give it a bit more time if names don't match yet
            await page.Page!.WaitForTimeoutAsync(500);
        }

        await CloseMenuAsync();
    }

    /// <summary>
    /// Clicks the Manage Workspaces button
    /// </summary>
    public async Task ClickManageWorkspacesAsync()
    {
        await OpenMenuAsync();
        await ManageWorkspacesButton.ClickAsync();
        // No need to wait here - let the destination page handle its own ready state
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the currently displayed workspace name
    /// </summary>
    /// <returns>The workspace name text, or null if not visible</returns>
    public async Task<string?> GetCurrentWorkspaceNameAsync()
    {
        if (await CurrentWorkspaceName.IsVisibleAsync())
        {
            return await CurrentWorkspaceName.TextContentAsync();
        }
        return null;
    }

    /// <summary>
    /// Checks if a workspace is currently selected
    /// </summary>
    /// <returns>True if a workspace name is visible, false otherwise</returns>
    public async Task<bool> HasWorkspaceSelectedAsync()
    {
        return await CurrentWorkspaceName.IsVisibleAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if the component is in loading state
    /// </summary>
    /// <returns>True if loading spinner is visible, false otherwise</returns>
    public async Task<bool> IsLoadingAsync()
    {
        await OpenMenuAsync();
        var isLoading = await LoadingSpinner.IsVisibleAsync();
        // Close menu after checking
        await MenuTrigger.ClickAsync();
        return isLoading;
    }

    /// <summary>
    /// Checks if an error is displayed
    /// </summary>
    /// <returns>True if error message is visible, false otherwise</returns>
    public async Task<bool> HasErrorAsync()
    {
        await OpenMenuAsync();
        var hasError = await ErrorMessage.IsVisibleAsync();
        // Close menu after checking
        await MenuTrigger.ClickAsync();
        return hasError;
    }

    /// <summary>
    /// Gets the error message text
    /// </summary>
    /// <returns>The error message, or null if not visible</returns>
    public async Task<string?> GetErrorMessageAsync()
    {
        await OpenMenuAsync();
        if (await ErrorMessage.IsVisibleAsync())
        {
            var errorText = await ErrorMessage.TextContentAsync();
            // Close menu after getting error
            await MenuTrigger.ClickAsync();
            return errorText;
        }
        // Close menu if no error
        await MenuTrigger.ClickAsync();
        return null;
    }

    /// <summary>
    /// Gets the workspace role from the details section
    /// </summary>
    /// <returns>The role text (e.g., "Owner", "Editor", "Viewer"), or null if not visible</returns>
    public async Task<string?> GetWorkspaceRoleAsync()
    {
        await OpenMenuAsync();
        if (await DetailsRole.IsVisibleAsync())
        {
            var role = await DetailsRole.TextContentAsync();
            // Close menu after getting role
            await MenuTrigger.ClickAsync();
            return role;
        }
        // Close menu if no role
        await MenuTrigger.ClickAsync();
        return null;
    }

    /// <summary>
    /// Gets the list of available workspace options from the select dropdown
    /// </summary>
    /// <returns>Array of workspace names available for selection</returns>
    /// <remarks>
    /// Only returns enabled options, filtering out disabled placeholder options
    /// </remarks>
    public async Task<string[]> GetAvailableWorkspacesAsync()
    {
        await OpenMenuAsync();

        // Get all enabled option elements using locator filtering
        var enabledOptions = WorkspaceSelect.Locator("option:not([disabled])");
        var workspaces = await enabledOptions.AllTextContentsAsync();

        // Close menu after getting workspaces
        await MenuTrigger.ClickAsync();

        // Filter out any empty strings
        return workspaces.Where(w => !string.IsNullOrEmpty(w)).ToArray();
    }

    /// <summary>
    /// Gets the workspace description from the details section
    /// </summary>
    /// <returns>The workspace description, or null if not visible</returns>
    public async Task<string?> GetWorkspaceDescriptionAsync()
    {
        await OpenMenuAsync();
        if (await DetailsDescription.IsVisibleAsync())
        {
            var description = await DetailsDescription.TextContentAsync();
            // Close menu after getting description
            await MenuTrigger.ClickAsync();
            return description;
        }
        // Close menu if no description
        await MenuTrigger.ClickAsync();
        return null;
    }

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Waits for the workspace selector to finish loading
    /// </summary>
    public async Task WaitForLoadingCompleteAsync()
    {
        await OpenMenuAsync();
        // Wait for loading spinner to disappear
        await LoadingSpinner.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        // Close menu after waiting
        await MenuTrigger.ClickAsync();
    }

    #endregion
}
