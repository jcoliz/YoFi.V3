using Gherkin.Generator.Utils;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for bank import operations in composition architecture.
/// </summary>
/// <param name="_context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Provides bank import-related operations for functional tests using composition pattern.
/// Methods handle OFX file upload, import review, and transaction selection verification.
/// </remarks>
public class BankImportSteps(ITestContext _context)
{
    #region Given Steps

    /// <summary>
    /// Navigates to the import review page with the correct workspace selected.
    /// </summary>
    /// <remarks>
    /// Sets up minimal state to be on the import review page: navigates to the page and
    /// selects the current workspace. Does not upload any files.
    ///
    /// Requires Objects
    /// - CurrentWorkspace
    /// </remarks>
    [Given("I am on the import review page")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task GivenIAmOnTheImportReviewPage()
    {
        // Given: Get workspace name
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");

        // And: Navigate to import page
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the workspace
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
    }

    #endregion

    #region When Steps

    /// <summary>
    /// Uploads an OFX file from the test sample data directory.
    /// </summary>
    /// <param name="filename">The filename (e.g., "checking-jan-2024.ofx")</param>
    [When("I upload OFX file {filename}")]
    public async Task WhenIUploadOFXFile(string filename)
    {
        // When: Upload the file
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.UploadFileAsync(filename);

        // And: Wait for upload to complete
        await importPage.WaitForUploadCompleteAsync();

        // And: Ensure the import button is enabled, indicating transactions are loaded
        await importPage.WaitForEnabled(importPage.ImportButton);
    }

    #endregion

    #region Then Steps

    /// <summary>
    /// Verifies that the import review page displays the expected number of transactions.
    /// </summary>
    /// <param name="count">The expected number of transactions.</param>
    [Then("page should display {count} transactions")]
    public async Task ThenPageShouldDisplayTransactions(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify transaction count
        var actualCount = await importPage.GetTransactionCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the expected number of transactions are selected by default.
    /// </summary>
    /// <param name="count">The expected number of selected transactions.</param>
    [Then("{count} transactions should be selected by default")]
    public async Task ThenTransactionsShouldBeSelectedByDefault(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify selected count
        var actualCount = await importPage.GetSelectedCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions to be selected but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the expected number of transactions are deselected by default.
    /// </summary>
    /// <param name="count">The expected number of deselected transactions.</param>
    [Then("{count} transactions should be deselected by default")]
    public async Task ThenTransactionsShouldBeDeselectedByDefault(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify deselected count
        var actualCount = await importPage.GetDeselectedCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions to be deselected but found {actualCount}");
    }

    #endregion

    #region NEW steps from feature file

    /// <summary>
    /// Sets up import review state with specified number of transactions ready for import.
    /// </summary>
    /// <param name="count">Number of transactions to seed in import review queue.</param>
    /// <param name="selectedCount">Number of transactions to mark as selected.</param>
    /// <remarks>
    /// Seeds import review transactions via Test Control API (SeedImportReviewTransactions endpoint).
    /// Uses deterministic test data generation to create predictable transaction data.
    /// This approach is faster and more reliable than uploading OFX files through the UI.
    ///
    /// Requires Objects
    /// - LoggedInAs (username from auth)
    /// - CurrentWorkspace (workspace name)
    /// </remarks>
    [Given("There are {count} transactions ready for import review, with {selectedCount} selected")]
    [RequiresObjects(ObjectStoreKeys.LoggedInAs, ObjectStoreKeys.CurrentWorkspace)]
    public async Task ThereAreTransactionsReadyForImportReview(int count, int selectedCount)
    {
        // Given: Get logged in username
        var username = _context.ObjectStore.Get<string>(ObjectStoreKeys.LoggedInAs)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.LoggedInAs} not found in object store");

        // And: Get current workspace name and resolve workspace key
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);

        // When: Seed import review transactions via Test Control API
        await _context.TestControlClient.SeedImportReviewTransactionsAsync(
            username,
            workspaceKey,
            count,
            selectedCount);
    }

    /// <summary>
    /// Clicks the Import button, confirms the import, and waits for completion.
    /// </summary>
    /// <remarks>
    /// Opens the import confirmation modal, clicks confirm, and waits for the
    /// import to complete and navigation to transactions page.
    /// </remarks>
    [When("I import the selected transactions")]
    public async Task IImportTheSelectedTransactions()
    {
        // When: Click the Import button to open confirmation modal
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.ClickImportButtonAsync();

        // And: Confirm the import
        await importPage.ConfirmImportAsync();
    }

    /// <summary>
    /// Verifies that the import review queue has been completely cleared.
    /// </summary>
    /// <remarks>
    /// Navigates back to the import page and verifies that the empty state is displayed,
    /// indicating no pending import review transactions remain.
    ///
    /// Requires Objects
    /// - CurrentWorkspace
    /// </remarks>
    [Then("import review queue should be completely cleared")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ImportReviewQueueShouldBeCompletelyCleared()
    {
        // Then: Navigate back to import page
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the current workspace
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);

        // And: Verify empty state is displayed (no pending imports)
        var isEmpty = await importPage.IsEmptyStateAsync();
        NUnit.Framework.Assert.That(isEmpty, NUnit.Framework.Is.True,
            "Expected import review queue to be empty but transactions are still pending");
    }

    #endregion
}
