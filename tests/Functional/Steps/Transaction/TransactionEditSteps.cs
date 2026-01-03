using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for transaction create, update, and delete operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles transaction modification operations:
/// - Creating new transactions
/// - Updating existing transactions
/// - Deleting transactions
/// - Verifying transaction state after modifications
/// </remarks>
public class TransactionEditSteps(ITestContext context) : TransactionStepsBase(context)
{
    #region Steps: WHEN

    /// <summary>
    /// Adds a test transaction to a workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page, selects workspace,
    /// creates transaction with today's date, unique payee name, and $100 amount.
    /// Stores payee name in object store for later reference.
    /// </remarks>
    [When("I add a transaction to {workspaceName}")]
    public async Task WhenIAddATransactionTo(string workspaceName)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(fullWorkspaceName);

        // Add a test transaction
        var testDate = DateTime.Today.ToString("yyyy-MM-dd");
        var testPayee = "Test Transaction " + Guid.NewGuid().ToString()[..8];
        await transactionsPage.CreateTransactionAsync(testDate, testPayee, 100.00m);

        _context.ObjectStore.Add(ObjectStoreKeys.TransactionPayee, testPayee);
    }

    /// <summary>
    /// Updates the previously added transaction.
    /// </summary>
    /// <remarks>
    /// Retrieves last transaction payee from object store, opens edit modal,
    /// updates payee name by prepending "Updated ", submits form, waits for
    /// transaction to appear, and stores new payee name.
    /// </remarks>
    [When("I update that transaction")]
    public async Task WhenIUpdateThatTransaction()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        var payee = GetLastTransactionPayee();
        var newPayee = "Updated " + payee;

        // Use quick edit workflow (only Payee and Memo fields available in modal from transactions page)
        await transactionsPage.OpenEditModalAsync(payee);
        await transactionsPage.FillEditPayeeAsync(newPayee);
        await transactionsPage.SubmitEditFormAsync();

        // Wait for the updated transaction to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        var key = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionKey);
        await transactionsPage.WaitForTransactionRowByKeyAsync(Guid.Parse(key));

        // Update the stored payee name
        _context.ObjectStore.Add(ObjectStoreKeys.TransactionPayee, newPayee);
    }

    /// <summary>
    /// Deletes the previously added/updated transaction.
    /// </summary>
    /// <remarks>
    /// Retrieves last transaction payee from object store and performs delete
    /// operation via transactions page.
    /// </remarks>
    [When("I delete that transaction")]
    public async Task WhenIDeleteThatTransaction()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        var payee = GetLastTransactionPayee();

        // Wait for the transaction to appear before attempting deletion
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        var key = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionKey);
        await transactionsPage.WaitForTransactionRowByKeyAsync(Guid.Parse(key));

        // Now it should be safe to do the delete
        await transactionsPage.DeleteTransactionAsync(payee);
    }

    /// <summary>
    /// Changes the memo field in the quick edit modal or details page.
    /// </summary>
    /// <param name="newMemo">The new memo value.</param>
    /// <remarks>
    /// Fills the memo field and stores the new value in object store for verification.
    /// Works with both quick edit modal and transaction details page.
    /// </remarks>
    [When("I change Memo to {newMemo}")]
    public async Task WhenIChangeMemoTo(string newMemo)
    {
        // When: Fill the memo field
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditMemoAsync(newMemo);

        // And: Store the new memo for verification
        _context.ObjectStore.Add("TransactionMemo", newMemo);
    }

    /// <summary>
    /// Changes the category field in the quick edit modal or details page.
    /// </summary>
    /// <param name="newCategory">The new category value.</param>
    /// <remarks>
    /// Fills the category field and stores the new value in object store for verification.
    /// Works with both quick edit modal and transaction details page.
    /// </remarks>
    [When("I change Category to {newCategory}")]
    public async Task WhenIChangeCategoryTo(string newCategory)
    {
        // When: Fill the category field
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditCategoryAsync(newCategory);

        // And: Store the new category for verification
        _context.ObjectStore.Add("TransactionCategory", newCategory);
    }

    /// <summary>
    /// Changes the Source field to the specified value.
    /// </summary>
    /// <param name="newSource">The new source value.</param>
    /// <remarks>
    /// Fills the source field and stores the new value in object store for verification.
    /// Only available on transaction details page (not in quick edit modal).
    /// </remarks>
    [When("I change Source to {newSource}")]
    public async Task WhenIChangeSourceTo(string newSource)
    {
        // When: Fill the source field
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.FillSourceAsync(newSource);

        // And: Store the new source for verification
        _context.ObjectStore.Add("TransactionSource", newSource);
    }

    /// <summary>
    /// Changes the ExternalId field to the specified value.
    /// </summary>
    /// <param name="newExternalId">The new external ID value.</param>
    /// <remarks>
    /// Fills the external ID field and stores the new value in object store for verification.
    /// Only available on transaction details page (not in quick edit modal).
    /// </remarks>
    [When("I change ExternalId to {newExternalId}")]
    public async Task WhenIChangeExternalIdTo(string newExternalId)
    {
        // When: Fill the external ID field
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.FillExternalIdAsync(newExternalId);

        // And: Store the new external ID for verification
        _context.ObjectStore.Add("TransactionExternalId", newExternalId);
    }

    /// <summary>
    /// Clicks the Update button on the quick edit modal.
    /// </summary>
    /// <remarks>
    /// Submits the quick edit form and waits for the modal to close.
    /// </remarks>
    [When("I click \"Update\"")]
    public async Task WhenIClickUpdate()
    {
        // When: Submit the edit form
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.SubmitEditFormAsync();
    }

    /// <summary>
    /// Clicks the Edit button on the transaction details page.
    /// </summary>
    /// <remarks>
    /// Transitions from display mode to edit mode on the details page.
    /// </remarks>
    [When("I click the \"Edit\" button")]
    public async Task WhenIClickTheEditButton()
    {
        // When: Click the Edit button to enter edit mode
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.StartEditingAsync();
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that the last added transaction is visible in the list.
    /// </summary>
    /// <remarks>
    /// Retrieves last transaction payee from object store, waits for it to appear
    /// in the list, and verifies visibility. Includes explicit wait to handle
    /// UI update timing.
    /// </remarks>
    [Then("the transaction should be saved successfully")]
    public async Task ThenTheTransactionShouldBeSavedSuccessfully()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        var payee = GetLastTransactionPayee();

        // Wait for the transaction to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await transactionsPage.WaitForTransactionAsync(payee);

        // Store the transaction's test ID for later reference. This makes it much
        // more straightforward to wait for the updated transaction in future steps.
        var transactionKey = await transactionsPage.GetTransactionKeyByPayeeAsync(payee);
        _context.ObjectStore.Add(ObjectStoreKeys.TransactionKey, transactionKey.ToString());

        // Confirm that the transaction is really there now
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.True, "Transaction should be visible in the list");
    }

    /// <summary>
    /// Verifies that transaction update was saved successfully.
    /// </summary>
    /// <remarks>
    /// Retrieves last (updated) transaction payee from object store and verifies
    /// it's visible in the list. Note: May need additional wait time for UI updates.
    /// </remarks>
    [Then("my changes should be saved")]
    public async Task ThenMyChangesShouldBeSaved()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        var payee = GetLastTransactionPayee();

        // Prior operation awaits the loading spinner being being hidden.
        // We can't rely on that alone to guarantee the updated transaction is visible,
        // so we add an explicit wait here. Last time we interacted with the transaction,
        // we stored its key for easy reference.
        var key = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionKey);
        await transactionsPage.WaitForTransactionRowByKeyAsync(Guid.Parse(key));

        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);

        Assert.That(hasTransaction, Is.True, "Updated transaction should be visible");
    }

    /// <summary>
    /// Verifies that the transaction was removed from the list.
    /// </summary>
    /// <remarks>
    /// Retrieves last transaction payee from object store and verifies it's no
    /// longer visible after deletion.
    /// </remarks>
    [Then("it should be removed")]
    public async Task ThenItShouldBeRemoved()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        var payee = GetLastTransactionPayee();

        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.False, "Transaction should be removed from the list");
    }

    #endregion
}
