using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for payee rule operations.
/// </summary>
/// <param name="_context">The test context providing access to test infrastructure.</param>
/// <remarks>
/// </remarks>
public class PayeeRuleSteps(ITestContext _context)
{
    #region GIVEN

    /// <summary>
    /// Given I have the following payee rules:
    /// </summary>
    /// <remarks>
    /// The table should have columns "Payee" and "Category", and optionally "IsRegex".
    /// Each row specifies the payee and the category for the rule to be created.
    /// This step will use the test control API to directly create the payee rules in the system.
    /// </remarks>
    [Given("I have a payee rule:")]
    [Given("I have the following payee rules:")]
    public async Task IHaveTheFollowingPayeeRules(DataTable table)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Given I have a regex payee rule:
    /// </summary>
    [Given("I have a regex payee rule:")]
    public async Task IHaveARegexPayeeRule(DataTable table)
    {
        throw new NotImplementedException();
    }


    #endregion

    #region WHEN

    /// <summary>
    /// When I create a payee rule:
    /// </summary>
    /// <remarks>
    /// The table should have columns "Payee" and "Category".
    /// Each row specifies the payee and the category for the rule to be created.
    /// </remarks>
    [When("I create a payee rule:")]
    public async Task ICreateAPayeeRule(DataTable table)
    {
        var page = _context.GetOrCreatePage<PayeeRulesPage>();
        await page.NavigateAsync();

        foreach (var row in table.Rows)
        {
            await page.CreateRuleAsync(row["Payee"], row["Category"]);
        }
    }

    /// <summary>
    /// When I edit the rule to:
    /// </summary>
    /// <remarks>
    /// The table should have columns "Payee" and "Category".
    /// Each row specifies the new values for the payee rule to be updated.
    /// </remarks>
    [When("I edit the rule to:")]
    public async Task IEditTheRuleTo(DataTable table)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// When I view my payee rules
    /// </summary>
    [When("I view my payee rules")]
    public async Task IViewMyPayeeRules()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// When I attempt to create a payee rule:
    /// </summary>
    /// <remarks>
    /// The table must contain exactly one row with "Payee" and "Category" columns.
    /// </remarks>
    [When("I attempt to create a payee rule:")]
    public async Task IAttemptToCreateAPayeeRule(DataTable table)
    {
        if (table.Rows.Count != 1)
        {
            throw new ArgumentException("Expected exactly one payee rule row.", nameof(table));
        }

        var page = _context.GetOrCreatePage<PayeeRulesPage>();
        await page.NavigateAsync();

        var row = table.Rows.Single();
        await page.AttemptCreateRuleAsync(row["Payee"], row["Category"]);
    }

    /// <summary>
    /// Then I should see the rule for {string1} with category {string2}
    /// </summary>
    [Then("I should see the rule for {payee} with category {category}")]
    public async Task IShouldSeeTheRuleForWithCategory(string payee, string category)
    {
        var page = _context.GetOrCreatePage<PayeeRulesPage>();
        var actualCategory = await page.GetRuleCategoryAsync(payee);
        Assert.That(actualCategory, Is.EqualTo(category),
            $"Rule for '{payee}' should have category '{category}'");
    }

    /// <summary>
    /// Then the rule should not be created
    /// </summary>
    [Then("the rule should not be created")]
    public async Task TheRuleShouldNotBeCreated()
    {
        var page = _context.GetOrCreatePage<PayeeRulesPage>();
        await page.NavigateAsync();
        Assert.That(await page.IsEmptyAsync(), Is.True,
            "The workspace should still have no rules after the rejected creation");
    }

    /// <summary>
    /// Then I should see the expected rules
    /// </summary>
    [Then("I should see the expected rules")]
    public async Task IShouldSeeTheExpectedRules()
    {
        throw new NotImplementedException();
    }

    #endregion
}
