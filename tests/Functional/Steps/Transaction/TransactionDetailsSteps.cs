using Microsoft.Playwright;
using NUnit.Framework;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;
using static YoFi.V3.Tests.Functional.Infrastructure.ObjectStoreKeys;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for transaction details page navigation and verification.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles transaction details page operations:
/// - Verifying navigation to transaction details page
/// - Verifying all transaction fields displayed on details page
/// - Verifying specific field values on details page
/// - Navigating back to transaction list
/// </remarks>
public class TransactionDetailsSteps(ITestContext context) : TransactionStepsBase(context)
{

    #region Steps: WHEN

    /// <summary>
    /// Clicks the "Back to Transactions" button to return to the transaction list.
    /// </summary>
    /// <remarks>
    /// Navigates from the transaction details page back to the transactions list page.
    /// </remarks>
    [When("I click \"Back to Transactions\"")]
    public async Task WhenIClickBackToTransactions()
    {
        // When: Click the back button to return to transactions list
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.GoBackAsync();
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that navigation to the transaction details page occurred.
    /// </summary>
    /// <remarks>
    /// Waits for page ready state to ensure page is interactive after navigation.
    /// </remarks>
    [Then("I should navigate to the transaction details page")]
    public async Task ThenIShouldNavigateToTheTransactionDetailsPage()
    {
        // Then: Wait for the transaction details page to be ready
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Verifies that all expected transaction fields are displayed on the details page.
    /// </summary>
    /// <remarks>
    /// Uses the seeded transaction data stored in the object store (from GivenIHaveAWorkspaceWithATransaction)
    /// to verify all fields match what was seeded. This handles cases where the seed API modifies
    /// values (e.g., appending numbers to payee names).
    ///
    /// Requires Objects:
    /// - TransactionPayee
    /// - TransactionAmount
    /// - TransactionCategory (optional)
    /// - TransactionMemo (optional)
    /// - TransactionSource (optional)
    /// - TransactionExternalId (optional)
    /// </remarks>
    [Then("I should see all the expected transaction fields displayed")]
    [RequiresObjects(TransactionPayee, TransactionAmount)]
    public async Task ThenIShouldSeeAllTheExpectedTransactionFieldsDisplayed()
    {
        // Then: Get the TransactionDetailsPage
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();

        // And: Get expected values from object store (seeded transaction data)
        var expectedPayee = GetRequiredFromStore(TransactionPayee);
        var expectedAmount = GetRequiredFromStore(TransactionAmount);

        // And: Verify payee
        var payeeValue = await detailsPage.GetPayeeAsync();
        Assert.That(payeeValue?.Trim(), Is.EqualTo(expectedPayee),
            $"Payee field should be '{expectedPayee}'");

        // And: Verify amount
        var amountValue = await detailsPage.GetAmountAsync();
        Assert.That(amountValue?.Trim(), Does.Contain(expectedAmount),
            $"Amount field should contain '{expectedAmount}'");

        // And: Verify optional fields if they were seeded
        if (_context.ObjectStore.Contains<string>(TransactionCategory))
        {
            var expectedCategory = _context.ObjectStore.Get<string>(TransactionCategory);
            var categoryValue = await detailsPage.GetCategoryAsync();
            var expectedDisplay = string.IsNullOrEmpty(expectedCategory) ? TransactionDetailsPage.EmptyFieldDisplay : expectedCategory;
            Assert.That(categoryValue?.Trim(), Is.EqualTo(expectedDisplay),
                $"Category field should be '{expectedDisplay}'");
        }

        if (_context.ObjectStore.Contains<string>(TransactionMemo))
        {
            var expectedMemo = _context.ObjectStore.Get<string>(TransactionMemo);
            var memoValue = await detailsPage.GetMemoAsync();
            var expectedDisplay = string.IsNullOrEmpty(expectedMemo) ? TransactionDetailsPage.EmptyFieldDisplay : expectedMemo;
            Assert.That(memoValue?.Trim(), Is.EqualTo(expectedDisplay),
                $"Memo field should be '{expectedDisplay}'");
        }

        if (_context.ObjectStore.Contains<string>(TransactionSource))
        {
            var expectedSource = _context.ObjectStore.Get<string>(TransactionSource);
            var sourceValue = await detailsPage.GetSourceAsync();
            var expectedDisplay = string.IsNullOrEmpty(expectedSource) ? TransactionDetailsPage.EmptyFieldDisplay : expectedSource;
            Assert.That(sourceValue?.Trim(), Is.EqualTo(expectedDisplay),
                $"Source field should be '{expectedDisplay}'");
        }

        if (_context.ObjectStore.Contains<string>(TransactionExternalId))
        {
            var expectedExternalId = _context.ObjectStore.Get<string>(TransactionExternalId);
            var externalIdValue = await detailsPage.GetExternalIdAsync();
            var expectedDisplay = string.IsNullOrEmpty(expectedExternalId) ? TransactionDetailsPage.EmptyFieldDisplay : expectedExternalId;
            Assert.That(externalIdValue?.Trim(), Is.EqualTo(expectedDisplay),
                $"ExternalId field should be '{expectedDisplay}'");
        }
    }

    /// <summary>
    /// Verifies that a specific field displays the expected value on the transaction details page.
    /// </summary>
    /// <param name="expectedValue">The expected value to see.</param>
    /// <param name="fieldName">The field name (e.g., "Source", "ExternalId").</param>
    /// <remarks>
    /// Retrieves the field value from the transaction details page and verifies it matches
    /// the expected value. Supports "Category", "Source", and "ExternalId" field names.
    /// </remarks>
    [Then("I should see {expectedValue} as the {fieldName}")]
    public async Task ThenIShouldSeeValueAsField(string expectedValue, string fieldName)
    {
        // Then: Get the TransactionDetailsPage
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();

        // And: Get the field value based on field name
        string? actualValue = fieldName switch
        {
            "Category" => await detailsPage.GetCategoryAsync(),
            "Source" => await detailsPage.GetSourceAsync(),
            "ExternalId" => await detailsPage.GetExternalIdAsync(),
            _ => throw new ArgumentException($"Unsupported field name: {fieldName}")
        };

        // And: Verify the field displays the expected value
        Assert.That(actualValue?.Trim(), Is.EqualTo(expectedValue),
            $"{fieldName} field should be '{expectedValue}' but was '{actualValue}'");
    }

    /// <summary>
    /// Verifies that the user returned to the transaction list page.
    /// </summary>
    /// <remarks>
    /// Waits for the transactions page to be ready after navigation.
    /// </remarks>
    [Then("I should return to the transaction list")]
    public async Task ThenIShouldReturnToTheTransactionList()
    {
        // Then: Get TransactionsPage and wait for it to be ready
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Verifies that the transaction created in the Given step is visible in the transaction list.
    /// </summary>
    /// <remarks>
    /// Retrieves the payee from object store and verifies the transaction is present in the list.
    ///
    /// Requires Objects:
    /// - TransactionPayee
    /// </remarks>
    [Then("I should see all my transactions")]
    [RequiresObjects(TransactionPayee)]
    public async Task ThenIShouldSeeAllMyTransactions()
    {
        // Then: Get the expected payee from object store
        var expectedPayee = GetRequiredFromStore(TransactionPayee);

        // And: Get TransactionsPage
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // And: Verify the transaction is visible in the list
        var hasTransaction = await transactionsPage.HasTransactionAsync(expectedPayee);
        Assert.That(hasTransaction, Is.True,
            $"Should see transaction with payee '{expectedPayee}' in the list");
    }

    #endregion
}
