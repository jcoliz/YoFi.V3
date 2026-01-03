using System.Globalization;
using Microsoft.Playwright;
using NUnit.Framework;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Bank Import functional tests.
/// </summary>
/// <remarks>
/// Inherits from TransactionRecordSteps to reuse user/workspace setup infrastructure.
/// Tests the bank import workflow: upload OFX files, review transactions, manage selections, and import.
/// </remarks>
public abstract class BankImportSteps : TransactionRecordSteps
{
    #region Given Steps

    /// <summary>
    /// Creates an active workspace with Editor role for the current user.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to workspace name. Creates workspace via Test Control API
    /// with Editor role. Stores workspace key and sets current workspace context.
    /// Follows same pattern as GivenUserOwnsAWorkspaceCalled from WorkspaceTenancySteps.
    /// </remarks>
    [Given("I have an active workspace {workspaceName}")]
    protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Get user credentials from dictionary (created by GivenIHaveAnExistingAccount)
        if (!_userCredentials.TryGetValue("I", out var cred))
        {
            throw new InvalidOperationException($"User 'I' credentials not found. Ensure user was created in Background.");
        }

        var request = new Generated.WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Editor"
        };

        Generated.TenantResultDto? result;
        try
        {
            result = await testControlClient.CreateWorkspaceForUserAsync(cred.Username, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating workspace '{fullWorkspaceName}' for user '{cred.Username}': {ex.Message}");
            throw;
        }

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result!.Name] = result.Key;
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);
    }

    /// <summary>
    /// Navigates to the import review page with a workspace selected.
    /// </summary>
    /// <remarks>
    /// Sets up minimal state to be on the import review page: navigates to the page and
    /// selects the current workspace. Does not upload any files.
    /// </remarks>
    [Given("I am on the import review page")]
    protected async Task GivenIAmOnTheImportReviewPage()
    {
        // Given: Get workspace name
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

        // And: Navigate to import page
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the workspace
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);

        // TODO: Do we need to wait for anything here?
    }

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
    [Given("I have existing transactions with external IDs:")]
    [Given("I have these exact transactions already:")]
    protected async Task GivenIHaveExistingTransactionsWithExternalIDs(DataTable table)
    {
        // Given: Get workspace context
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);
        var workspaceKey = _workspaceKeys[workspaceName];

        // And: Get logged in user
        var loggedInUser = GetRequiredFromStore(KEY_LOGGED_IN_AS);

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
        var response = await testControlClient.SeedTransactionsPreciseAsync(
            loggedInUser,
            workspaceKey,
            transactions
        );

        Assert.That(response, Has.Count.EqualTo(table.Rows.Count),
            $"Expected to seed {table.Rows.Count} transactions but seeded {response.Count}");
    }

    #endregion

    #region When Steps

    /// <summary>
    /// Uploads an OFX file from the test sample data directory.
    /// </summary>
    /// <param name="filename">The filename (e.g., "checking-jan-2024.ofx")</param>
    [When("I upload OFX file {filename}")]
    protected async Task WhenIUploadOFXFile(string filename)
    {
        // When: Upload the file
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.UploadFileAsync(filename);

        // And: Wait for upload to complete
        await importPage.WaitForUploadCompleteAsync();

        // And: Ensure the import button is enabled, indicating transactions are loaded
        await importPage.WaitForEnabled(importPage.ImportButton);
    }

    /// <summary>
    /// Clicks the Import button to open the confirmation modal.
    /// </summary>
    [When("I click \"Import\" button")]
    protected async Task WhenIClickImportButton()
    {
        // When: Click the Import button
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.ClickImportButtonAsync();
    }

    /// <summary>
    /// Confirms the import in the modal dialog.
    /// </summary>
    [When("I confirm the import in the modal dialog")]
    protected async Task WhenIConfirmTheImportInTheModalDialog()
    {
        // When: Confirm the import
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.ConfirmImportAsync();
    }

    #endregion

    #region Then Steps

    /// <summary>
    /// Verifies that the import review page displays the expected number of transactions.
    /// </summary>
    /// <param name="count">The expected number of transactions.</param>
    [Then("page should display {count} transactions")]
    protected async Task ThenPageShouldDisplayTransactions(int count)
    {
        // Then: Get the import page
        var importPage = GetOrCreatePage<ImportPage>();

        // And: Verify transaction count
        var actualCount = await importPage.GetTransactionCountAsync();
        Assert.That(actualCount, Is.EqualTo(count),
            $"Expected {count} transactions but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the expected number of transactions are selected by default.
    /// </summary>
    /// <param name="count">The expected number of selected transactions.</param>
    [Then("{count} transactions should be selected by default")]
    protected async Task ThenTransactionsShouldBeSelectedByDefault(int count)
    {
        // Then: Get the import page
        var importPage = GetOrCreatePage<ImportPage>();

        // And: Verify selected count
        var actualCount = await importPage.GetSelectedCountAsync();
        Assert.That(actualCount, Is.EqualTo(count),
            $"Expected {count} transactions to be selected but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the expected number of transactions are deselected by default.
    /// </summary>
    /// <param name="count">The expected number of deselected transactions.</param>
    [Then("{count} transactions should be deselected by default")]
    protected async Task ThenTransactionsShouldBeDeselectedByDefault(int count)
    {
        // Then: Get the import page
        var importPage = GetOrCreatePage<ImportPage>();

        // And: Verify deselected count
        var actualCount = await importPage.GetDeselectedCountAsync();
        Assert.That(actualCount, Is.EqualTo(count),
            $"Expected {count} transactions to be deselected but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the user was redirected to the transactions page.
    /// </summary>
    [Then("I should be redirected to transactions page")]
    protected async Task ThenIShouldBeRedirectedToTransactionsPage()
    {
        // Then: Wait for URL to change to transactions page
        await Page.WaitForURLAsync("**/transactions", new() { Timeout = 5000 });

        // And: Verify we're on transactions page
        var url = Page.Url;
        Assert.That(url, Does.Contain("/transactions"),
            "Should be redirected to transactions page");
    }

    /// <summary>
    /// Verifies that new transactions appear in the transaction list.
    /// </summary>
    /// <param name="count">The expected number of new transactions.</param>
    [Then("I should see {count} new transactions in the transaction list")]
    protected async Task ThenIShouldSeeNewTransactionsInTransactionList(int count)
    {
        // Then: Get the transactions page
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

        // And: Wait for page to load
        await transactionsPage.WaitForPageReadyAsync();
        await transactionsPage.WaitForLoadingCompleteAsync();

        // And: Verify transaction count
        var actualCount = await transactionsPage.GetTransactionCountAsync();
        Assert.That(actualCount, Is.EqualTo(count),
            $"Expected {count} transactions in the list but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the import review queue has been completely cleared.
    /// </summary>
    [Then("import review queue should be completely cleared")]
    protected async Task ThenImportReviewQueueShouldBeCompletelyCleared()
    {
        // Then: Navigate back to import review page
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Verify empty state is displayed
        var isEmpty = await importPage.IsEmptyStateAsync();
        Assert.That(isEmpty, Is.True,
            "Import review queue should be empty after import");
    }

    #endregion
}
