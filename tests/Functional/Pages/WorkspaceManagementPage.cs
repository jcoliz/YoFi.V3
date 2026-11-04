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
    public ILocator SettingsCard => Page.Locator("section[aria-labelledby='workspace-settings-heading']");
    public ILocator DeleteCard => Page.Locator("section[aria-labelledby='delete-workspace-heading']");
    public ILocator EditButton => SettingsCard.GetByTestId("edit-workspace-button");
    public ILocator DeleteButton => DeleteCard.GetByTestId("delete-workspace-button");
    public ILocator EditForm => SettingsCard.GetByTestId("edit-form");
    public ILocator EditName => EditForm.GetByTestId("edit-workspace-name");
    public ILocator EditDescription => EditForm.GetByTestId("edit-workspace-description");
    public ILocator DeleteModal => Page.GetByTestId("delete-modal");
    public ILocator DeletingWorkspaceName => DeleteModal.GetByTestId("deleting-workspace-name");
    public ILocator AccessDenied => Page.GetByTestId("access-denied");
    public ILocator AccessDeniedTitle => AccessDenied.GetByTestId("title");

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
        await Assertions.Expect(SettingsCard.GetByRole(AriaRole.Heading)).ToHaveTextAsync("Workspace Settings");
        await Assertions.Expect(DeleteCard.GetByRole(AriaRole.Heading)).ToHaveTextAsync("Delete Workspace");
    }

    public async Task StartEditAsync()
    {
        await EditButton.ClickAsync();
        await EditForm.WaitForAsync();
    }

    public async Task SubmitEditFormAsync(string expectedName)
    {
        await EditForm.GetByTestId("edit-workspace-submit").ClickAsync();
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
        await DeleteModal.GetByTestId("delete-submit-button").ClickAsync();
        await Page.WaitForURLAsync("**/workspaces");
        await new WorkspacesPage(Page).WaitForPageReadyAsync();
    }

    public async Task CancelDeleteAsync()
    {
        await DeleteModal.GetByTestId("delete-cancel-button").ClickAsync();
        await Assertions.Expect(DeleteModal).ToBeHiddenAsync();
    }

    public async Task NavigateAsync(Guid key)
    {
        await Page.GotoAsync($"/workspaces/{key}");
    }

    public async Task AssertAccessDeniedAsync()
    {
        await Assertions.Expect(AccessDenied).ToBeVisibleAsync();
        await Assertions.Expect(AccessDeniedTitle).ToHaveTextAsync("Access denied");
        await Assertions.Expect(AccessDenied).ToContainTextAsync("Contact the workspace owner to request access.");
        await Assertions.Expect(Page.GetByTestId("loading-state")).ToBeHiddenAsync();
        await Assertions.Expect(PageHeading).ToBeHiddenAsync();
        await Assertions.Expect(SettingsCard).ToBeHiddenAsync();
        await Assertions.Expect(DeleteCard).ToBeHiddenAsync();
        await Assertions.Expect(EditButton).ToBeHiddenAsync();
        await Assertions.Expect(DeleteButton).ToBeHiddenAsync();
        await Assertions.Expect(EditForm).ToBeHiddenAsync();
        await Assertions.Expect(DeleteModal).ToBeHiddenAsync();
    }
}
