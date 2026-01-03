using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for transaction quick edit modal operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles quick edit modal operations (limited field editing):
/// - Opening quick edit modal from transaction list
/// - Editing fields (payee, category, memo)
/// - Submitting quick edit changes
/// - Verifying quick edit modal shows only payee/category/memo
/// - Verifying quick edit modal hides date/amount/source/externalId
/// - Field value verification and modal close behavior
/// </remarks>
public class TransactionQuickEditSteps(ITestContext context) : TransactionStepsBase(context)
{
    #region Steps: WHEN

    /// <summary>
    /// Opens the quick edit modal for the current transaction.
    /// </summary>
    /// <remarks>
    /// Retrieves transaction payee from object store (KEY_TRANSACTION_PAYEE) and
    /// opens the edit modal. Tests the quick edit workflow (PATCH endpoint).
    ///
    /// Requires Objects:
    /// - TransactionPayee
    /// </remarks>
    [When("I click the \"Edit\" button on the transaction")]
    [RequiresObjects(ObjectStoreKeys.TransactionPayee)]
    public async Task WhenIClickTheEditButtonOnTheTransaction()
    {
        // When: Get the payee from object store
        var payee = _context.ObjectStore.Get<string>("TransactionPayee")
            ?? throw new InvalidOperationException("TransactionPayee not found in object store");

        // And: Locate and click the edit button for the transaction
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenEditModalAsync(payee);
    }

    /// <summary>
    /// Opens the quick edit modal for the specified transaction.
    /// </summary>
    /// <param name="payee">The payee name of the transaction to edit (optional, uses object store if null).</param>
    /// <remarks>
    /// Locates the transaction by payee and opens the edit modal.
    ///
    /// Requires Objects:
    /// - TransactionPayee (if payee parameter is null)
    /// </remarks>
    [When("I quick edit the transaction")]
    [When("I quick edit the {payee} transaction")]
    [RequiresObjects(ObjectStoreKeys.TransactionPayee)]
    public async Task WhenIQuickEditTheTransaction(string? payee = null)
    {
        var actualPayee = payee ?? _context.ObjectStore.Get<string>("TransactionPayee")
            ?? throw new InvalidOperationException("TransactionPayee not found in object store");

        // When: Locate and click the edit button for the transaction
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenEditModalAsync(actualPayee);
    }

    /// <summary>
    /// Changes the memo field in the quick edit modal.
    /// </summary>
    /// <param name="newMemo">The new memo value.</param>
    /// <remarks>
    /// Fills the memo field and stores the new value in object store for verification.
    ///
    /// Provides Objects:
    /// - TransactionMemo
    /// </remarks>
    [When("I change Memo to {newMemo}")]
    [ProvidesObjects(ObjectStoreKeys.TransactionMemo)]
    public async Task WhenIChangeMemoTo(string newMemo)
    {
        // When: Fill the memo field
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditMemoAsync(newMemo);

        // And: Store the new memo for verification
        _context.ObjectStore.Add("TransactionMemo", newMemo);
    }

    /// <summary>
    /// Changes the category field in the quick edit modal.
    /// </summary>
    /// <param name="newCategory">The new category value.</param>
    /// <remarks>
    /// Fills the category field and stores the new value in object store for verification.
    ///
    /// Provides Objects:
    /// - TransactionCategory
    /// </remarks>
    [When("I change Category to {newCategory}")]
    [ProvidesObjects(ObjectStoreKeys.TransactionCategory)]
    public async Task WhenIChangeCategoryTo(string newCategory)
    {
        // When: Fill the category field
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditCategoryAsync(newCategory);

        // And: Store the new category for verification
        _context.ObjectStore.Add("TransactionCategory", newCategory);
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

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that a modal with the expected title is displayed.
    /// </summary>
    /// <param name="expectedTitle">The expected modal title text.</param>
    /// <remarks>
    /// Waits for edit modal to be visible, extracts the title from modal header,
    /// and stores it in object store for later verification.
    ///
    /// Provides Objects:
    /// - ModalTitle
    /// </remarks>
    [Then("I should see a modal titled {expectedTitle}")]
    [ProvidesObjects("ModalTitle")]
    public async Task ThenIShouldSeeAModalTitled(string expectedTitle)
    {
        // Then: Wait for the edit modal to be visible
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.EditModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // And: Get the modal title from the modal header
        var modalTitle = await transactionsPage.EditModal.Locator("h5, .modal-title").First.TextContentAsync();

        Assert.That(modalTitle, Is.EqualTo(expectedTitle),
            $"Expected modal title to be '{expectedTitle}' but was '{modalTitle}'");

        // And: Store modal title for future verification
        _context.ObjectStore.Add("ModalTitle", modalTitle ?? string.Empty);
    }

    /// <summary>
    /// Verifies that only Payee, Category, and Memo fields are visible in the quick edit modal.
    /// </summary>
    /// <remarks>
    /// Tests the quick edit modal constraint - only Payee, Category, and Memo fields should
    /// be editable via the modal (PATCH endpoint).
    /// </remarks>
    [Then("I should only see fields for \"Payee\", \"Category\", and \"Memo\"")]
    public async Task ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo()
    {
        // Then: Verify Payee field is visible
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        var payeeVisible = await transactionsPage.EditPayeeInput.IsVisibleAsync();
        Assert.That(payeeVisible, Is.True, "Payee field should be visible in quick edit modal");

        // And: Verify Category field is visible
        var categoryVisible = await transactionsPage.EditCategoryInput.IsVisibleAsync();
        Assert.That(categoryVisible, Is.True, "Category field should be visible in quick edit modal");

        // And: Verify Memo field is visible
        var memoVisible = await transactionsPage.EditMemoInput.IsVisibleAsync();
        Assert.That(memoVisible, Is.True, "Memo field should be visible in quick edit modal");
    }

    /// <summary>
    /// Verifies that the fields in the quick edit modal match the expected values from the object store.
    /// </summary>
    /// <remarks>
    /// Checks Payee, Category, and Memo fields against values stored during transaction seeding.
    /// Only verifies fields that were populated during seeding (checks object store for presence).
    ///
    /// Requires Objects:
    /// - TransactionPayee (optional)
    /// - TransactionCategory (optional)
    /// - TransactionMemo (optional)
    /// </remarks>
    [Then("the fields match the expected values")]
    public async Task ThenTheFieldsMatchTheExpectedValues()
    {
        // Then: Get the transactions page
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // And: Verify Payee value if expected
        if (_context.ObjectStore.Contains<string>("TransactionPayee"))
        {
            var expectedPayee = _context.ObjectStore.Get<string>("TransactionPayee");
            var actualPayee = await transactionsPage.EditPayeeInput.InputValueAsync();
            Assert.That(actualPayee, Is.EqualTo(expectedPayee),
                $"Payee field should display '{expectedPayee}' but was '{actualPayee}'");
        }

        // And: Verify Category value if expected
        if (_context.ObjectStore.Contains<string>("TransactionCategory"))
        {
            var expectedCategory = _context.ObjectStore.Get<string>("TransactionCategory");
            var actualCategory = await transactionsPage.EditCategoryInput.InputValueAsync();
            Assert.That(actualCategory, Is.EqualTo(expectedCategory),
                $"Category field should display '{expectedCategory}' but was '{actualCategory}'");
        }

        // And: Verify Memo value if expected
        if (_context.ObjectStore.Contains<string>("TransactionMemo"))
        {
            var expectedMemo = _context.ObjectStore.Get<string>("TransactionMemo");
            var actualMemo = await transactionsPage.EditMemoInput.InputValueAsync();
            Assert.That(actualMemo, Is.EqualTo(expectedMemo),
                $"Memo field should display '{expectedMemo}' but was '{actualMemo}'");
        }
    }

    /// <summary>
    /// Verifies that Date, Amount, Source, and ExternalId fields are NOT in the quick edit modal.
    /// </summary>
    /// <remarks>
    /// Tests that the quick edit modal excludes fields that require full details page.
    /// These fields are only editable via the PUT endpoint on the details page.
    /// </remarks>
    [Then("I should not see fields for \"Date\", \"Amount\", \"Source\", or \"ExternalId\"")]
    public async Task ThenIShouldNotSeeFieldsForDateAmountSourceOrExternalId()
    {
        // Then: Verify Date field is not visible (doesn't exist in quick edit modal)
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        var dateCount = await transactionsPage.EditModal.GetByTestId("edit-transaction-date").CountAsync();
        Assert.That(dateCount, Is.EqualTo(0), "Date field should not exist in quick edit modal");

        // And: Verify Amount field is not visible (doesn't exist in quick edit modal)
        var amountCount = await transactionsPage.EditModal.GetByTestId("edit-transaction-amount").CountAsync();
        Assert.That(amountCount, Is.EqualTo(0), "Amount field should not exist in quick edit modal");

        // And: Verify Source field is not visible (doesn't exist in quick edit modal)
        var sourceCount = await transactionsPage.EditModal.GetByTestId("edit-transaction-source").CountAsync();
        Assert.That(sourceCount, Is.EqualTo(0), "Source field should not exist in quick edit modal");

        // And: Verify ExternalId field is not visible (doesn't exist in quick edit modal)
        var externalIdCount = await transactionsPage.EditModal.GetByTestId("edit-transaction-external-id").CountAsync();
        Assert.That(externalIdCount, Is.EqualTo(0), "ExternalId field should not exist in quick edit modal");
    }

    #endregion
}
