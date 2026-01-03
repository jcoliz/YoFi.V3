using System.Globalization;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for transaction operations in composition architecture.
/// </summary>
/// <param name="_context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Provides transaction-related operations for functional tests using composition pattern.
/// Methods handle transaction seeding with specific data for testing scenarios.
/// </remarks>
public class TransactionSteps(ITestContext _context)
{
    /// <summary>
    /// Seeds existing transactions with specific external IDs to enable duplicate detection testing.
    /// </summary>
    /// <param name="table">DataTable with columns: ExternalId, Date, Payee, Amount</param>
    /// <remarks>
    /// Creates transactions via Test Control API with the specified External IDs.
    /// These transactions will be matched against OFX FITIDs during import to test duplicate detection.
    /// Table format:
    /// | ExternalId | Date       | Payee         | Amount  |
    /// | FITID-001  | 2024-01-05 | Coffee Shop   | -5.50   |
    /// </remarks>
    // [Given("I have existing transactions with external IDs:")]
    // [Given("I have these exact transactions already:")]
    public async Task GivenIHaveExistingTransactionsWithExternalIDs(DataTable table)
    {
        // Given: Get workspace context
        var workspaceName = _context.ObjectStore.Get<string>("CurrentWorkspaceName")
            ?? throw new InvalidOperationException("CurrentWorkspaceName not found in object store");
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);

        // And: Get logged in user
        var loggedInUser = _context.ObjectStore.Get<string>("LoggedInAs")
            ?? throw new InvalidOperationException("LoggedInAs not found in object store");

        // And: Convert datatable to TransactionEditDtos
        var transactions = table.Rows.Select(row => new Generated.TransactionEditDto
        {
            ExternalId = row["ExternalId"],
            Date = DateTimeOffset.Parse(row["Date"], CultureInfo.InvariantCulture),
            Payee = row["Payee"],
            Amount = decimal.Parse(row["Amount"], CultureInfo.InvariantCulture),
            Memo = string.Empty,
            Source = "OFX",
            Category = string.Empty
        }).ToList();

        // And: Seed transactions via test control API in a single bulk operation
        var response = await _context.TestControlClient.SeedTransactionsPreciseAsync(
            loggedInUser,
            workspaceKey,
            transactions
        );

        NUnit.Framework.Assert.That(response, NUnit.Framework.Has.Count.EqualTo(table.Rows.Count),
            $"Expected to seed {table.Rows.Count} transactions but seeded {response.Count}");
    }
}
