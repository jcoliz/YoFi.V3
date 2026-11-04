using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Components;

/// <summary>
/// Page Object Model for the WorkspaceSelector component
/// </summary>
/// <param name="parent">The parent locator containing this component</param>
/// <remarks>
/// Represents the workspace/tenant selector component that displays the current workspace
/// and provides functionality to switch between workspaces and manage them.
/// </remarks>
public class WorkspaceSelector(ILocator parent)
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
    /// Highlighted current workspace entry in the dropdown
    /// </summary>
    public ILocator CurrentWorkspaceOption => MenuPanel.GetByTestId("selector-current-workspace");

    /// <summary>
    /// No workspace selected message
    /// </summary>
    public ILocator NoWorkspaceMessage => MenuPanel.GetByTestId("no-workspace-message");

    /// <summary>
    /// Buttons for switching to each non-current workspace
    /// </summary>
    public ILocator WorkspaceOptions => MenuPanel.GetByTestId("workspace-option");

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
    /// <remarks>
    /// Clicking a workspace option closes the dropdown. If the workspace is already
    /// current, the menu is closed without changing the selection.
    /// </remarks>
    public async Task SelectWorkspaceAsync(string workspaceName)
    {
        // Bug AV#1979 call stack here
        await OpenMenuAsync();

        var option = WorkspaceOptions.Filter(new() { HasTextRegex = ExactText(workspaceName) });
        if (await option.CountAsync() == 0
            && await CurrentWorkspaceOption.Filter(new() { HasTextRegex = ExactText(workspaceName) }).IsVisibleAsync())
        {
            await CloseMenuAsync();
            return;
        }

        await option.ClickAsync();
        await MenuPanel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await Assertions.Expect(CurrentWorkspaceName).ToHaveTextAsync(ExactText(workspaceName));
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
    /// Gets the names of all workspaces listed in the dropdown, including the current one
    /// </summary>
    /// <returns>Array of workspace names available for selection</returns>
    public async Task<string[]> GetAvailableWorkspacesAsync()
    {
        await OpenMenuAsync();

        var names = (await CurrentWorkspaceOption.AllTextContentsAsync())
            .Concat(await WorkspaceOptions.AllTextContentsAsync())
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .ToArray();

        // Close menu after getting workspaces
        await MenuTrigger.ClickAsync();

        return names;
    }

    #endregion

    #region Helpers

    private static Regex ExactText(string text) => new($"^\\s*{Regex.Escape(text)}\\s*$");

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
