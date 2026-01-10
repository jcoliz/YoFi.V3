using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Steps.BankImport;

/// <summary>
/// Step definitions for bank import test data seeding operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles seeding import review transactions via Test Control API for faster test setup.
/// </remarks>
public class BankImportDataSteps(ITestContext context) : BankImportStepsBase(context)
{
    #region Steps: GIVEN

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
        var username = GetLoggedInUsername();

        // And: Get current workspace name and resolve workspace key
        var workspaceName = GetCurrentWorkspace();
        var workspaceKey = GetWorkspaceKey(workspaceName);

        // When: Seed import review transactions via Test Control API
        await _context.TestControlClient.SeedImportReviewTransactionsAsync(
            username,
            workspaceKey,
            count,
            selectedCount);
    }

    #endregion
}
