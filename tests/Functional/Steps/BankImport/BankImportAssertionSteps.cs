using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.BankImport;

/// <summary>
/// Step definitions for bank import assertion and verification operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles all THEN steps for verifying import review state, transaction counts,
/// selection state, errors, warnings, and permissions.
/// </remarks>
public class BankImportAssertionSteps(ITestContext context) : BankImportStepsBase(context)
{
    #region Steps: THEN

    /// <summary>
    /// Verifies that the import review page displays the expected number of transactions.
    /// </summary>
    /// <param name="count">The expected number of transactions.</param>
    [Then("page should display {count} transactions")]
    [Then("I should see {count} transactions in the review list")]
    public async Task PageShouldDisplayTheTransactions(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify transaction count
        var actualCount = await importPage.GetTransactionCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the expected number of transactions are selected
    /// </summary>
    /// <param name="count">The expected number of selected transactions.</param>
    [Then("{count} transactions should be selected by default")]
    [Then("all {count} transactions should be selected by default")]
    [Then("I should see {count} transactions selected")]
    public async Task TransactionsShouldBeSelected(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify selected count
        var actualCount = await importPage.GetSelectedCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions to be selected but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the expected number of transactions are deselected
    /// </summary>
    /// <param name="count">The expected number of deselected transactions.</param>
    [Then("{count} transactions should be deselected by default")]
    [Then("I should see {count} transactions deselected")]
    public async Task TransactionsShouldBeDeselected(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify deselected count
        var actualCount = await importPage.GetDeselectedCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions to be deselected but found {actualCount}");
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
    [Then("import review queue should be empty")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ImportReviewQueueShouldBeCompletelyCleared()
    {
        // Then: Navigate back to import page
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the current workspace
        var workspaceName = GetCurrentWorkspace();
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);

        // And: Verify empty state is displayed (no pending imports)
        var isEmpty = await importPage.IsEmptyStateAsync();
        Assert.That(isEmpty, Is.True,
            "Expected import review queue to be empty but transactions are still pending");
    }

    /// <summary>
    /// Then the review list contains the transactions uploaded earlier
    /// </summary>
    [Then("the review list contains the transactions uploaded earlier")]
    [RequiresObjects(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task TheReviewListContainsTheTransactionsUploadedEarlier()
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Get all payees currently displayed
        var actualPayees = await importPage.GetAllTransactionPayeesAsync();

        // And: Get the expected payees from object store
        var expectedPayees = _context.ObjectStore.Get<List<string>>(ObjectStoreKeys.UploadedTransactionPayees)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.UploadedTransactionPayees} not found in object store");

        // Verify that all of the uploaded transactions are visible in the review list
        var missingPayees = expectedPayees.Except(actualPayees).ToList();
        Assert.That(missingPayees, Is.Empty,
            $"All uploaded transactions should appear in the review list, but missing: {string.Join(", ", missingPayees)}");

    }

    /// <summary>
    /// Then all 5 transactions should be deselected by default
    /// </summary>
    [Then("all {count} transactions should be deselected by default")]
    [Then("I should see {count} transactions deselected")]

    public async Task AllTransactionsShouldBeDeselectedByDefault(int count)
    {
        await TransactionsShouldBeDeselected(count);
        await TransactionsShouldBeSelected(0);
    }

    /// <summary>
    /// Then no transactions should be highlighted for further review
    /// </summary>
    [Then("no transactions should be highlighted for further review")]
    public Task NoTransactionsShouldBeHighlightedForFurtherReview()
        => TransactionsShouldBeHighlightedForFurtherReview(0);

    /// <summary>
    /// Then no transactions should be highlighted for further review
    /// </summary>
    [Then("there should be {count} transactions highlighted for further review")]
    public async Task TransactionsShouldBeHighlightedForFurtherReview(int count)
    {
        var importPage = _context.GetOrCreatePage<ImportPage>();
        var highlightedCount = await importPage.GetWarningTransactionCountAsync();
        Assert.That(highlightedCount, Is.EqualTo(count), $"Expected {count} transactions to be highlighted for further review, but found {highlightedCount}");
    }

    /// <summary>
    /// Then I should be able to upload files
    /// </summary>
    [Then("I should be able to upload files")]
    public async Task IShouldBeAbleToUploadFiles()
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify that the file upload input is present and enabled
        var isUploadEnabled = await importPage.FileInput.IsEnabledAsync();
        Assert.That(isUploadEnabled, Is.True, "Expected to be able to upload files, but upload input is not enabled");
    }

    /// <summary>
    /// Then I should not be able to upload files
    /// </summary>
    [Then("I should not be able to upload files")]
    public async Task IShouldNotBeAbleToUploadFiles()
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify that the file upload input is not available
        var isUploadVisible = await importPage.FileInput.IsVisibleAsync();
        Assert.That(isUploadVisible, Is.False, "Expected not to be able to upload files, but upload input is visible");
    }

    /// <summary>
    /// Then I should see a permission error message
    /// </summary>
    [Then("I should see a permission error message")]
    public async Task IShouldSeeAPermissionErrorMessage()
    {
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.PermissionDeniedError.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible });
        var hasErrorMessage = await importPage.PermissionDeniedError.IsVisibleAsync();
        Assert.That(hasErrorMessage, Is.True, "Expected to see a permission error message, but none was found");
    }

    /// <summary>
    /// Then I should see a warning about potential duplicates
    /// </summary>
    [Then("I should see a warning about potential duplicates")]
    public async Task IShouldSeeAWarningAboutPotentialDuplicates()
    {
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.DuplicateReviewAlert.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible });
        var hasErrorMessage = await importPage.DuplicateReviewAlert.IsVisibleAsync();
        Assert.That(hasErrorMessage, Is.True, "Expected to see a warning about potential duplicates, but none was found");
    }

    /// <summary>
    /// Then I should be able to complete the import review
    /// </summary>
    [Then("I should be able to complete the import review")]
    public async Task IShouldBeAbleToCompleteTheImportReview()
    {
        var importPage = _context.GetOrCreatePage<ImportPage>();
        var isImportButtonEnabled = await importPage.ImportButton.IsEnabledAsync();
        Assert.That(isImportButtonEnabled, Is.True, "Expected the import button to be enabled, but it was not");
    }

    /// <summary>
    /// Verifies that the previously deselected transactions remain deselected.
    /// </summary>
    /// <remarks>
    /// Requires Objects
    /// - AffectedTransactions (List&lt;string&gt;) - Keys of the transactions that should be deselected
    /// </remarks>
    [Then("the transactions deselected earlier should remain deselected")]
    [RequiresObjects(ObjectStoreKeys.AffectedTransactionKeys)]
    public async Task TheTransactionsDeselectedEarlierShouldRemainDeselected()
    {
        // And: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // Then: Retrieve the deselected transaction keys from object store
        var deselectedKeys = _context.ObjectStore.Get<List<string>>(ObjectStoreKeys.AffectedTransactionKeys)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.AffectedTransactionKeys} not found in object store");

        // And: Verify each transaction remains deselected
        foreach (var key in deselectedKeys)
        {
            var isSelected = await importPage.IsTransactionSelectedAsync(key);
            Assert.That(isSelected, Is.False,
                $"Expected transaction {key} to remain deselected, but it is currently selected");
        }
    }

    /// <summary>
    /// Then I should have seen a confirmation message indicating {count} transactions were accepted
    /// </summary>
    [Then("I should have seen a confirmation message indicating {count} transactions were accepted")]
    [RequiresObjects(ObjectStoreKeys.ImportStatisticsDto)]
    public async Task IShouldHaveSeenAConfirmationMessageIndicatingTransactionsWereAccepted(int count)
    {
        // Then: Get the actual statistics reported when we completed the import process from object store
        var actualStatistics = _context.ObjectStore.Get<ImportStatisticsDto>(ObjectStoreKeys.ImportStatisticsDto)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.ImportStatisticsDto} not found in object store");

        // And: Verify that the actual statistics match the expected values
        Assert.That(actualStatistics.SelectedCount, Is.EqualTo(count),
            $"Expected {count} transactions to be accepted but found {actualStatistics.SelectedCount}");
    }

    #endregion
}
