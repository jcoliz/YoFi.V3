using Microsoft.Playwright;
using System.Text.RegularExpressions;
using YoFi.V3.Tests.Functional.Components;
namespace YoFi.V3.Tests.Functional.Pages;

public class RegisterPage(IPage _page): BasePage(_page)
{
    public ILocator View => Page!.GetByTestId("RegisterForm");
    public ILocator EmailInput => View.GetByTestId("email");
    public ILocator UsernameInput =>  View.GetByTestId("username");
    public ILocator PasswordInput => View.GetByTestId("password");
    public ILocator PasswordAgainInput => View.GetByTestId("password-again");
    public ILocator RegisterButton => View.GetByTestId("Register");
    public ILocator ExpiredIndicator => View.GetByTestId("Expired");
    public ILocator UsedIndicator => View.GetByTestId("Used");
    public ILocator NotFoundIndicator => View.GetByTestId("NotFound");
    public ILocator InvitationOnlyIndicator => View.GetByTestId("InvitationOnly");
    public ILocator ContinueButton => View.GetByTestId("Continue");

    public async Task RegisterAsync(string email, string username, string password)
    {
        await EmailInput.FillAsync(email);
        await UsernameInput.FillAsync(username);
        await PasswordInput.FillAsync(password);
        await PasswordAgainInput.FillAsync(password);

        await SaveScreenshotAsync("Registering");

        await WaitForApi(async () => 
        { 
            await RegisterButton.ClickAsync();
        }, "/api/auth/register*");
    }
}