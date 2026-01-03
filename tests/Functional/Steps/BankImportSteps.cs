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
    /// Navigates to the import review page with a workspace selected.
    /// </summary>
    /// <remarks>
    /// Sets up minimal state to be on the import review page: navigates to the page and
    /// selects the current workspace. Does not upload any files.
    /// </remarks>
    // [Given("I am on the import review page")]
    public async Task GivenIAmOnTheImportReviewPage()
    {
        // Given: Get workspace name
        var workspaceName = _context.ObjectStore.Get<string>("CurrentWorkspaceName")
            ?? throw new InvalidOperationException("CurrentWorkspaceName not found in object store");

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
    // [When("I upload OFX file {filename}")]
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
    // [Then("page should display {count} transactions")]
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
    // [Then("{count} transactions should be selected by default")]
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
    // [Then("{count} transactions should be deselected by default")]
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
}
