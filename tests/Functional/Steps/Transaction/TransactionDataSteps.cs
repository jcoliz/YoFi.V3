using System.Globalization;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Generated;
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
        }).ToArray();

        var seedRequest = new Generated.CollectionRequestOfTransactionEditDto
        {
            Items = transactions
        };

        // And: Seed transactions via test control API in a single bulk operation
        ICollection<TransactionResultDto>? seededTransactions = null;
        try
        {
            seededTransactions = await _context.TestControlClient.SeedTransactionsPreciseAsync(
                loggedInUser,
                workspaceKey,
                seedRequest
            );
        }
        catch (ApiException<ProblemDetails> ex)
        {
            Assert.Fail("Failed to seed transaction via Test Control API: " + ex.Result.Detail);
        }

        Assert.That(seededTransactions, Has.Count.EqualTo(table.Rows.Count),
            $"Expected to seed {table.Rows.Count} transactions but seeded {seededTransactions.Count}");
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
        var seedRequest = new Generated.TransactionEditDto
        {
            Payee = payee,
            Memo = memo,
            Source = source,
            ExternalId = externalId,
            Category = category,
            Date = DateTimeOffset.UtcNow,
            Amount = amount
        };

        ICollection<TransactionResultDto>? seededTransactions = null;
        try
        {
            seededTransactions = await _context.TestControlClient.SeedTransactionsPreciseAsync(
                            loggedInUser,
                            workspaceKey,
                            new Generated.CollectionRequestOfTransactionEditDto { Items = new[] { seedRequest } }
                    );
        }
        catch (ApiException<ProblemDetails> ex)
        {
            Assert.Fail("Failed to seed transaction via Test Control API: " + ex.Result.Detail);
        }

        // And: Get the actual seeded transaction data from the response
        var seededTransaction = seededTransactions!.First();
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
        await listSteps.IAmOnTheTransactionsPage();

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

    /// <summary>
    /// Given I have 5 existing transactions in my workspace
    /// </summary>
    [Given("I have {count} existing transactions in my workspace")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.LoggedInAs)]
    [ProvidesObjects(ObjectStoreKeys.ExistingTransactionKeys, ObjectStoreKeys.ExistingTransactions)]
    public async Task IHaveSomeExistingTransactionsInMyWorkspace(int count)
    {
        // Get current workspace and logged in user
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store. Ensure workspace is set up before calling this step.");
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);

        var loggedInUser = _context.ObjectStore.Get<string>(ObjectStoreKeys.LoggedInAs)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.LoggedInAs} not found in object store. Ensure user is logged in before calling this step.");

        // Seed count transactions into the current workspace
        var transactions = await _context.TestControlClient.SeedTransactionsAsync(
            loggedInUser,
            workspaceKey,
            new Generated.TransactionSeedRequest
            {
                Count = count,
                PayeePrefix = "Existing Transaction ",
                Source = "Seeding"
            }
        );

        // Store these transactions for later verification
        var transactionKeys = transactions.Select(t => t.Key).ToList();
        _context.ObjectStore.Add(ObjectStoreKeys.ExistingTransactionKeys, transactionKeys);
        _context.ObjectStore.Add(ObjectStoreKeys.ExistingTransactions, transactions);
    }

    /// <summary>
    /// Then I should see only the 5 original transactions
    /// </summary>
    // TODO: Move to transaction list steps
    [Then("I should see only the original transactions")]
    [RequiresObjects(ObjectStoreKeys.ExistingTransactionKeys)]
    public async Task IShouldSeeOnlyTheOriginalTransactions()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // Retrieve the original transaction keys from object store
        var expected = _context.ObjectStore.Get<List<Guid>>(ObjectStoreKeys.ExistingTransactionKeys);

        // Get the transaction rows currently displayed
        var actual = await transactionsPage.GetTransactionRowKeys();

        // Verify that only the original transactions are visible in the transaction list
        Assert.That(expected, Is.EquivalentTo(actual),
            "The transaction list should display only the original transactions seeded earlier.");
    }

    /// <summary>
    /// Then the uploaded transactions should not appear
    /// </summary>
    [Then("the uploaded transactions should not appear")]
    [RequiresObjects(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task TheUploadedTransactionsShouldNotAppear()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // Retrieve the uploaded transaction payees from object store
        var uploadedPayees = _context.ObjectStore.Get<List<string>>(ObjectStoreKeys.UploadedTransactionPayees);

        // Get the payees of the transaction rows currently displayed
        var displayedPayees = await transactionsPage.GetTransactionRowPayees();

        // Verify that none of the uploaded transactions are visible in the transaction list
        // Check for zero intersection between uploaded payees and displayed payees
        var intersection = uploadedPayees.Intersect(displayedPayees).ToList();
        Assert.That(intersection, Is.Empty,
            $"Uploaded transactions should not appear in the transaction list, but found: {string.Join(", ", intersection)}");
    }

    /// <summary>
    /// Then the uploaded transactions should appear
    /// </summary>
    [Then("the uploaded transactions should appear")]
    [Then("they are the transactions uploaded earlier")]
    [RequiresObjects(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task TheUploadedTransactionsShouldAppear()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // Retrieve the uploaded transaction payees from object store
        var uploadedPayees = _context.ObjectStore.Get<List<string>>(ObjectStoreKeys.UploadedTransactionPayees);

        // Get the payees of the transaction rows currently displayed
        var displayedPayees = await transactionsPage.GetTransactionRowPayees();

        // Verify that all of the uploaded transactions are visible in the transaction list
        var missingPayees = uploadedPayees.Except(displayedPayees).ToList();
        Assert.That(missingPayees, Is.Empty,
            $"All uploaded transactions should appear in the transaction list, but missing: {string.Join(", ", missingPayees)}");
    }
}

