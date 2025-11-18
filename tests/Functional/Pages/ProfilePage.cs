using Microsoft.Playwright;
namespace YoFi.V3.Tests.Functional.Pages;

public class ProfilePage(IPage _page): BasePage(_page)
{
    // Main sections
    public ILocator AccountInfoSection => Page!.GetByTestId("AccountInfo");
    public ILocator WorkspaceInfoSection => Page!.GetByTestId("WorkspaceInfo");
    public ILocator EditProfileForm => Page!.GetByTestId("EditProfileForm");
    public ILocator ErrorDisplay => Page!.GetByTestId("ProfileErrors");

    // Account information display
    public ILocator EmailDisplay => AccountInfoSection.GetByTestId("Email");
    public ILocator UsernameDisplay => AccountInfoSection.GetByTestId("Username");

    // Edit profile controls
    public ILocator EditProfileButton => Page!.GetByTestId("EditProfile");
    public ILocator EditEmailInput => EditProfileForm.GetByTestId("EditEmail");
    public ILocator EditUsernameInput => EditProfileForm.GetByTestId("EditUsername");
    public ILocator SaveProfileButton => EditProfileForm.GetByTestId("SaveProfile");
    public ILocator CancelEditButton => EditProfileForm.GetByTestId("CancelEdit");

    // Account action buttons
    public ILocator ChangePasswordButton => Page!.GetByTestId("ChangePassword");
    public ILocator ManageWorkspacesButton => Page!.GetByTestId("ManageWorkspaces");
    public ILocator LogoutButton => Page!.GetByTestId("Logout");

    // Workspace section
    public ILocator WorkspaceDashboardLink => Page!.GetByRole(AriaRole.Link, new() { Name = "Go to Workspace" });

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

    public async Task<bool> HasUpdateProfileOptionsAsync()
    {
        return await EditProfileButton.IsVisibleAsync();
    }

    public async Task<bool> HasWorkspaceInformationAsync()
    {
        return await WorkspaceInfoSection.IsVisibleAsync();
    }

    public async Task StartEditingProfileAsync()
    {
        await SaveScreenshotAsync("Before-edit-profile");
        await EditProfileButton.ClickAsync();
    }

    public async Task<bool> IsInEditModeAsync()
    {
        return await EditProfileForm.IsVisibleAsync();
    }

    public async Task UpdateProfileAsync(string newEmail, string newUsername)
    {
        await EditEmailInput.FillAsync(newEmail);
        await EditUsernameInput.FillAsync(newUsername);

        await SaveScreenshotAsync("Before-save-profile");

        await WaitForApi(async () => 
        { 
            await SaveProfileButton.ClickAsync();
        }, "/api/profile/update*");
    }

    public async Task EnterProfileDetailsAsync(string email, string username)
    {
        await EditEmailInput.FillAsync(email);
        await EditUsernameInput.FillAsync(username);
    }

    public async Task ClickSaveProfileAsync()
    {
        await SaveScreenshotAsync("Before-profile-save-attempt");
        await SaveProfileButton.ClickAsync();
    }

    public async Task CancelEditingAsync()
    {
        await CancelEditButton.ClickAsync();
    }

    public async Task<bool> HasProfileErrorAsync(string expectedError)
    {
        if (!await ErrorDisplay.IsVisibleAsync())
            return false;
            
        var errorText = await ErrorDisplay.InnerTextAsync();
        return errorText.Contains(expectedError);
    }

    public async Task<bool> IsSaveButtonDisabledAsync()
    {
        return await SaveProfileButton.IsDisabledAsync();
    }

    public async Task<bool> AreEditInputsDisabledAsync()
    {
        var emailDisabled = await EditEmailInput.IsDisabledAsync();
        var usernameDisabled = await EditUsernameInput.IsDisabledAsync();
        return emailDisabled && usernameDisabled;
    }

    public async Task<bool> IsLoadingAsync()
    {
        // Check if the form is in loading state
        return await IsSaveButtonDisabledAsync() && await AreEditInputsDisabledAsync();
    }

    public async Task ClearEditFormAsync()
    {
        await EditEmailInput.FillAsync("");
        await EditUsernameInput.FillAsync("");
    }

    public async Task ClickChangePasswordAsync()
    {
        await ChangePasswordButton.ClickAsync();
    }

    public async Task ClickManageWorkspacesAsync()
    {
        await ManageWorkspacesButton.ClickAsync();
    }

    public async Task ClickLogoutAsync()
    {
        await SaveScreenshotAsync("Before-logout");
        await LogoutButton.ClickAsync();
    }

    public async Task NavigateToWorkspaceDashboardAsync()
    {
        await WorkspaceDashboardLink.ClickAsync();
    }

    public async Task<bool> IsOnProfilePageAsync()
    {
        return await AccountInfoSection.IsVisibleAsync() || await EditProfileForm.IsVisibleAsync();
    }

    public async Task<string> GetCurrentEmailValueAsync()
    {
        if (await IsInEditModeAsync())
        {
            return await EditEmailInput.InputValueAsync();
        }
        return await GetEmailAsync();
    }

    public async Task<string> GetCurrentUsernameValueAsync()
    {
        if (await IsInEditModeAsync())
        {
            return await EditUsernameInput.InputValueAsync();
        }
        return await GetUsernameAsync();
    }

    public async Task<bool> HasRequiredFieldValidationAsync()
    {
        // Check for validation on empty fields
        var emailClass = await EditEmailInput.GetAttributeAsync("class");
        var usernameClass = await EditUsernameInput.GetAttributeAsync("class");
        
        return (emailClass?.Contains("is-invalid") ?? false) || 
               (usernameClass?.Contains("is-invalid") ?? false);
    }
}