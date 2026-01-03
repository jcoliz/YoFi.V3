using System.Globalization;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for transaction test data setup operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Provides transaction data seeding operations for functional tests.
/// Parallel to WorkspaceDataSteps, handles transaction-specific test data setup
/// via Test Control API.
/// </remarks>
public class TransactionDataSteps(ITestContext context) : TransactionStepsBase(context)
{
    #region Steps: GIVEN

    /// <summary>
    /// Seeds existing transactions with specific external IDs to enable duplicate detection testing.
    /// </summary>
    /// <param name="table">DataTable with columns: ExternalId, Date, Payee, Amount</param>
    /// <remarks>
    /// Creates transactions via Test Control API with the specified External IDs.
    /// These transactions will be matched against OFX FITIDs during import to test duplicate detection.
    /// Requires CurrentWorkspaceName and LoggedInAs to be set in object store by prior steps.
    /// Table format:
    /// | ExternalId | Date       | Payee         | Amount  |
    /// | FITID-001  | 2024-01-05 | Coffee Shop   | -5.50   |
    /// </remarks>
    [Given("I have existing transactions with external IDs:")]
    [Given("I have these exact transactions already:")]
    public async Task GivenIHaveExistingTransactionsWithExternalIDs(DataTable table)
    {
        // Given: Get workspace context
        var workspaceName = _context.ObjectStore.Get<string>("CurrentWorkspaceName")
            ?? throw new InvalidOperationException("CurrentWorkspaceName not found in object store. Ensure workspace is set up before calling this step.");
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);

        // And: Get logged in user
        var loggedInUser = _context.ObjectStore.Get<string>("LoggedInAs")
            ?? throw new InvalidOperationException("LoggedInAs not found in object store. Ensure user is logged in before calling this step.");

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

        Assert.That(response, Has.Count.EqualTo(table.Rows.Count),
            $"Expected to seed {table.Rows.Count} transactions but seeded {response.Count}");
    }

    #endregion
}
