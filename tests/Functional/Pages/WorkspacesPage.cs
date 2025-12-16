using System.Text.RegularExpressions;
using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Components;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page Object Model for the Workspaces management page
/// </summary>
/// <param name="page">The Playwright page instance</param>
/// <remarks>
/// Represents the workspaces management page that provides complete CRUD functionality
/// for managing workspace (tenant) entities.
/// </remarks>
public class WorkspacesPage(IPage page) : BasePage(page)
{
    private static readonly Regex CreateTenantApiRegex = new("/api/Tenant", RegexOptions.Compiled);

    #region Components

    /// <summary>
    /// Workspace selector component at the top of the page
    /// </summary>
    public WorkspaceSelector WorkspaceSelector => new WorkspaceSelector(Page!, Page!.Locator("body"));

    /// <summary>
    /// Error display component for page-level errors
    /// </summary>
    public ErrorDisplay ErrorDisplay => new ErrorDisplay(Page!.Locator("body"));

    #endregion

    #region Page Elements

    /// <summary>
    /// Main page heading
    /// </summary>
    public ILocator PageHeading => Page!.GetByTestId("page-heading");

    /// <summary>
    /// Create Workspace button in the header
    /// </summary>
    public ILocator CreateWorkspaceButton => PageHeading.GetByTestId("create-workspace-button");

    /// <summary>
    /// Loading spinner
    /// </summary>
    public ILocator LoadingSpinner => Page!.GetByTestId("BaseSpinner");

    /// <summary>
    /// Loading text display
    /// </summary>
    public ILocator LoadingText => Page!.GetByTestId("loading-workspaces-text");

    /// <summary>
    /// Loading state container
    /// </summary>
    public ILocator LoadingState => Page!.GetByTestId("loading-state");

    #endregion

    #region Create Form Elements

    /// <summary>
    /// Create form card container
    /// </summary>
    public ILocator CreateFormCard => Page!.GetByTestId("create-form-card");

    /// <summary>
    /// Name input field in create form
    /// </summary>
    public ILocator CreateNameInput => Page!.Locator("#create-name");

    /// <summary>
    /// Description textarea in create form
    /// </summary>
    public ILocator CreateDescriptionInput => Page!.Locator("#create-description");

    /// <summary>
    /// Create button in create form
    /// </summary>
    public ILocator CreateButton => CreateFormCard.GetByTestId("create-submit-button");

    /// <summary>
    /// Cancel button in create form
    /// </summary>
    public ILocator CreateCancelButton => CreateFormCard.GetByTestId("create-cancel-button");

    #endregion

    #region Workspace List Elements

    /// <summary>
    /// Workspaces list container
    /// </summary>
    public ILocator WorkspacesList => Page!.GetByTestId("workspaces-list");

    /// <summary>
    /// Empty state display when no workspaces exist
    /// </summary>
    public ILocator EmptyState => Page!.GetByTestId("empty-state");

    /// <summary>
    /// Empty state create button
    /// </summary>
    public ILocator EmptyStateCreateButton => EmptyState.GetByTestId("create-workspace-button");

    public ILocator EditForm => WorkspacesList.GetByTestId("edit-form");

    #endregion

    #region Delete Modal Elements

    /// <summary>
    /// Delete confirmation modal
    /// </summary>
    public ILocator DeleteModal => Page!.GetByTestId("delete-modal");

    /// <summary>
    /// Delete button in modal
    /// </summary>
    public ILocator DeleteModalButton => DeleteModal.GetByTestId("delete-submit-button");

    /// <summary>
    /// Cancel button in delete modal
    /// </summary>
    public ILocator DeleteModalCancelButton => DeleteModal.GetByTestId("delete-cancel-button");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates to the workspaces page
    /// </summary>
    public async Task NavigateAsync()
    {
        await Page!.GotoAsync("/workspaces");
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion

    #region Create Operations

    /// <summary>
    /// Opens the create workspace form
    /// </summary>
    public async Task OpenCreateFormAsync()
    {
        await CreateWorkspaceButton.ClickAsync();
        await CreateFormCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Creates a new workspace with the given name and optional description
    /// </summary>
    /// <param name="name">The workspace name</param>
    /// <param name="description">Optional workspace description</param>
    public async Task CreateWorkspaceAsync(string name, string? description = null)
    {
        await OpenCreateFormAsync();
        await CreateNameInput.FillAsync(name);
        if (!string.IsNullOrEmpty(description))
        {
            await CreateDescriptionInput.FillAsync(description);
        }
        await ClickCreateButtonAsync();
    }

    public async Task ClickCreateButtonAsync()
    {
        await WaitForApi(async () =>
        {
            await CreateButton.ClickAsync();
        }, CreateTenantApiRegex);
        //?await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Cancels the create workspace form
    /// </summary>
    public async Task CancelCreateAsync()
    {
        await CreateCancelButton.ClickAsync();
        await CreateFormCard.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Workspace Card Helpers

    /// <summary>
    /// Gets a workspace card by workspace key
    /// </summary>
    /// <param name="workspaceKey">The key (GUID) of the workspace</param>
    /// <returns>Locator for the workspace card</returns>
    public ILocator GetWorkspaceCard(string workspaceKey)
    {
        return Page!.GetByTestId($"workspace-card-{workspaceKey}");
    }

    /// <summary>
    /// Gets a workspace card by workspace name (searches through all cards)
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>Locator for the workspace card</returns>
    public ILocator GetWorkspaceCardByName(string workspaceName)
    {
        return WorkspacesList.Locator("[data-test-id^='workspace-card-']").Filter(new() { Has = Page!.GetByTestId("workspace-name").Filter(new() { HasText = workspaceName }) });
    }

    /// <summary>
    /// Checks if a workspace card exists on the page by name
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>True if the workspace card is visible, false otherwise</returns>
    public async Task<bool> HasWorkspaceAsync(string workspaceName)
    {
        return await GetWorkspaceCardByName(workspaceName).IsVisibleAsync();
    }

    /// <summary>
    /// Gets the Edit button for a specific workspace by name
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>Locator for the Edit button</returns>
    public ILocator GetEditButton(string workspaceName)
    {
        return GetWorkspaceCardByName(workspaceName).GetByTestId("edit-workspace-button");
    }

    /// <summary>
    /// Gets the Delete button for a specific workspace by name
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>Locator for the Delete button</returns>
    public ILocator GetDeleteButton(string workspaceName)
    {
        return GetWorkspaceCardByName(workspaceName).GetByTestId("delete-workspace-button");
    }

    /// <summary>
    /// Gets the Delete button for a specific workspace by name
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>Locator for the Delete button</returns>
    public Task<string> GetWorkspaceCardCreatedDate(string workspaceName)
    {
        return GetWorkspaceCardByName(workspaceName).GetByTestId("created-date").InnerTextAsync();
    }


    #endregion

    #region Edit Operations

    /// <summary>
    /// Starts editing a workspace
    /// </summary>
    /// <param name="workspaceName">The name of the workspace to edit</param>
    public async Task StartEditAsync(string workspaceName)
    {
        await GetEditButton(workspaceName).ClickAsync();
        // Wait for edit form to appear in the card
        await EditForm.WaitForAsync();
    }

    /// <summary>
    /// Updates a workspace with new values
    /// </summary>
    /// <param name="originalName">The original workspace name</param>
    /// <param name="newName">The new workspace name</param>
    /// <param name="newDescription">The new workspace description</param>
    public async Task UpdateWorkspaceAsync(string originalName, string newName, string? newDescription = null)
    {
        await StartEditAsync(originalName);
        var card = EditForm;
        var nameInput = card.GetByTestId("edit-workspace-name");
        var descriptionInput = card.GetByTestId("edit-workspace-description");

        await nameInput.FillAsync(newName);
        if (newDescription != null)
        {
            await descriptionInput.FillAsync(newDescription);
        }

        await WaitForApi(async () =>
        {
            await card.GetByTestId("edit-workspace-submit").ClickAsync();
        }, CreateTenantApiRegex);
    }

    /// <summary>
    /// Cancels editing a workspace
    /// </summary>
    /// <param name="workspaceName">The name of the workspace being edited</param>
    public async Task CancelEditAsync(string workspaceName)
    {
        var card = GetWorkspaceCardByName(workspaceName);
        await card.GetByTestId("edit-workspace-cancel").ClickAsync();
    }

    #endregion

    #region Delete Operations

    /// <summary>
    /// Deletes a workspace
    /// </summary>
    /// <param name="workspaceName">The name of the workspace to delete</param>
    public async Task DeleteWorkspaceAsync(string workspaceName)
    {
        await GetDeleteButton(workspaceName).ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await DeleteModalButton.ClickAsync();
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Starts the delete process but doesn't confirm
    /// </summary>
    /// <param name="workspaceName">The name of the workspace to delete</param>
    public async Task StartDeleteAsync(string workspaceName)
    {
        await GetDeleteButton(workspaceName).ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Cancels the delete operation
    /// </summary>
    public async Task CancelDeleteAsync()
    {
        await DeleteModalCancelButton.ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the role badge text for a specific workspace
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>The role text (e.g., "Owner", "Editor", "Viewer")</returns>
    public async Task<string?> GetWorkspaceRoleAsync(string workspaceName)
    {
        var badge = GetWorkspaceCardByName(workspaceName).GetByTestId("workspace-role-badge");
        return await badge.TextContentAsync();
    }

    /// <summary>
    /// Checks if a workspace is marked as current
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>True if the workspace has a "Current" badge, false otherwise</returns>
    public async Task<bool> IsCurrentWorkspaceAsync(string workspaceName)
    {
        var currentBadge = GetWorkspaceCardByName(workspaceName).GetByTestId("current-workspace-badge");
        return await currentBadge.IsVisibleAsync();
    }

    /// <summary>
    /// Gets the count of workspace cards displayed
    /// </summary>
    /// <returns>Number of workspace cards</returns>
    public async Task<int> GetWorkspaceCountAsync()
    {
        return await WorkspacesList.Locator("[data-test-id^='workspace-card-']").CountAsync();
    }

    /// <summary>
    /// Checks if the page is in loading state
    /// </summary>
    /// <returns>True if loading spinner is visible, false otherwise</returns>
    public async Task<bool> IsLoadingAsync()
    {
        return await LoadingSpinner.IsVisibleAsync();
    }

    /// <summary>
    /// Waits for the page to finish loading
    /// </summary>
    public async Task WaitForLoadingCompleteAsync()
    {
        await LoadingSpinner.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Role-based Permission Checks

    /// <summary>
    /// Checks if the Create Workspace button is available for interaction.
    /// </summary>
    /// <returns>True if the button is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// All authenticated users should be able to create workspaces.
    /// </remarks>
    public Task<bool> IsCreateWorkspaceAvailableAsync() => IsAvailableAsync(CreateWorkspaceButton);

    /// <summary>
    /// Checks if the Edit button for a specific workspace is available for interaction.
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>True if the button is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// This button should only be available to users with Editor or Owner roles.
    /// Viewers should not have access to edit workspace details.
    /// </remarks>
    public Task<bool> IsEditAvailableAsync(string workspaceName) => IsAvailableAsync(GetEditButton(workspaceName));

    /// <summary>
    /// Checks if the Delete button for a specific workspace is available for interaction.
    /// </summary>
    /// <param name="workspaceName">The name of the workspace</param>
    /// <returns>True if the button is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// This button should only be available to users with Owner role.
    /// Editors and Viewers should not have access to delete workspaces.
    /// </remarks>
    public Task<bool> IsDeleteAvailableAsync(string workspaceName) => IsAvailableAsync(GetDeleteButton(workspaceName));

    #endregion
}
