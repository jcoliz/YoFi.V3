using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.BankImport;

/// <summary>
/// Step definitions for bank import review operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles transaction selection, deselection, and import confirmation operations.
/// </remarks>
public class BankImportReviewSteps(ITestContext context) : BankImportStepsBase(context)
{
    #region Steps: GIVEN

    /// <summary>
    /// Deselects the specified number of transactions and stores their keys for later verification.
    /// </summary>
    /// <param name="count">Number of transactions to deselect (starting from the first transaction)</param>
    /// <remarks>
    /// Provides Objects
    /// - AffectedTransactions (List&lt;string&gt;) - Keys of the deselected transactions
    /// </remarks>
    /// <summary>
    /// When I deselect 3 transactions
    /// </summary>
    [Given("I have deselected {count} transactions")]
    [When("I deselect {count} transactions")]
    [ProvidesObjects(ObjectStoreKeys.AffectedTransactionKeys)]
    public async Task IHaveDeselectedTransactions(int count)
    {
        // Given: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // When: Deselect the specified number of transactions and collect their keys
        var deselectedKeys = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var key = await importPage.DeselectTransactionAsync(i);
            deselectedKeys.Add(key);
        }

        // And: Store the deselected transaction keys in the object store
        _context.ObjectStore.Add(ObjectStoreKeys.AffectedTransactionKeys, deselectedKeys);
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// Clicks the Import button, confirms the import, and waits for completion.
    /// </summary>
    /// <remarks>
    /// Opens the import confirmation modal, clicks confirm, and waits for the
    /// import to complete and navigation to transactions page.
    /// </remarks>
    [Given("I have imported these transactions")]
    [When("I import the selected transactions")]
    public async Task IImportTheSelectedTransactions()
    {
        // When: Click the Import button to open confirmation modal
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.ClickImportButtonAsync();

        // And: Confirm the import
        await importPage.ConfirmImportAsync();
    }

    #endregion
}
