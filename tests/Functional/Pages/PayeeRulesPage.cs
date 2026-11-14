using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page Object Model for creating, editing, deleting, and viewing payee matching rules.
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
    private ILocator RuleRows => RulesTable.Locator("tbody tr");
    private ILocator PayeePatternText => Page.GetByTestId("payee-pattern-text");
    private ILocator RegexBadges => Page.GetByTestId("regex-badge");
    private ILocator EditButton => RulesTable.GetByTestId("edit-button");
    private ILocator EditDialog => Page.GetByTestId("rule-edit-dialog");
    private ILocator EditPayeePatternInput => EditDialog.GetByTestId("payee-pattern-input");
    private ILocator EditCategoryInput => EditDialog.GetByTestId("category-input");
    private ILocator EditSaveButton => EditDialog.GetByTestId("save-button");
    private ILocator DeleteButtons => Page.GetByTestId("delete-button");
    private ILocator DeleteDialog => Page.GetByTestId("delete-rule-modal");
    private ILocator DeleteSubmitButton => DeleteDialog.GetByTestId("delete-submit-button");
    private ILocator SearchFilterBar => Page.GetByTestId("search-filter-bar");
    private ILocator SearchInput => SearchFilterBar.GetByTestId("search-input");
    private ILocator SearchButton => SearchFilterBar.GetByTestId("search-button");

    #region Pagination Elements

    // TODO: Refactor these into a component
    private ILocator PaginationBar => Page.GetByTestId("PaginationBar"); // TODO: Should be pagination-bar
    private ILocator TotalItems => PaginationBar.GetByTestId("totalitems");

    #endregion


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
        await RulesTable.GetByTestId("row-actions-button").ClickAsync();
        await EditButton.ClickAsync();
        await EditPayeePatternInput.FillAsync(payee);
        await EditCategoryInput.FillAsync(category);
        await EditSaveButton.ClickAsync();
        await EditDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await RulesCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Deletes the rule with the exact payee pattern and waits for the refreshed list.
    /// </summary>
    public async Task DeleteRuleAsync(string payee)
    {
        await GetRuleRow(payee).GetByTestId("row-actions-button").ClickAsync();
        await GetRuleRow(payee).Locator(DeleteButtons).ClickAsync();
        await DeleteDialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await DeleteSubmitButton.ClickAsync();
        await DeleteDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await RulesCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Waits until no rule with the exact payee pattern remains in the list.
    /// </summary>
    public Task ExpectNoRuleForAsync(string payee) =>
        Assertions.Expect(GetRuleRow(payee)).ToHaveCountAsync(0);

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
    /// Verifies that the empty state is displayed.
    /// </summary>
    public async Task ExpectEmptyStateAsync() =>
        await Assertions.Expect(EmptyState).ToBeVisibleAsync();

    /// <summary>
    /// Gets the displayed category for the rule with the exact payee pattern.
    /// </summary>
    public async Task<string> GetRuleCategoryAsync(string payee, bool isRegex = false)
    {
        var row = GetRuleRow(payee, isRegex);
        return (await row.GetByRole(AriaRole.Cell).Nth(1).InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Waits until exactly the expected number of rules is displayed.
    /// </summary>
    public Task ExpectRuleCountAsync(int expectedCount) =>
        Assertions.Expect(RuleRows).ToHaveCountAsync(expectedCount);

    /// <summary>
    /// Checks whether the matching rule displays a regex badge.
    /// </summary>
    public Task<bool> IsRegexRuleAsync(string payee, bool isRegex)
    {
        return GetRuleRow(payee, isRegex).Locator(RegexBadges).IsVisibleAsync();
    }

    private ILocator GetRuleRow(string payee, bool? isRegex = null)
    {
        var rows = RuleRows.Filter(new()
        {
            Has = PayeePatternText.And(Page.GetByText(payee, new() { Exact = true }))
        });
        return isRegex switch
        {
            true => rows.Filter(new() { Has = RegexBadges }),
            false => rows.Filter(new() { HasNot = RegexBadges }),
            null => rows
        };
    }

    /// <summary>
    /// Waits until at least one rule with the exact payee pattern is displayed, regardless of regex flag.
    /// </summary>
    public Task ExpectRuleForAsync(string expectedPayee) =>
        Assertions.Expect(GetRuleRow(expectedPayee).First).ToBeVisibleAsync();

    public async Task SearchAsync(string string1)
    {
        await SearchInput.FillAsync(string1);
        await SearchButton.ClickAsync();
    }

    internal async Task<int> GetTotalItemsAsync()
    {
        await TotalItems.WaitForAsync();
        var totalItemsText = await TotalItems.TextContentAsync();
        if (int.TryParse(totalItemsText?.Trim(), out var totalItems))
        {
            return totalItems;
        }
        throw new InvalidOperationException("Unable to retrieve total items count from the pagination bar.");
    }

}
