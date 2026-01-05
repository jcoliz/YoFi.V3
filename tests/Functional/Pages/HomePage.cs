using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page object for the home page.
/// </summary>
public class HomePage(IPage? _page): BasePage(_page)
{
    #region Page Elements

    /// <summary>
    /// Main brochure section on the home page
    /// </summary>
    public ILocator BrochureSection => Page!.GetByTestId("brochure-section");

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Waits for the home page to fully load by ensuring the brochure section is visible
    /// </summary>
    public async Task EnsurePageLoaded(float timeout = 5000)
    {
        await BrochureSection.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
    }

    /// <summary>
    /// Waits for the home page to be ready
    /// </summary>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await EnsurePageLoaded(timeout);
    }

    #endregion
}
