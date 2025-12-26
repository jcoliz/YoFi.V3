using Microsoft.Playwright;
using NUnit.Framework;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Transaction Record field tests (Memo, Source, ExternalId).
/// </summary>
/// <remarks>
/// Inherits from WorkspaceTenancySteps to reuse user/workspace setup infrastructure.
/// Tests the two-tier editing model:
/// - Quick Edit Modal: Only Payee and Memo fields (PATCH endpoint)
/// - Full Details Page: All fields including Source and ExternalId (PUT endpoint)
/// </remarks>
public abstract class TransactionRecordSteps : WorkspaceTenancySteps
{
    #region Object Store Keys

    protected const string KEY_MODAL_TITLE = "ModalTitle";
    protected const string KEY_TRANSACTION_PAYEE = "TransactionPayee";
    protected const string KEY_TRANSACTION_AMOUNT = "TransactionAmount";
    protected const string KEY_TRANSACTION_MEMO = "TransactionMemo";
    protected const string KEY_TRANSACTION_SOURCE = "TransactionSource";
    protected const string KEY_TRANSACTION_EXTERNAL_ID = "TransactionExternalId";
    protected const string KEY_TRANSACTION_KEY = "TransactionKey";

    #endregion

    #region Given Steps

    /// <summary>
    /// Given: I am logged in as a user with "Editor" role
    /// </summary>
    protected async Task GivenIAmLoggedInAsAUserWithEditorRole()
    {
        // Given: Clear existing test data
        await testControlClient.DeleteAllTestDataAsync();

        // And: Create user context for an Editor user
        var username = "editor-user";
        var fullUsername = AddTestPrefix(username);

        // And: Create the user first
        var userCredentials = await testControlClient.CreateBulkUsersAsync(new[] { fullUsername });
        var credentials = userCredentials.First();
        _userCredentials[fullUsername] = credentials;

        // And: Create the workspace for the user via test control API
        var workspaceName = "Test Workspace";
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var request = new Generated.WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Editor"
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(fullUsername, new[] { request });
        var result = results.First();

        // And: Store workspace key
        _workspaceKeys[result.Name] = result.Key;
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // When: Login as the user
        await GivenIAmLoggedInAs(username);
    }

    /// <summary>
    /// Given: I have a workspace with a transaction:
    /// </summary>
    protected async Task GivenIHaveAWorkspaceWithATransaction(DataTable transactionTable)
    {
        // Given: Parse transaction data from table
        var payee = transactionTable.GetKeyValue("Payee");

        // And: Parse optional fields with defaults
        transactionTable.TryGetKeyValue("Amount", out var amountStr);
        var amount = decimal.Parse(amountStr ?? "100.00");

        transactionTable.TryGetKeyValue("Memo", out var memo);
        transactionTable.TryGetKeyValue("Source", out var source);
        transactionTable.TryGetKeyValue("ExternalId", out var externalId);

        // And: Store transaction data for verification
        _objectStore.Add(KEY_TRANSACTION_PAYEE, payee);
        _objectStore.Add(KEY_TRANSACTION_AMOUNT, amount.ToString("F2"));
        if (!string.IsNullOrEmpty(memo))
            _objectStore.Add(KEY_TRANSACTION_MEMO, memo);
        if (!string.IsNullOrEmpty(source))
            _objectStore.Add(KEY_TRANSACTION_SOURCE, source);
        if (!string.IsNullOrEmpty(externalId))
            _objectStore.Add(KEY_TRANSACTION_EXTERNAL_ID, externalId);

        // And: Get workspace key
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);
        var workspaceKey = _workspaceKeys[workspaceName];

        // And: Seed transaction via test control API with specific payee
        var loggedInUser = GetRequiredFromStore(KEY_LOGGED_IN_AS);

        var seedRequest = new Generated.TransactionSeedRequest
        {
            Count = 1,
            PayeePrefix = payee, // Use payee as prefix for single transaction
            Memo = memo,
            Source = source,
            ExternalId = externalId
        };

        var seededTransactions = await testControlClient.SeedTransactionsAsync(
            loggedInUser,
            workspaceKey,
            seedRequest
        );

        // Store the transaction key for later reference
        var transactionKey = seededTransactions.First().Key;
        _objectStore.Add(KEY_TRANSACTION_KEY, transactionKey.ToString());

        // And: Navigate to transactions page
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();
        await transactionsPage.WaitForLoadingCompleteAsync();

        // And: Select the workspace
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
        await transactionsPage.WaitForLoadingCompleteAsync();
    }

    #endregion

    #region When Steps

    /// <summary>
    /// When: I click the "Edit" button on the transaction
    /// </summary>
    protected async Task WhenIClickTheEditButtonOnTheTransaction()
    {
        // When: Get the payee from object store
        var payee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);

        // And: Locate and click the edit button for the transaction
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenEditModalAsync(payee);
    }

    /// <summary>
    /// When: I update the memo to {newMemo}
    /// </summary>
    protected async Task WhenIUpdateTheMemoTo(string newMemo)
    {
        // When: Fill the memo field
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditMemoAsync(newMemo);

        // And: Store the new memo for verification
        _objectStore.Add("NewMemo", newMemo);
    }

    /// <summary>
    /// When: I click "Save"
    /// </summary>
    protected async Task WhenIClickSave()
    {
        // When: Submit the edit form
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.SubmitEditFormAsync();
    }

    /// <summary>
    /// When: I click on the transaction row
    /// </summary>
    protected async Task WhenIClickOnTheTransactionRow()
    {
        // When: Get the payee from object store
        var payee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);

        // And: Click on the transaction row to navigate to details
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        var row = transactionsPage.GetTransactionRowByPayee(payee);
        await row.ClickAsync();
    }

    /// <summary>
    /// When: I click the "New Transaction" button
    /// </summary>
    protected async Task WhenIClickTheNewTransactionButton()
    {
        // When: Open the create modal
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenCreateModalAsync();
    }

    /// <summary>
    /// When: I fill in the transaction fields:
    /// </summary>
    protected async Task WhenIFillInTheTransactionFields(DataTable fieldsTable)
    {
        // When: Parse field values from table
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

        // And: Fill each field if present
        if (fieldsTable.TryGetKeyValue("Date", out var date))
            await transactionsPage.FillCreateDateAsync(date!);

        if (fieldsTable.TryGetKeyValue("Payee", out var payee))
        {
            await transactionsPage.FillCreatePayeeAsync(payee!);
            _objectStore.Add("NewPayee", payee!);
        }

        if (fieldsTable.TryGetKeyValue("Amount", out var amountStr))
            await transactionsPage.FillCreateAmountAsync(decimal.Parse(amountStr!));

        if (fieldsTable.TryGetKeyValue("Memo", out var memo))
            await transactionsPage.FillCreateMemoAsync(memo!);

        if (fieldsTable.TryGetKeyValue("Source", out var source))
            await transactionsPage.FillCreateSourceAsync(source!);

        if (fieldsTable.TryGetKeyValue("ExternalId", out var externalId))
            await transactionsPage.FillCreateExternalIdAsync(externalId!);
    }

    /// <summary>
    /// When: I click "Create"
    /// </summary>
    protected async Task WhenIClickCreate()
    {
        // When: Submit the create form
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.SubmitCreateFormAsync();
    }

    #endregion

    #region Then Steps

    /// <summary>
    /// Then: I should see a modal titled {expectedTitle}
    /// </summary>
    protected async Task ThenIShouldSeeAModalTitled(string expectedTitle)
    {
        // Then: Wait for the edit modal to be visible
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.EditModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // And: Get the modal title from the modal header
        var modalTitle = await transactionsPage.EditModal.Locator("h5, .modal-title").First.TextContentAsync();

        Assert.That(modalTitle, Is.EqualTo(expectedTitle),
            $"Expected modal title to be '{expectedTitle}' but was '{modalTitle}'");

        // And: Store modal title for future verification
        _objectStore.Add(KEY_MODAL_TITLE, modalTitle ?? string.Empty);
    }

    /// <summary>
    /// Then: I should only see fields for "Payee" and "Memo"
    /// </summary>
    protected async Task ThenIShouldOnlySeeFieldsForPayeeAndMemo()
    {
        // Then: Verify Payee field is visible
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        var payeeVisible = await transactionsPage.EditPayeeInput.IsVisibleAsync();
        Assert.That(payeeVisible, Is.True, "Payee field should be visible in quick edit modal");

        // And: Verify Memo field is visible
        var memoVisible = await transactionsPage.EditMemoInput.IsVisibleAsync();
        Assert.That(memoVisible, Is.True, "Memo field should be visible in quick edit modal");
    }

    /// <summary>
    /// Then: I should not see fields for "Date", "Amount", "Source", or "ExternalId"
    /// </summary>
    protected async Task ThenIShouldNotSeeFieldsForDateAmountSourceOrExternalId()
    {
        // Then: Verify Date field is not visible (doesn't exist in quick edit modal)
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
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

    /// <summary>
    /// Then: the memo should be updated to {expectedMemo}
    /// </summary>
    protected async Task ThenTheMemoShouldBeUpdatedTo(string expectedMemo)
    {
        // Then: Get the payee from object store
        var payee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);

        // And: Verify the memo in the transaction list
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        var actualMemo = await transactionsPage.GetTransactionMemoAsync(payee);

        Assert.That(actualMemo?.Trim(), Is.EqualTo(expectedMemo),
            $"Expected memo to be '{expectedMemo}' but was '{actualMemo}'");
    }

    /// <summary>
    /// Then: I should see the Transaction Details page
    /// </summary>
    protected async Task ThenIShouldSeeTheTransactionDetailsPage()
    {
        // Then: Wait for navigation to details page
        await Page.WaitForURLAsync("**/transactions/**", new() { Timeout = 5000 });

        // And: Verify we're on a transaction details page (URL contains /transactions/{guid})
        var url = Page.Url;
        Assert.That(url, Does.Match(@"/transactions/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"),
            "Should be on transaction details page");
    }

    /// <summary>
    /// Then: I should see all transaction fields:
    /// </summary>
    protected async Task ThenIShouldSeeAllTransactionFields(DataTable expectedFieldsTable)
    {
        // Then: Get the TransactionDetailsPage
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.WaitForPageReadyAsync();

        // And: Verify each field from the table
        if (expectedFieldsTable.TryGetKeyValue("Date", out var expectedDate))
        {
            var dateValue = await detailsPage.EditDateInput.InputValueAsync();
            Assert.That(dateValue, Is.EqualTo(expectedDate), $"Date field should be '{expectedDate}'");
        }

        if (expectedFieldsTable.TryGetKeyValue("Payee", out var expectedPayee))
        {
            var payeeValue = await detailsPage.GetPayeeAsync();
            Assert.That(payeeValue, Is.EqualTo(expectedPayee), $"Payee field should be '{expectedPayee}'");
        }

        if (expectedFieldsTable.TryGetKeyValue("Amount", out var expectedAmount))
        {
            var amountValue = await detailsPage.GetAmountAsync();
            Assert.That(amountValue, Does.Contain(expectedAmount), $"Amount field should contain '{expectedAmount}'");
        }

        if (expectedFieldsTable.TryGetKeyValue("Memo", out var expectedMemo))
        {
            var memoValue = await detailsPage.GetMemoAsync();
            Assert.That(memoValue, Is.EqualTo(expectedMemo), $"Memo field should be '{expectedMemo}'");
        }

        if (expectedFieldsTable.TryGetKeyValue("Source", out var expectedSource))
        {
            var sourceValue = await detailsPage.GetSourceAsync();
            Assert.That(sourceValue, Is.EqualTo(expectedSource), $"Source field should be '{expectedSource}'");
        }

        if (expectedFieldsTable.TryGetKeyValue("ExternalId", out var expectedExternalId))
        {
            var externalIdValue = await detailsPage.GetExternalIdAsync();
            Assert.That(externalIdValue, Is.EqualTo(expectedExternalId), $"ExternalId field should be '{expectedExternalId}'");
        }
    }

    /// <summary>
    /// Then: I should see all create modal fields visible
    /// </summary>
    protected async Task ThenIShouldSeeAllCreateModalFieldsVisible()
    {
        // Then: Verify all fields are visible in create modal
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

        var dateVisible = await transactionsPage.CreateDateInput.IsVisibleAsync();
        Assert.That(dateVisible, Is.True, "Date field should be visible in create modal");

        var payeeVisible = await transactionsPage.CreatePayeeInput.IsVisibleAsync();
        Assert.That(payeeVisible, Is.True, "Payee field should be visible in create modal");

        var amountVisible = await transactionsPage.CreateAmountInput.IsVisibleAsync();
        Assert.That(amountVisible, Is.True, "Amount field should be visible in create modal");

        var memoVisible = await transactionsPage.CreateMemoInput.IsVisibleAsync();
        Assert.That(memoVisible, Is.True, "Memo field should be visible in create modal");

        var sourceVisible = await transactionsPage.CreateSourceInput.IsVisibleAsync();
        Assert.That(sourceVisible, Is.True, "Source field should be visible in create modal");

        var externalIdVisible = await transactionsPage.CreateExternalIdInput.IsVisibleAsync();
        Assert.That(externalIdVisible, Is.True, "ExternalId field should be visible in create modal");
    }

    /// <summary>
    /// Then: the transaction should be created with those values
    /// </summary>
    protected async Task ThenTheTransactionShouldBeCreatedWithThoseValues()
    {
        // Then: Get the new payee from object store
        var newPayee = _objectStore.Get<string>("NewPayee");

        // And: Verify the transaction appears in the list
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForTransactionAsync(newPayee);

        var hasTransaction = await transactionsPage.HasTransactionAsync(newPayee);
        Assert.That(hasTransaction, Is.True, $"Transaction with payee '{newPayee}' should exist in the list");
    }

    #endregion
}
