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
public partial class WorkspacesPage(IPage page) : BasePage(page)
{
    [GeneratedRegex("/api/Tenant")]
    private static partial Regex TenantsApiRegex();

    [GeneratedRegex(@"/api/Tenant/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex SingleTenantApiRegex();

    #region Components

    /// <summary>
    /// Workspace selector component at the top of the page
    /// </summary>
    public WorkspaceSelector WorkspaceSelector => new WorkspaceSelector(this, Page!.Locator("body"));

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
    /// Navigates to the workspaces page and waits for it to be ready
    /// </summary>
    public async Task NavigateAsync()
    {
        await WaitForApi(async () =>
        {
            await Page!.GotoAsync("/workspaces");
        }, TenantsApiRegex());

        // Wait for the page to be fully ready with workspace cards rendered
        await WaitForPageReadyAsync();
    }

    #endregion

    #region Create Operations - Single Actions

    /// <summary>
    /// Opens the create workspace form
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when you need fine-grained control over the create workflow,
    /// such as testing form validation or cancellation flows.
    /// </remarks>
    public async Task OpenCreateFormAsync()
    {
        await CreateWorkspaceButton.ClickAsync();
        await CreateFormCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Fills the workspace name field in the create form
    /// </summary>
    /// <param name="name">The workspace name</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreateNameAsync(string name)
    {
        await CreateNameInput.FillAsync(name);
    }

    /// <summary>
    /// Fills the workspace description field in the create form
    /// </summary>
    /// <param name="description">The workspace description</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreateDescriptionAsync(string description)
    {
        await CreateDescriptionInput.FillAsync(description);
    }

    /// <summary>
    /// Clicks the create button and waits for the create workspace API call
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when you need to control when the form is submitted,
    /// such as after taking a screenshot or checking form state.
    /// </remarks>
    public async Task SubmitCreateFormAsync()
    {
        await WaitForApi(async () =>
        {
            await CreateButton.ClickAsync();
        }, TenantsApiRegex());

        // Wait for the loading spinner to disappear, indicating UI has updated
        await WaitForLoadingCompleteAsync();
    }

    /// <summary>
    /// Cancels the create workspace form
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when testing cancel workflows.
    /// </remarks>
    public async Task CancelCreateAsync()
    {
        await CreateCancelButton.ClickAsync();
        await CreateFormCard.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Create Operations - Common Workflows

    /// <summary>
    /// Creates a new workspace with the given name and optional description
    /// </summary>
    /// <param name="name">The workspace name</param>
    /// <param name="description">Optional workspace description</param>
    /// <remarks>
    /// High-level workflow method for the common "happy path" create scenario.
    /// For fine-grained control (e.g., testing validation, cancellation, or taking screenshots mid-flow),
    /// use the individual action methods: OpenCreateFormAsync, FillCreateNameAsync,
    /// FillCreateDescriptionAsync, and SubmitCreateFormAsync.
    /// </remarks>
    public async Task CreateWorkspaceAsync(string name, string? description = null)
    {
        await OpenCreateFormAsync();
        await FillCreateNameAsync(name);
        if (!string.IsNullOrEmpty(description))
        {
            await FillCreateDescriptionAsync(description);
        }
        await SubmitCreateFormAsync();
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

    #region Edit Operations - Single Actions

    /// <summary>
    /// Opens the edit form for a workspace
    /// </summary>
    /// <param name="workspaceName">The name of the workspace to edit</param>
    /// <remarks>
    /// Single action method. Use this when you need fine-grained control over the edit workflow,
    /// such as testing form validation, checking intermediate states, or cancellation flows.
    /// </remarks>
    public async Task StartEditAsync(string workspaceName)
    {
        await GetEditButton(workspaceName).ClickAsync();
        // Wait for edit form to appear in the card
        await EditForm.WaitForAsync();
    }

    /// <summary>
    /// Fills the workspace name field in the edit form
    /// </summary>
    /// <param name="newName">The new workspace name</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form updates or validation.
    /// Must be called after StartEditAsync.
    /// </remarks>
    public async Task FillEditNameAsync(string newName)
    {
        var nameInput = EditForm.GetByTestId("edit-workspace-name");
        await nameInput.FillAsync(newName);
    }

    /// <summary>
    /// Fills the workspace description field in the edit form
    /// </summary>
    /// <param name="newDescription">The new workspace description</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form updates or validation.
    /// Must be called after StartEditAsync.
    /// </remarks>
    public async Task FillEditDescriptionAsync(string newDescription)
    {
        var descriptionInput = EditForm.GetByTestId("edit-workspace-description");
        await descriptionInput.FillAsync(newDescription);
    }

    /// <summary>
    /// Clicks the update button and waits for the update workspace API call
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when you need to control when the form is submitted,
    /// such as after taking a screenshot or simulating network conditions.
    /// Must be called after StartEditAsync.
    /// </remarks>
    public async Task SubmitEditFormAsync()
    {
        await WaitForApi(async () =>
        {
            await EditForm.GetByTestId("edit-workspace-submit").ClickAsync();
        }, TenantsApiRegex());

        // Wait for the loading spinner to disappear, indicating UI has updated
        await WaitForLoadingCompleteAsync();
    }

    /// <summary>
    /// Cancels editing a workspace
    /// </summary>
    /// <param name="workspaceName">The name of the workspace being edited</param>
    /// <remarks>
    /// Single action method. Use this when testing cancel workflows.
    /// </remarks>
    public async Task CancelEditAsync(string workspaceName)
    {
        var card = GetWorkspaceCardByName(workspaceName);
        await card.GetByTestId("edit-workspace-cancel").ClickAsync();
    }

    #endregion

    #region Edit Operations - Common Workflows

    /// <summary>
    /// Updates a workspace with new values
    /// </summary>
    /// <param name="originalName">The original workspace name</param>
    /// <param name="newName">The new workspace name</param>
    /// <param name="newDescription">The new workspace description</param>
    /// <remarks>
    /// High-level workflow method for the common "happy path" update scenario.
    /// For fine-grained control (e.g., testing validation, cancellation, network errors, or taking screenshots mid-flow),
    /// use the individual action methods: StartEditAsync, FillEditNameAsync,
    /// FillEditDescriptionAsync, and SubmitEditFormAsync.
    /// </remarks>
    public async Task UpdateWorkspaceAsync(string originalName, string newName, string? newDescription = null)
    {
        await StartEditAsync(originalName);
        await FillEditNameAsync(newName);
        if (newDescription != null)
        {
            await FillEditDescriptionAsync(newDescription);
        }
        await SubmitEditFormAsync();
    }

    #endregion

    #region Delete Operations - Single Actions

    /// <summary>
    /// Opens the delete confirmation modal for a workspace
    /// </summary>
    /// <param name="workspaceName">The name of the workspace to delete</param>
    /// <remarks>
    /// Single action method. Use this when you need fine-grained control over the delete workflow,
    /// such as testing modal display, reading deletion warnings, or cancellation flows.
    /// </remarks>
    public async Task OpenDeleteModalAsync(string workspaceName)
    {
        await GetDeleteButton(workspaceName).ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Clicks the delete button in the modal and waits for the delete workspace API call
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when you need to control when the deletion is confirmed,
    /// such as after taking a screenshot or checking modal content.
    /// Must be called after OpenDeleteModalAsync.
    /// </remarks>
    public async Task ConfirmDeleteAsync()
    {
        await WaitForApi(async () =>
        {
            await DeleteModalButton.ClickAsync();
        }, SingleTenantApiRegex());

        // Wait for the loading spinner to disappear, indicating UI has updated
        await WaitForLoadingCompleteAsync();
    }

    /// <summary>
    /// Cancels the delete operation by closing the modal
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when testing cancel workflows.
    /// </remarks>
    public async Task CancelDeleteAsync()
    {
        await DeleteModalCancelButton.ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Delete Operations - Common Workflows

    /// <summary>
    /// Deletes a workspace
    /// </summary>
    /// <param name="workspaceName">The name of the workspace to delete</param>
    /// <remarks>
    /// High-level workflow method for the common "happy path" delete scenario.
    /// For fine-grained control (e.g., testing confirmation modal, cancellation, or taking screenshots),
    /// use the individual action methods: OpenDeleteModalAsync, ConfirmDeleteAsync, and CancelDeleteAsync.
    /// </remarks>
    public async Task DeleteWorkspaceAsync(string workspaceName)
    {
        // AB#1976 Call Stack Here
        await OpenDeleteModalAsync(workspaceName);
        await ConfirmDeleteAsync();
    }

    /// <summary>
    /// Opens the delete modal but doesn't confirm the deletion
    /// </summary>
    /// <param name="workspaceName">The name of the workspace to delete</param>
    /// <remarks>
    /// Convenience method for testing scenarios where the modal is opened but not confirmed.
    /// Equivalent to OpenDeleteModalAsync.
    /// </remarks>
    public async Task StartDeleteAsync(string workspaceName)
    {
        await OpenDeleteModalAsync(workspaceName);
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

    /// <summary>
    /// Waits for the page to be ready by ensuring the workspaces list container is visible
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Use this after navigation to ensure the page has fully loaded and workspace cards are ready.
    /// Waits for either the workspaces list container or empty state to be visible.
    /// </remarks>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        // Wait for loading to complete first
        await WaitForLoadingCompleteAsync();

        // Then wait for either the workspace list or empty state to be visible
        // This ensures Vue has finished rendering the workspace cards
        try
        {
            await WorkspacesList.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
        }
        catch
        {
            // If workspaces list isn't visible, check if empty state is visible
            await EmptyState.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
        }
    }

    /// <summary>
    /// Waits for a workspace card with the specified name to appear in the list
    /// </summary>
    /// <param name="workspaceName">The workspace name to wait for</param>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Use this after create or update operations to ensure the workspace list has been fully rendered
    /// </remarks>
    public async Task WaitForWorkspaceAsync(string workspaceName, float timeout = 5000)
    {
        await GetWorkspaceCardByName(workspaceName).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
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
