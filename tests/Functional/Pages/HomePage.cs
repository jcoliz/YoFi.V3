using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace YoFi.V3.Tests.Functional.Pages;

public class HomePage(IPage? _page): BasePage(_page)
{
    public ILocator BrochureSection => Page!.GetByTestId("brochure-section");

    /// <summary>
    /// Waits for the home page to fully load by ensuring the brochure section is visible
    /// </summary>
    public async Task EnsurePageLoaded()
    {
        await BrochureSection.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Waits for the home page to be ready
    /// </summary>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await EnsurePageLoaded();
    }
}
