using System.Globalization;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

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
    ///
    /// Table format:
    /// | ExternalId | Date       | Payee         | Amount  |
    /// | FITID-001  | 2024-01-05 | Coffee Shop   | -5.50   |
    ///
    /// Requires Objects
    /// - CurrentWorkspace
    /// - LoggedInAs
    /// </remarks>
    [Given("I have existing transactions with external IDs:")]
    [Given("I have these exact transactions already:")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.LoggedInAs)]
    public async Task GivenIHaveExistingTransactionsWithExternalIDs(DataTable table)
    {
        // Given: Get workspace context
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store. Ensure workspace is set up before calling this step.");
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);

        // And: Get logged in user
        var loggedInUser = _context.ObjectStore.Get<string>(ObjectStoreKeys.LoggedInAs)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.LoggedInAs} not found in object store. Ensure user is logged in before calling this step.");

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

    /// <summary>
    /// Seeds a transaction with specified fields into the current workspace.
    /// </summary>
    /// <param name="transactionTable">DataTable with Field/Value columns containing transaction properties.</param>
    /// <remarks>
    /// Parses transaction data from table, seeds via Test Control API, and stores transaction
    /// details in object store for later verification. Does NOT navigate to transactions page -
    /// use separate navigation step. Default amount is 100.00 if not specified.
    ///
    /// Table format (Field/Value):
    /// | Field    | Value           |
    /// | Payee    | Coffee Shop     |
    /// | Amount   | 5.50            |
    /// | Category | Beverages       |
    /// | Memo     | Morning coffee  |
    ///
    /// Requires Objects
    /// - CurrentWorkspace
    /// - LoggedInAs
    ///
    /// Provides Objects
    /// - TransactionPayee
    /// - TransactionAmount
    /// - TransactionCategory
    /// - TransactionMemo
    /// - TransactionSource (if specified)
    /// - TransactionExternalId (if specified)
    /// - TransactionKey
    /// </remarks>
    [Given("I have a workspace with a transaction:")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.LoggedInAs)]
    [ProvidesObjects(
        ObjectStoreKeys.TransactionPayee,
        ObjectStoreKeys.TransactionAmount,
        ObjectStoreKeys.TransactionCategory,
        ObjectStoreKeys.TransactionMemo,
        ObjectStoreKeys.TransactionKey)]
    public async Task GivenIHaveAWorkspaceWithATransaction(DataTable transactionTable)
    {
        // Given: Parse transaction data from table (Field/Value format)
        var payee = transactionTable.GetKeyValue("Payee");

        // And: Parse optional fields with defaults
        transactionTable.TryGetKeyValue("Amount", out var amountStr);
        var amount = decimal.Parse(amountStr ?? "100.00");

        transactionTable.TryGetKeyValue("Memo", out var memo);
        transactionTable.TryGetKeyValue("Source", out var source);
        transactionTable.TryGetKeyValue("ExternalId", out var externalId);
        transactionTable.TryGetKeyValue("Category", out var category);

        // And: Get workspace context
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store. Ensure workspace is set up before calling this step.");
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);

        // And: Get logged in user
        var loggedInUser = _context.ObjectStore.Get<string>(ObjectStoreKeys.LoggedInAs)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.LoggedInAs} not found in object store. Ensure user is logged in before calling this step.");

        // And: Seed transaction via test control API with specific payee
        var seedRequest = new Generated.TransactionSeedRequest
        {
            Count = 1,
            PayeePrefix = payee, // Use payee as prefix for single transaction
            Memo = memo,
            Source = source,
            ExternalId = externalId,
            Category = category
        };

        var seededTransactions = await _context.TestControlClient.SeedTransactionsAsync(
            loggedInUser,
            workspaceKey,
            seedRequest
        );

        // And: Get the actual seeded transaction data from the response
        var seededTransaction = seededTransactions.First();
        var actualPayee = seededTransaction.Payee;
        var actualAmount = seededTransaction.Amount;
        var actualCategory = seededTransaction.Category;
        var actualMemo = seededTransaction.Memo;

        // And: Store actual transaction data for verification (from seeded response, not input table)
        _context.ObjectStore.Add(ObjectStoreKeys.TransactionPayee, actualPayee);
        _context.ObjectStore.Add(ObjectStoreKeys.TransactionAmount, actualAmount.ToString("F2"));
        _context.ObjectStore.Add(ObjectStoreKeys.TransactionCategory, actualCategory);
        _context.ObjectStore.Add(ObjectStoreKeys.TransactionMemo, actualMemo);
        if (!string.IsNullOrEmpty(source))
            _context.ObjectStore.Add(ObjectStoreKeys.TransactionSource, source);
        if (!string.IsNullOrEmpty(externalId))
            _context.ObjectStore.Add(ObjectStoreKeys.TransactionExternalId, externalId);

        // And: Store the transaction key for later reference
        _context.ObjectStore.Add(ObjectStoreKeys.TransactionKey, seededTransaction.Key.ToString());
    }

    /// <summary>
    /// Sets up a logged-in user with Editor role, creates a transaction, and navigates to its details page.
    /// </summary>
    /// <param name="transactionTable">DataTable with Payee (required), and optional Amount, Memo, Source, ExternalId, Category.</param>
    /// <remarks>
    /// Complete setup for testing transaction details page. Seeds transaction, navigates to transactions
    /// page, clicks on transaction row to reach details page, and waits for page to be ready.
    /// Uses TransactionListSteps for navigation operations.
    ///
    /// Requires Objects
    /// - CurrentWorkspace
    /// - LoggedInAs
    ///
    /// Provides Objects
    /// - TransactionPayee
    /// - TransactionAmount
    /// - TransactionCategory
    /// - TransactionMemo
    /// - TransactionSource (if specified)
    /// - TransactionExternalId (if specified)
    /// - TransactionKey
    /// - EditMode ("TransactionDetailsPage")
    /// </remarks>
    [Given("I am viewing the details page for a transaction with:")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.LoggedInAs)]
    [ProvidesObjects(
        ObjectStoreKeys.TransactionPayee,
        ObjectStoreKeys.TransactionAmount,
        ObjectStoreKeys.TransactionCategory,
        ObjectStoreKeys.TransactionMemo,
        ObjectStoreKeys.TransactionKey,
        ObjectStoreKeys.EditMode)]
    public async Task GivenIAmViewingTheDetailsPageForATransactionWith(DataTable transactionTable)
    {
        // Given: Seed the transaction using the existing step
        await GivenIHaveAWorkspaceWithATransaction(transactionTable);

        // And: Create TransactionListSteps for navigation
        var listSteps = new TransactionListSteps(_context);

        // And: Navigate to transactions page with workspace selected
        await listSteps.GivenIAmOnTheTransactionsPage();

        // When: Click on the transaction row to navigate to details page
        await listSteps.WhenIClickOnTheTransactionRow();

        // And: Wait for the transaction details page to be ready
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.WaitForPageReadyAsync();

        // And: Mark that we're in transaction details page mode
        _context.ObjectStore.Add(ObjectStoreKeys.EditMode, "TransactionDetailsPage");
    }

    /// <summary>
    /// Sets up a logged-in user with Editor role, creates a transaction, and navigates to its details page.
    /// </summary>
    /// <remarks>
    /// Simplified version without DataTable parameter. Seeds a basic transaction and navigates to the details page.
    /// </remarks>
    [Given("I am viewing the details page for a transaction")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.LoggedInAs)]
    [ProvidesObjects(
        ObjectStoreKeys.TransactionPayee,
        ObjectStoreKeys.TransactionAmount,
        ObjectStoreKeys.TransactionCategory,
        ObjectStoreKeys.TransactionMemo,
        ObjectStoreKeys.TransactionKey,
        ObjectStoreKeys.EditMode)]
    public async Task GivenIAmViewingTheDetailsPageForATransaction()
    {
        // Given: Create a basic transaction DataTable
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Test Transaction"],
            ["Amount", "100.00"]
        );

        // When: Use existing step to seed transaction and navigate to details page
        await GivenIAmViewingTheDetailsPageForATransactionWith(table);
    }

    #endregion
}
