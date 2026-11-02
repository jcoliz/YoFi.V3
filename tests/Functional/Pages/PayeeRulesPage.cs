using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page Object Model for creating, editing, and viewing payee matching rules.
/// </summary>
public class PayeeRulesPage(IPage page) : BasePage(page)
{
    private ILocator RulesCard => Page.GetByTestId("rules-card");
    private ILocator CreateDialog => Page.GetByTestId("rule-create-dialog");
    private ILocator NewRuleButton => Page.GetByTestId("new-button");
    private ILocator PayeePatternInput => CreateDialog.GetByTestId("payee-pattern-input");
    private ILocator CategoryInput => CreateDialog.GetByTestId("category-input");
    private ILocator SaveButton => CreateDialog.GetByTestId("save-button");
    private ILocator ValidationError => CreateDialog.GetByTestId("validation-error");
    private ILocator EmptyState => RulesCard.GetByTestId("empty-state");
    private ILocator RulesTable => Page.GetByTestId("payee-rules");
    private ILocator EditButton => RulesTable.GetByTestId("edit-button");
    private ILocator EditDialog => Page.GetByTestId("rule-edit-dialog");
    private ILocator EditPayeePatternInput => EditDialog.GetByTestId("payee-pattern-input");
    private ILocator EditCategoryInput => EditDialog.GetByTestId("category-input");
    private ILocator EditSaveButton => EditDialog.GetByTestId("save-button");

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
        await AttemptCreateRuleAsync(payee, category);
        await CreateDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await RulesCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Submits the create form without requiring the rule to be accepted.
    /// </summary>
    public async Task AttemptCreateRuleAsync(string payee, string category)
    {
        await NewRuleButton.ClickAsync();
        await PayeePatternInput.FillAsync(payee);
        await CategoryInput.FillAsync(category);
        await SaveButton.ClickAsync();
    }

    /// <summary>
    /// Edits the only rule in the list and waits for the refreshed list.
    /// </summary>
    public async Task EditRuleAsync(string payee, string category)
    {
        await EditButton.ClickAsync();
        await EditPayeePatternInput.FillAsync(payee);
        await EditCategoryInput.FillAsync(category);
        await EditSaveButton.ClickAsync();
        await EditDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await RulesCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Gets the visible validation error from the create dialog.
    /// </summary>
    public async Task<string> GetValidationErrorAsync()
    {
        await ValidationError.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return await ValidationError.InnerTextAsync();
    }

    /// <summary>
    /// Checks whether the loaded rules list is empty.
    /// </summary>
    public async Task<bool> IsEmptyAsync()
    {
        return await EmptyState.IsVisibleAsync();
    }

    /// <summary>
    /// Gets the displayed category for the rule with the exact payee pattern.
    /// </summary>
    public async Task<string> GetRuleCategoryAsync(string payee)
    {
        var row = RulesTable.GetByRole(AriaRole.Row).Filter(new()
        {
            Has = Page.GetByRole(AriaRole.Cell, new() { Name = payee, Exact = true })
        });
        return (await row.GetByRole(AriaRole.Cell).Nth(1).InnerTextAsync()).Trim();
    }
}
