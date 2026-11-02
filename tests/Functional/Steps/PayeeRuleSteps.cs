using System.Net;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Infrastructure.TestControl;
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
    [Requires(ObjectStoreKeys.LoggedInAs)]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    [Provides(ObjectStoreKeys.ExpectedPayeeRules)]
    public async Task IHaveTheFollowingPayeeRules(DataTable table)
    {
        var items = table.Rows.Select(row => new PayeeMatchingRuleEditDto()
        {
            PayeePattern = row.GetRequired("Payee"),
            PayeeIsRegex = row.GetOptional<bool>("IsRegex") ?? false,
            Category = row.GetRequired("Category")
        }).ToList();

        Assert.That(items, Is.Not.Empty, "At least one payee rule row is required.");

        // And: Get workspace context
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);

        // And: Get logged in user
        var loggedInUser = _context.ObjectStore.Get<string>(ObjectStoreKeys.LoggedInAs);

        ICollection<PayeeMatchingRuleResultDto>? seeded = null;
        try
        {
            seeded = await _context.TestControlClient.TestControl_SeedPayeeRulesAsync(
                loggedInUser,
                workspaceKey,
                new CollectionRequestOfPayeeMatchingRuleEditDto()
                {
                    Items = items
                }
            );
        }
        catch (ApiException<ProblemDetails> ex)
        {
            Assert.Fail("Failed to seed payee rules via Test Control API: " + ex.Result.Detail);
        }

        Assert.That(seeded, Has.Count.EqualTo(items.Count),
            $"Expected to seed {items.Count} payee rules but seeded {seeded!.Count}");

        _context.ObjectStore.Add(ObjectStoreKeys.ExpectedPayeeRules, seeded);
    }

    /// <summary>
    /// Given I have a regex payee rule:
    /// </summary>
    [Given("I have a regex payee rule:")]
    public async Task IHaveARegexPayeeRule(DataTable table)
    {
        // TODO: DRY with above. Only difference here is that "IsRegex" is set to true for all rows.
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
    /// Exactly one row specifies the new values for the only rule in the list.
    /// </remarks>
    [When("I edit the rule to:")]
    public async Task IEditTheRuleTo(DataTable table)
    {
        // Navigate to the payee rules page before attempting to edit a rule.
        var page = _context.GetOrCreatePage<PayeeRulesPage>();
        await page.NavigateAsync();

        // When we say "the" rule, we are assuming there is exactly one rule to be edited, which should correspond to the single row in the provided table.
        if (table.Rows.Count != 1)
        {
            throw new ArgumentException("Expected exactly one payee rule row.", nameof(table));
        }
        // In the future, if we support picking one rule out of a list, we'll need a way
        // of identifying which rule to edit. For now, we assume there is only one rule and it corresponds to the single row in the table.

        var row = table.Rows.Single();
        // Gherkin.Generator HTML-encodes special characters in table values.
        // TODO: This is a Gherkin.Generator bug. Need to file it there and get it fixed there.
        await page.EditRuleAsync(
            WebUtility.HtmlDecode(row.GetRequired("Payee")),
            WebUtility.HtmlDecode(row.GetRequired("Category")));
    }

    /// <summary>
    /// When I view my payee rules
    /// </summary>
    [When("I view my payee rules")]
    public async Task IViewMyPayeeRules()
    {
        var page = _context.GetOrCreatePage<PayeeRulesPage>();
        await page.NavigateAsync();
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
    [Requires(ObjectStoreKeys.ExpectedPayeeRules)]
    public async Task IShouldSeeTheExpectedRules()
    {
        var page = _context.GetOrCreatePage<PayeeRulesPage>();
        var expectedRules = _context.ObjectStore.Get<ICollection<PayeeMatchingRuleResultDto>>(ObjectStoreKeys.ExpectedPayeeRules);

        await page.ExpectRuleCountAsync(expectedRules.Count);

        foreach (var expectedRule in expectedRules)
        {
            var actualCategory = await page.GetRuleCategoryAsync(expectedRule.PayeePattern, expectedRule.PayeeIsRegex);
            Assert.That(actualCategory, Is.EqualTo(expectedRule.Category),
                $"Rule for '{expectedRule.PayeePattern}' should have category '{expectedRule.Category}'");
            Assert.That(await page.IsRegexRuleAsync(expectedRule.PayeePattern, expectedRule.PayeeIsRegex),
                Is.EqualTo(expectedRule.PayeeIsRegex),
                $"Rule for '{expectedRule.PayeePattern}' should have regex status '{expectedRule.PayeeIsRegex}'");
        }
    }

    #endregion
}
