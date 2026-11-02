using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page Object Model for creating and viewing payee matching rules.
/// </summary>
public class PayeeRulesPage(IPage page) : BasePage(page)
{
    private ILocator RulesCard => Page.GetByTestId("rules-card");
    private ILocator CreateDialog => Page.GetByTestId("rule-create-dialog");

    /// <summary>
    /// Navigates to the payee rules page and waits for the rules to finish loading.
    /// </summary>
    public async Task NavigateAsync()
    {
        await Page.GotoAsync("/payee-rules");
        await RulesCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Creates a substring matching rule and waits for the refreshed list.
    /// </summary>
    public async Task CreateRuleAsync(string payee, string category)
    {
        await Page.GetByTestId("new-button").ClickAsync();
        await CreateDialog.GetByTestId("payee-pattern-input").FillAsync(payee);
        await CreateDialog.GetByTestId("category-input").FillAsync(category);
        await CreateDialog.GetByTestId("save-button").ClickAsync();
        await CreateDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await RulesCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Gets the displayed category for the rule with the exact payee pattern.
    /// </summary>
    public async Task<string> GetRuleCategoryAsync(string payee)
    {
        var row = Page.GetByTestId("payee-rules").GetByRole(AriaRole.Row).Filter(new()
        {
            Has = Page.GetByRole(AriaRole.Cell, new() { Name = payee, Exact = true })
        });
        return (await row.GetByRole(AriaRole.Cell).Nth(1).InnerTextAsync()).Trim();
    }
}
