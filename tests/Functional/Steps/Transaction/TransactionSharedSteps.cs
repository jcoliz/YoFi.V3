using Microsoft.Playwright;
using NUnit.Framework;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Shared step definitions used across multiple transaction step classes.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Contains step definitions that are reused by multiple transaction-related step classes,
/// such as modal close verification, common assertions, and shared UI interactions.
/// </remarks>
public class TransactionSharedSteps(ITestContext context)
{
    private readonly ITestContext _context = context;

    /// <summary>
    /// Verifies that the modal has closed.
    /// </summary>
    /// <remarks>
    /// Waits for the create/edit modal to disappear and verifies it's no longer visible.
    /// Works for both create and edit modals on the transactions page.
    /// </remarks>
    [Then("the modal should close")]
    public async Task ThenTheModalShouldClose()
    {
        // Then: Wait for the modal to be hidden (works for both create and edit)
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // Try create modal first
        if (await transactionsPage.CreateModal.IsVisibleAsync())
        {
            await transactionsPage.CreateModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
            var isVisible = await transactionsPage.CreateModal.IsVisibleAsync();
            Assert.That(isVisible, Is.False, "Create modal should be closed");
        }
        // Fallback to edit modal
        else if (await transactionsPage.EditModal.IsVisibleAsync())
        {
            await transactionsPage.EditModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
            var isVisible = await transactionsPage.EditModal.IsVisibleAsync();
            Assert.That(isVisible, Is.False, "Edit modal should be closed");
        }
    }

    /// <summary>
    /// Submits the edit form (quick edit, create modal, or full details).
    /// </summary>
    /// <remarks>
    /// Submits the currently open form. Used with quick edit modal, create modal,
    /// and full details page. Uses object store to determine which mode we're in.
    ///
    /// Requires Objects:
    /// - EditMode (optional, defaults to edit form if not present)
    /// </remarks>
    [When("I click \"Save\"")]
    public async Task WhenIClickSave()
    {
        // When: Check object store for edit mode
        if (_context.ObjectStore.Contains<string>("EditMode"))
        {
            var editMode = _context.ObjectStore.Get<string>("EditMode");
            if (editMode == "TransactionDetailsPage")
            {
                var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
                await detailsPage.SaveAsync();
            }
            else if (editMode == "CreateModal")
            {
                var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
                await transactionsPage.SubmitCreateFormAsync();
            }
            else
            {
                var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
                await transactionsPage.SubmitEditFormAsync();
            }
        }
        else
        {
            // Default to edit form for backward compatibility
            var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
            await transactionsPage.SubmitEditFormAsync();
        }
    }
}
