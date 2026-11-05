using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Settings and deletion for the workspace identified by /workspaces/{key}.
/// </summary>
public class WorkspaceManagementPage(IPage page) : BasePage(page)
{
    public ILocator PageHeading => Page.GetByTestId("page-heading");
    public ILocator Name => Page.GetByTestId("workspace-settings-name");
    public ILocator Description => Page.GetByTestId("workspace-description");
    public ILocator Key => Page.GetByTestId("workspace-key");
    public ILocator SettingsCard => Page.GetByTestId("workspace-settings-card");
    public ILocator SettingsTitle => SettingsCard.GetByTestId("workspace-settings-title");
    public ILocator DeleteCard => Page.GetByTestId("delete-workspace-card");
    public ILocator DeleteTitle => DeleteCard.GetByTestId("delete-workspace-title");
    public ILocator EditButton => SettingsCard.GetByTestId("edit-workspace-button");
    public ILocator DeleteButton => DeleteCard.GetByTestId("delete-workspace-button");
    public ILocator EditForm => SettingsCard.GetByTestId("edit-form");
    public ILocator EditName => EditForm.GetByTestId("edit-workspace-name");
    public ILocator EditDescription => EditForm.GetByTestId("edit-workspace-description");
    public ILocator EditSubmitButton => EditForm.GetByTestId("edit-workspace-submit");
    public ILocator DeleteModal => Page.GetByTestId("delete-modal");
    public ILocator DeleteSubmitButton => DeleteModal.GetByTestId("delete-submit-button");
    public ILocator DeleteCancelButton => DeleteModal.GetByTestId("delete-cancel-button");
    public ILocator DeletingWorkspaceName => DeleteModal.GetByTestId("deleting-workspace-name");
    public ILocator DeleteConfirmationName => DeleteModal.GetByTestId("confirmation-workspace-name");
    public ILocator DeleteConfirmationInput => DeleteModal.GetByTestId("confirmation-name-input");
    public ILocator AccessDenied => Page.GetByTestId("access-denied");
    public ILocator AccessDeniedTitle => AccessDenied.GetByTestId("title");
    public ILocator LoadingState => Page.GetByTestId("loading-state");
    public ILocator SampleScenarioSelector => Page.Locator("#sample-scenario");
    public ILocator LoadSampleDataButton => Page.GetByTestId("load-sample-data-button");

    public ILocator LoadSampleModal => Page.GetByTestId("load-sample-modal");
    public ILocator LoadSampleModalSubmitButton => LoadSampleModal.GetByTestId("load-sample-submit-button");
    public ILocator SampleLoadResult => Page.GetByTestId("sample-load-result");

    public ILocator DownloadSampleOFXButton => Page.GetByTestId("download-sample-ofx-button");

    public async Task WaitForWorkspaceAsync(string name)
    {
        await Assertions.Expect(PageHeading).ToHaveTextAsync(name);
        await Assertions.Expect(Name).ToHaveTextAsync(name);
        await Assertions.Expect(EditButton).ToBeEnabledAsync();
        await Assertions.Expect(DeleteButton).ToBeEnabledAsync();
    }

    public async Task ReloadAsync()
    {
        await Page.ReloadAsync();
    }

    public async Task AssertWorkspaceInformationAsync(string name, Guid key)
    {
        await WaitForWorkspaceAsync(name);
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/workspaces/{key}$"));
        await Assertions.Expect(Key).ToHaveTextAsync(key.ToString());
        await Assertions.Expect(Description).ToBeVisibleAsync();
        await Assertions.Expect(SettingsTitle).ToBeVisibleAsync();
        await Assertions.Expect(DeleteTitle).ToBeVisibleAsync();
    }

    public async Task StartEditAsync()
    {
        await EditButton.ClickAsync();
        await EditForm.WaitForAsync();
    }

    public async Task SubmitEditFormAsync(string expectedName)
    {
        await EditSubmitButton.ClickAsync();
        await Assertions.Expect(EditForm).ToBeHiddenAsync();
        await WaitForWorkspaceAsync(expectedName);
    }

    public async Task UpdateWorkspaceAsync(string name, string? description = null)
    {
        await StartEditAsync();
        await EditName.FillAsync(name);
        if (description != null)
        {
            await EditDescription.FillAsync(description);
        }
        await SubmitEditFormAsync(name);
        if (description != null)
        {
            await Assertions.Expect(Description).ToHaveTextAsync(description);
        }
    }

    public async Task OpenDeleteModalAsync()
    {
        await DeleteButton.ClickAsync();
        await DeleteModal.WaitForAsync();
    }

    public async Task DeleteWorkspaceAsync()
    {
        await OpenDeleteModalAsync();

        // Deletion stays disabled until the exact workspace name is typed
        await Assertions.Expect(DeleteSubmitButton).ToBeDisabledAsync();
        var workspaceName = await DeleteConfirmationName.TextContentAsync() ?? string.Empty;
        await DeleteConfirmationInput.FillAsync(workspaceName);
        await Assertions.Expect(DeleteSubmitButton).ToBeEnabledAsync();

        await DeleteSubmitButton.ClickAsync();
        await Page.WaitForURLAsync("**/workspaces");
        await new WorkspacesPage(Page).WaitForPageReadyAsync();
    }

    public async Task CancelDeleteAsync()
    {
        await DeleteCancelButton.ClickAsync();
        await Assertions.Expect(DeleteModal).ToBeHiddenAsync();
    }

    public async Task NavigateAsync(Guid key)
    {
        await Page.GotoAsync($"/workspaces/{key}");
    }

    public async Task<bool> IsEditAvailableAsync()
    {
        await EditButton.Or(AccessDenied).WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return await IsAvailableAsync(EditButton);
    }

    public async Task<bool> IsDeleteAvailableAsync()
    {
        await DeleteButton.Or(AccessDenied).WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return await IsAvailableAsync(DeleteButton);
    }

    public async Task AssertAccessDeniedAsync()
    {
        await Assertions.Expect(AccessDenied).ToBeVisibleAsync();
        await Assertions.Expect(AccessDeniedTitle).ToBeVisibleAsync();
        await Assertions.Expect(LoadingState).ToBeHiddenAsync();
        await Assertions.Expect(PageHeading).ToBeHiddenAsync();
        await Assertions.Expect(SettingsCard).ToBeHiddenAsync();
        await Assertions.Expect(DeleteCard).ToBeHiddenAsync();
        await Assertions.Expect(EditButton).ToBeHiddenAsync();
        await Assertions.Expect(DeleteButton).ToBeHiddenAsync();
        await Assertions.Expect(EditForm).ToBeHiddenAsync();
        await Assertions.Expect(DeleteModal).ToBeHiddenAsync();
    }

    internal async Task LoadSampleDataAsync(string scenario)
    {
        await SampleScenarioSelector.SelectOptionAsync(
            new SelectOptionValue { Label = scenario });
        await LoadSampleDataButton.ClickAsync();
        await LoadSampleModal.WaitForAsync();
        await LoadSampleModalSubmitButton.ClickAsync();
        await SampleLoadResult.WaitForAsync();
    }

    internal async Task ExpectSampleLoadResultAsync()
    {
        await Assertions.Expect(SampleLoadResult).ToBeVisibleAsync();
    }

    internal async Task<string> DownloadSampleOFXAsync(string directory)
    {
        var downloadTask = Page.WaitForDownloadAsync();
        await DownloadSampleOFXButton.ClickAsync();
        var download = await downloadTask;

        var filePath = Path.Combine(directory, download.SuggestedFilename);
        await download.SaveAsAsync(filePath);

        return filePath;
    }
}
