using System.Globalization;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for creating and managing transaction splits via the create modal.
/// </summary>
/// <param name="context">Test context providing access to shared test infrastructure.</param>
/// <remarks>
/// Handles all operations related to the transaction creation modal including opening,
/// field verification, form filling, and validation of created transactions.
/// </remarks>
public class TransactionSplitSteps(ITestContext context) : TransactionStepsBase(context)
{
    /// <summary>
    /// When I add a new split with the following details:
    /// </summary>
    /// <remarks>
    /// Expects that we are on the transaction detail page for the transaction we're adding to
    /// </remarks>
    [When("I add a new split with the following details:")]
    public async Task IAddANewSplitWithTheFollowingDetails(DataTable table)
    {
        Assert.That(table.Rows, Has.Count.EqualTo(1), "Adding a single split requires exactly one row.");
        var row = table.Rows.Single();
        var amount = decimal.Parse(row.GetRequired("Amount"), NumberStyles.Number, CultureInfo.InvariantCulture);
        var category = row.TryGetValue("Category", out var value) ? value ?? string.Empty : string.Empty;
        var memo = row.TryGetValue("Memo", out value) ? value ?? string.Empty : string.Empty;

        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.AddSplitAsync(amount, category, memo);
    }

    /// <summary>
    /// Then the following splits are displayed:
    /// </summary>
    [Then("the following splits are displayed:")]
    public async Task TheFollowingSplitsAreDisplayed(DataTable table)
    {
        var expectedSplits = table.Rows.Select(row => new
        {
            Category = row.TryGetValue("Category", out var category) ? category ?? string.Empty : string.Empty,
            Amount = decimal.Parse(row.GetRequired("Amount"), NumberStyles.Number, CultureInfo.InvariantCulture),
            Memo = row.TryGetValue("Memo", out var memo) ? memo ?? string.Empty : string.Empty
        }).ToArray();

        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        var actualSplits = await detailsPage.GetDisplayedSplitsAsync();

        Assert.That(actualSplits, Has.Count.EqualTo(expectedSplits.Length), "Displayed split count differs from the expected count.");
        for (var index = 0; index < expectedSplits.Length; index++)
        {
            var expected = expectedSplits[index];
            var actual = actualSplits[index];
            Assert.That(actual.Category, Is.EqualTo(string.IsNullOrWhiteSpace(expected.Category) ? "(none)" : expected.Category),
                $"Split {index} category differs.");
            Assert.That(ParseDisplayedAmount(actual.Amount), Is.EqualTo(expected.Amount),
                $"Split {index} amount differs.");
            Assert.That(actual.Memo, Is.EqualTo(expected.Memo), $"Split {index} memo differs.");
        }
    }

    /// <summary>
    /// Then the transaction is balanced
    /// </summary>
    /// <remarks>
    /// Checks that the displayed split amounts sum to the transaction amount.
    /// </remarks>
    [Then("the transaction is balanced")]
    public async Task TheTransactionIsBalanced()
    {
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        var transactionAmountText = await detailsPage.GetAmountAsync();
        Assert.That(transactionAmountText, Is.Not.Null.And.Not.Empty, "Transaction amount is not displayed.");

        var transactionAmount = ParseDisplayedAmount(transactionAmountText!);
        var splits = await detailsPage.GetDisplayedSplitsAsync();
        var splitTotal = splits.Sum(split => ParseDisplayedAmount(split.Amount));
        Assert.That(splitTotal, Is.EqualTo(transactionAmount),
            $"Displayed splits total {splitTotal} but transaction amount is {transactionAmount}.");
    }

    private static decimal ParseDisplayedAmount(string amount)
    {
        Assert.That(decimal.TryParse(amount, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"), out var parsed),
            Is.True, $"Could not parse displayed currency amount '{amount}'.");
        return parsed;
    }

}
