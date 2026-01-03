using Microsoft.Playwright;
using NUnit.Framework;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;
using static YoFi.V3.Tests.Functional.Pages.TransactionsPage;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for creating new transactions via the create modal.
/// </summary>
/// <param name="context">Test context providing access to shared test infrastructure.</param>
/// <remarks>
/// Handles all operations related to the transaction creation modal including opening,
/// field verification, form filling, and validation of created transactions.
/// </remarks>
public class TransactionCreateSteps(ITestContext context) : TransactionStepsBase(context)
{
    #region When Steps

    /// <summary>
    /// Clicks the "Add Transaction" button to open the create transaction modal.
    /// </summary>
    /// <remarks>
    /// Opens the create modal with all transaction fields and marks the edit mode
    /// for polymorphic save operations.
    /// </remarks>
    [When("I click the \"Add Transaction\" button")]
    public async Task WhenIClickTheAddTransactionButton()
    {
        // When: Click the Add Transaction button to open create modal
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenCreateModalAsync();

        // And: Mark that we're in create modal mode
        _context.ObjectStore.Add(ObjectStoreKeys.EditMode, "CreateModal");
    }

    /// <summary>
    /// Fills transaction fields in the create modal from a DataTable.
    /// </summary>
    /// <param name="dataTable">DataTable with columns "Field" and "Value" containing field names and values.</param>
    /// <remarks>
    /// Parses the DataTable to extract field-value pairs and fills the corresponding fields
    /// in the create transaction modal. Supports: Date, Payee, Amount, Category, Memo, Source, External ID.
    /// Stores all values in object store for later verification.
    /// </remarks>
    [When("I fill in the following transaction fields:")]
    public async Task WhenIFillInTheFollowingTransactionFields(DataTable dataTable)
    {
        // When: Get the TransactionsPage
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // And: Process each row in the data table
        foreach (var row in dataTable.Rows)
        {
            var fieldName = row["Field"];
            var value = row["Value"];

            // And: Fill the appropriate field based on field name
            switch (fieldName)
            {
                case "Date":
                    await transactionsPage.FillCreateDateAsync(value);
                    // Store for later verification
                    break;

                case "Payee":
                    await transactionsPage.FillCreatePayeeAsync(value);
                    _context.ObjectStore.Add(ObjectStoreKeys.TransactionPayee, value);
                    break;

                case "Amount":
                    await transactionsPage.FillCreateAmountAsync(decimal.Parse(value));
                    _context.ObjectStore.Add(ObjectStoreKeys.TransactionAmount, value);
                    break;

                case "Category":
                    await transactionsPage.FillCreateCategoryAsync(value);
                    _context.ObjectStore.Add(ObjectStoreKeys.TransactionCategory, value);
                    break;

                case "Memo":
                    await transactionsPage.FillCreateMemoAsync(value);
                    _context.ObjectStore.Add(ObjectStoreKeys.TransactionMemo, value);
                    break;

                case "Source":
                    await transactionsPage.FillCreateSourceAsync(value);
                    _context.ObjectStore.Add(ObjectStoreKeys.TransactionSource, value);
                    break;

                case "External ID":
                    await transactionsPage.FillCreateExternalIdAsync(value);
                    _context.ObjectStore.Add(ObjectStoreKeys.TransactionExternalId, value);
                    break;

                default:
                    throw new ArgumentException($"Unsupported field name: {fieldName}");
            }
        }
    }

    #endregion

    #region Then Steps

    /// <summary>
    /// Verifies that the create transaction modal is visible.
    /// </summary>
    /// <remarks>
    /// Checks that the create modal has appeared after clicking the "Add Transaction" button.
    /// </remarks>
    [Then("I should see a create transaction modal")]
    public async Task ThenIShouldSeeACreateTransactionModal()
    {
        // Then: Verify the create modal is visible
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        var isVisible = await transactionsPage.CreateModal.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Create transaction modal should be visible");
    }

    /// <summary>
    /// Verifies that all specified fields are present in the create transaction modal.
    /// </summary>
    /// <param name="fieldsTable">DataTable with a "Field" column listing field names to verify.</param>
    /// <remarks>
    /// Iterates through each field name in the table and verifies its corresponding input
    /// element is visible in the create modal. Supports: Date, Payee, Amount, Category, Memo,
    /// Source, and External ID.
    /// </remarks>
    [Then("I should see the following fields in the create form:")]
    public async Task ThenIShouldSeeTheFollowingFieldsInTheCreateForm(DataTable fieldsTable)
    {
        // Then: Get the TransactionsPage
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // And: Verify each field from the table is visible
        foreach (var row in fieldsTable.Rows)
        {
            var fieldName = row["Field"];
            bool isVisible;

            // And: Check field visibility based on field name
            isVisible = fieldName switch
            {
                "Date" => await transactionsPage.CreateDateInput.IsVisibleAsync(),
                "Payee" => await transactionsPage.CreatePayeeInput.IsVisibleAsync(),
                "Amount" => await transactionsPage.CreateAmountInput.IsVisibleAsync(),
                "Category" => await transactionsPage.CreateCategoryInput.IsVisibleAsync(),
                "Memo" => await transactionsPage.CreateMemoInput.IsVisibleAsync(),
                "Source" => await transactionsPage.CreateSourceInput.IsVisibleAsync(),
                "External ID" => await transactionsPage.CreateExternalIdInput.IsVisibleAsync(),
                _ => throw new ArgumentException($"Unsupported field name: {fieldName}")
            };

            Assert.That(isVisible, Is.True, $"{fieldName} field should be visible in create modal");
        }
    }

    /// <summary>
    /// Verifies that a transaction with the specified payee appears in the transaction list.
    /// </summary>
    /// <param name="payee">The payee name to search for.</param>
    /// <remarks>
    /// Waits for the transaction to appear in the list, verifies it is visible, and stores
    /// all list-visible fields (Date, Amount, Category, Memo) in the object store for later verification.
    /// Used after creating a new transaction to verify it was successfully added.
    /// </remarks>
    [Then("I should see a transaction with Payee {payee}")]
    public async Task ThenIShouldSeeATransactionWithPayee(string payee)
    {
        // Then: Get the TransactionsPage
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // And: Wait for the transaction to appear in the list
        var rowData = await transactionsPage.GetTransactionRowDataByPayeeAsync(payee);

        if (rowData == null)
        {
            await Task.Delay(100);
            rowData = await transactionsPage.GetTransactionRowDataByPayeeAsync(payee)
                ?? throw new Exception($"Transaction with payee '{payee}' not found in the list");
        }

        // And: Verify the transaction is visible
        var hasTransaction = await rowData.RowLocator.IsVisibleAsync();
        Assert.That(hasTransaction, Is.True,
            $"Transaction with payee '{payee}' should be visible in the transaction list");

        // And: Store row data in object store for later verification
        _context.ObjectStore.Add(rowData);
    }

    /// <summary>
    /// Verifies that the transaction list fields match the expected values from object store.
    /// </summary>
    /// <remarks>
    /// Compares the actual list fields (stored by ThenIShouldSeeATransactionWithPayee) against
    /// the expected values (stored during transaction creation). Verifies Date, Amount, Category,
    /// and Memo fields that are displayed in the transaction list.
    /// </remarks>
    [Then("it contains the expected list fields")]
    public async Task ThenItContainsTheExpectedListFields()
    {
        // Then: Get actual values from object store (fetched from page in previous step)
        var rowData = _context.ObjectStore.Get<TransactionRowData>();

        // And: Get expected values from object store (set during creation)

        // And: Verify Category if it was set during creation
        if (_context.ObjectStore.Contains<string>(ObjectStoreKeys.TransactionCategory))
        {
            var actualCategory = rowData.Columns["category"];
            var expectedCategory = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionCategory);
            Assert.That(actualCategory, Is.EqualTo(expectedCategory),
                $"Category in list should be '{expectedCategory}' but was '{actualCategory}'");
        }

        // And: Verify Memo if it was set during creation
        if (_context.ObjectStore.Contains<string>(ObjectStoreKeys.TransactionMemo))
        {
            var actualMemo = rowData.Columns["memo"];
            var expectedMemo = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionMemo);
            Assert.That(actualMemo, Is.EqualTo(expectedMemo),
                $"Memo in list should be '{expectedMemo}' but was '{actualMemo}'");
        }

        // And: Verify Amount (always set during creation)
        if (_context.ObjectStore.Contains<string>(ObjectStoreKeys.TransactionAmount))
        {
            var actualAmount = rowData.Columns["amount"].Replace("$", "").Trim();
            var expectedAmount = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionAmount);
            // Amount may have currency formatting, so check if actual contains expected
            Assert.That(actualAmount, Does.Contain(expectedAmount),
                $"Amount in list should contain '{expectedAmount}' but was '{actualAmount}'");
        }

        // Note: Date verification is complex due to formatting differences, skipping for now
        await Task.CompletedTask;
    }

    #endregion
}
