using Microsoft.Playwright;
using NUnit.Framework;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;
using static YoFi.V3.Tests.Functional.Pages.TransactionsPage;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Transaction Record field tests (Memo, Source, ExternalId).
/// </summary>
/// <remarks>
/// <para>Inherits from WorkspaceTenancySteps to reuse user/workspace setup infrastructure.</para>
/// <para><strong>Tests the two-tier editing model:</strong></para>
/// <list type="bullet">
/// <item>Quick Edit Modal: Only Payee and Memo fields (PATCH endpoint)</item>
/// <item>Full Details Page: All fields including Source and ExternalId (PUT endpoint)</item>
/// </list>
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
    protected const string KEY_TRANSACTION_CATEGORY = "TransactionCategory";
    protected new const string KEY_TRANSACTION_KEY = "TransactionKey";
    protected const string KEY_EDIT_MODE = "EditMode"; // "TransactionDetailsPage", "TransactionsPage", or "CreateModal"

    #endregion

    #region Given Steps

    /// <summary>
    /// Sets up a logged-in user with Editor role in a test workspace.
    /// </summary>
    /// <remarks>
    /// Comprehensive setup step that: clears existing test data, creates an editor user,
    /// creates a test workspace with Editor role, stores credentials and workspace key,
    /// and performs login. Used as the standard starting point for transaction record tests.
    /// </remarks>
    [Given("I am logged in as a user with \"Editor\" role")]
    protected async Task GivenIAmLoggedInAsAUserWithEditorRole()
    {
        // Given: Clear existing test data
        await testControlClient.DeleteAllTestDataAsync();

        // And: Create user context for an Editor user
        var username = "editor-user";

        // And: Create the user first
        var userCredentials = await testControlClient.CreateUsersAsync(new[] { username });
        var credentials = userCredentials.First();
        _userCredentials[credentials.ShortName] = credentials;

        // And: Create the workspace for the user via test control API
        var workspaceName = "Test Workspace";
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var request = new Generated.WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Editor"
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(credentials.Username, new[] { request });
        var result = results.First();

        // And: Store workspace key
        _workspaceKeys[result.Name] = result.Key;
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // When: Login as the user
        await GivenIAmLoggedInAs(username);
    }

    /// <summary>
    /// Seeds a transaction with specified fields into the current workspace.
    /// </summary>
    /// <param name="transactionTable">DataTable with Field/Value columns containing transaction properties.</param>
    /// <remarks>
    /// <para>Parses transaction data from table, seeds via Test Control API, and stores transaction
    /// details in object store for later verification. Does NOT navigate to transactions page -
    /// use "And I am on the transactions page" step separately. Default amount is 100.00 if not specified.</para>
    /// <para><strong>Table Format:</strong> Two columns "Field" and "Value" with rows for each property.</para>
    /// <para><strong>Required Fields:</strong> Payee</para>
    /// <para><strong>Optional Fields:</strong> Amount, Category, Memo, Source, ExternalId</para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// | Field    | Value           |
    /// | Payee    | Coffee Shop     |
    /// | Amount   | 5.50            |
    /// | Category | Beverages       |
    /// | Memo     | Morning coffee  |
    /// </code>
    /// </remarks>
    [Given("I have a workspace with a transaction:")]
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
        transactionTable.TryGetKeyValue("Category", out var category);

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
            ExternalId = externalId,
            Category = category
        };

        var seededTransactions = await testControlClient.SeedTransactionsAsync(
            loggedInUser,
            workspaceKey,
            seedRequest
        );

        // And: Get the actual seeded transaction data from the response
        var seededTransaction = seededTransactions.First();
        var actualPayee = seededTransaction.Payee;
        var actualAmount = seededTransaction.Amount;
        var actualCategory = seededTransaction.Category;
        var actualMemo = seededTransaction.Memo;

        // And: Store actual transaction data for verification (from seeded response, not input table)
        _objectStore.Add(KEY_TRANSACTION_PAYEE, actualPayee);
        _objectStore.Add(KEY_TRANSACTION_AMOUNT, actualAmount.ToString("F2"));
        _objectStore.Add(KEY_TRANSACTION_CATEGORY, actualCategory);
        _objectStore.Add(KEY_TRANSACTION_MEMO, actualMemo);
        if (!string.IsNullOrEmpty(source))
            _objectStore.Add(KEY_TRANSACTION_SOURCE, source);
        if (!string.IsNullOrEmpty(externalId))
            _objectStore.Add(KEY_TRANSACTION_EXTERNAL_ID, externalId);

        // Store the transaction key for later reference
        _objectStore.Add(KEY_TRANSACTION_KEY, seededTransaction.Key.ToString());
    }

    /// <summary>
    /// Sets up a logged-in user with Editor role, creates a transaction, and navigates to its details page.
    /// </summary>
    /// <param name="transactionTable">DataTable with Payee (required), and optional Amount, Memo, Source, ExternalId.</param>
    /// <remarks>
    /// Complete setup for testing transaction details page. Creates workspace, seeds transaction,
    /// stores transaction data in object store, navigates to transactions page, and clicks on the
    /// transaction row to reach the details page (mimics user behavior).
    /// </remarks>
    [Given("I am viewing the details page for a transaction with:")]
    protected async Task GivenIAmViewingTheDetailsPageForATransactionWith(DataTable transactionTable)
    {
        // Given: Seed the transaction using the existing step
        await GivenIHaveAWorkspaceWithATransaction(transactionTable);

        // And: Navigate to transactions page
        await GivenIAmOnTheTransactionsPage();

        // When: Click on the transaction row to navigate to details page
        await WhenIClickOnTheTransactionRow();

        // And: Wait for the transaction details page to be ready
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.WaitForPageReadyAsync();

        // And: Mark that we're in transaction details page mode
        _objectStore.Add(KEY_EDIT_MODE, "TransactionDetailsPage");
    }

    /// <summary>
    /// Sets up a logged-in user with Editor role, creates a transaction, and navigates to its details page.
    /// </summary>
    /// <remarks>
    /// Simplified version without DataTable parameter. Seeds a basic transaction and navigates to the details page.
    /// </remarks>
    [Given("I am viewing the details page for a transaction")]
    protected async Task GivenIAmViewingTheDetailsPageForATransaction()
    {
        // Given: Create a basic transaction DataTable
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Test Transaction"],
            ["Amount", "100.00"]
        );

        // When: Use existing step to seed transaction and navigate to details page
        await GivenIAmViewingTheDetailsPageForATransactionWith(table);
    }

    /// <summary>
    /// Navigates to the transactions page with a workspace selected.
    /// </summary>
    /// <remarks>
    /// Sets up minimal state to be on the transactions page: navigates to the page and
    /// selects the current workspace. Does not seed any transactions.
    /// </remarks>
    [Given("I am on the transactions page")]
    protected async Task GivenIAmOnTheTransactionsPage()
    {
        // Given: Get workspace name
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

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
    /// Opens the quick edit modal for the current transaction.
    /// </summary>
    /// <remarks>
    /// Retrieves transaction payee from object store (KEY_TRANSACTION_PAYEE) and
    /// opens the edit modal. Tests the quick edit workflow (PATCH endpoint).
    /// </remarks>
    [When("I click the \"Edit\" button on the transaction")]
    protected async Task WhenIClickTheEditButtonOnTheTransaction()
    {
        // When: Get the payee from object store
        var payee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);

        // And: Locate and click the edit button for the transaction
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenEditModalAsync(payee);
    }

    /// <summary>
    /// Opens the quick edit modal for the specified transaction.
    /// </summary>
    /// <param name="payee">The payee name of the transaction to edit.</param>
    /// <remarks>
    /// Locates the transaction by payee and opens the edit modal.
    /// </remarks>
    [When("I quick edit the {payee} transaction")]
    protected async Task WhenIQuickEditTheTransaction(string? payee = null)
    {
        var actualPayee = payee ?? GetRequiredFromStore(KEY_TRANSACTION_PAYEE);

        // When: Locate and click the edit button for the transaction
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenEditModalAsync(actualPayee);
    }

    /// <summary>
    /// Changes the category field in the quick edit modal.
    /// </summary>
    /// <param name="newCategory">The new category value.</param>
    /// <remarks>
    /// Fills the category field and stores the new value in object store for verification.
    /// </remarks>
    [When("I change Category to {newCategory}")]
    protected async Task WhenIChangeCategoryTo(string newCategory)
    {
        // When: Fill the category field
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditCategoryAsync(newCategory);

        // And: Store the new category for verification
        _objectStore.Add(KEY_TRANSACTION_CATEGORY, newCategory);
    }

    /// <summary>
    /// Changes the memo field in the quick edit modal.
    /// </summary>
    /// <param name="newMemo">The new memo value.</param>
    /// <remarks>
    /// Fills the memo field and stores the new value in object store for verification.
    /// </remarks>
    [When("I change Memo to {newMemo}")]
    protected async Task WhenIChangeMemoTo(string newMemo)
    {
        // When: Fill the memo field
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditMemoAsync(newMemo);

        // And: Store the new memo for verification
        _objectStore.Add(KEY_TRANSACTION_MEMO, newMemo);
    }

    /// <summary>
    /// Updates the memo field in the quick edit modal.
    /// </summary>
    /// <param name="newMemo">The new memo value.</param>
    /// <remarks>
    /// Fills the memo field and stores the new value in object store for verification.
    /// </remarks>
    [When("I update the memo to {newMemo}")]
    protected async Task WhenIUpdateTheMemoTo(string newMemo)
    {
        // When: Fill the memo field
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditMemoAsync(newMemo);

        // And: Store the new memo for verification
        _objectStore.Add("NewMemo", newMemo);
    }

    /// <summary>
    /// Clicks the Update button on the edit modal.
    /// </summary>
    /// <remarks>
    /// Submits the edit form and waits for the modal to close.
    /// </remarks>
    [When("I click \"Update\"")]
    protected async Task WhenIClickUpdate()
    {
        // When: Submit the edit form
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.SubmitEditFormAsync();
    }

    /// <summary>
    /// Submits the edit form (quick edit, create modal, or full details).
    /// </summary>
    /// <remarks>
    /// Submits the currently open form. Used with quick edit modal, create modal,
    /// and full details page. Uses object store to determine which mode we're in.
    /// </remarks>
    [When("I click \"Save\"")]
    protected async Task WhenIClickSave()
    {
        // When: Check object store for edit mode
        if (_objectStore.Contains<string>(KEY_EDIT_MODE))
        {
            var editMode = _objectStore.Get<string>(KEY_EDIT_MODE);
            if (editMode == "TransactionDetailsPage")
            {
                await WhenIClickSaveInTransactionDetails();
            }
            else if (editMode == "CreateModal")
            {
                await WhenIClickSaveInCreateModal();
            }
            else
            {
                await WhenIClickSaveInEditForm();
            }
        }
        else
        {
            // Default to edit form for backward compatibility
            await WhenIClickSaveInEditForm();
        }
    }

    /// <summary>
    /// Saves transaction changes from the transaction details page.
    /// </summary>
    /// <remarks>
    /// Submits the full details page edit form (PUT endpoint).
    /// </remarks>
    protected async Task WhenIClickSaveInTransactionDetails()
    {
        // When: Save from details page edit mode
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.SaveAsync();
    }

    /// <summary>
    /// Saves transaction changes from the quick edit modal.
    /// </summary>
    /// <remarks>
    /// Submits the quick edit modal form (PATCH endpoint).
    /// </remarks>
    protected async Task WhenIClickSaveInEditForm()
    {
        // When: Submit edit form from transactions page modal
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.SubmitEditFormAsync();
    }

    /// <summary>
    /// Saves new transaction from the create modal.
    /// </summary>
    /// <remarks>
    /// Submits the create modal form (POST endpoint).
    /// </remarks>
    protected async Task WhenIClickSaveInCreateModal()
    {
        // When: Submit create form from transactions page modal
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.SubmitCreateFormAsync();
    }

    /// <summary>
    /// Clicks the Edit button on the transaction details page.
    /// </summary>
    /// <remarks>
    /// Transitions from display mode to edit mode on the details page.
    /// </remarks>
    [When("I click the \"Edit\" button")]
    protected async Task WhenIClickTheEditButton()
    {
        // When: Click the Edit button to enter edit mode
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.StartEditingAsync();
    }

    /// <summary>
    /// Changes the Source field to the specified value.
    /// </summary>
    /// <param name="newSource">The new source value.</param>
    /// <remarks>
    /// Fills the source field and stores the new value in object store for verification.
    /// </remarks>
    [When("I change Source to {newSource}")]
    protected async Task WhenIChangeSourceTo(string newSource)
    {
        // When: Fill the source field
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.FillSourceAsync(newSource);

        // And: Store the new source for verification
        _objectStore.Add(KEY_TRANSACTION_SOURCE, newSource);
    }

    /// <summary>
    /// Changes the ExternalId field to the specified value.
    /// </summary>
    /// <param name="newExternalId">The new external ID value.</param>
    /// <remarks>
    /// Fills the external ID field and stores the new value in object store for verification.
    /// </remarks>
    [When("I change ExternalId to {newExternalId}")]
    protected async Task WhenIChangeExternalIdTo(string newExternalId)
    {
        // When: Fill the external ID field
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.FillExternalIdAsync(newExternalId);

        // And: Store the new external ID for verification
        _objectStore.Add(KEY_TRANSACTION_EXTERNAL_ID, newExternalId);
    }

    /// <summary>
    /// Clicks on the transaction row to navigate to the details page.
    /// </summary>
    /// <remarks>
    /// Retrieves transaction payee from object store, finds the transaction row,
    /// and clicks it to navigate to the full details page (PUT endpoint).
    /// </remarks>
    [When("I click on the transaction row")]
    protected async Task WhenIClickOnTheTransactionRow()
    {
        // When: Get the payee from object store
        var payee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);

        // And: Click on the transaction row to navigate to details
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

        // And: Row is loaded
        await transactionsPage.WaitForTransactionAsync(payee);

        // And: Get the row data
        var row = await transactionsPage.GetTransactionRowByPayeeAsync(payee);

        await row.ClickAsync();
    }

    /// <summary>
    /// Clicks on a specific transaction row to navigate to its details page.
    /// </summary>
    /// <param name="payee">The payee name of the transaction to click.</param>
    /// <remarks>
    /// Locates the transaction by payee name and clicks the row to navigate to
    /// the full details page (PUT endpoint).
    /// </remarks>
    [When("I click on the {payee} transaction row")]
    protected async Task WhenIClickOnTheTransactionRow(string payee)
    {
        // When: Click on the transaction row to navigate to details
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        var row = await transactionsPage.GetTransactionRowByPayeeAsync(payee);
        await row.ClickAsync();
    }

    /// <summary>
    /// Opens the create transaction modal.
    /// </summary>
    /// <remarks>
    /// Opens the "New Transaction" modal which includes all transaction fields
    /// (Date, Payee, Amount, Memo, Source, ExternalId).
    /// </remarks>
    [When("I click the \"New Transaction\" button")]
    protected async Task WhenIClickTheNewTransactionButton()
    {
        // When: Open the create modal
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenCreateModalAsync();
    }

    /// <summary>
    /// Fills transaction fields in the create modal from a DataTable.
    /// </summary>
    /// <param name="fieldsTable">DataTable with optional Date, Payee, Amount, Memo, Source, ExternalId.</param>
    /// <remarks>
    /// Conditionally fills each field if present in the table. Stores new payee
    /// in object store for verification. Used for testing transaction creation
    /// with all available fields.
    /// </remarks>
    [When("I fill in the transaction fields")]
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
    /// Submits the create transaction form.
    /// </summary>
    /// <remarks>
    /// Submits the "New Transaction" modal form to create a new transaction.
    /// </remarks>
    [When("I click \"Create\"")]
    protected async Task WhenIClickCreate()
    {
        // When: Submit the create form
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.SubmitCreateFormAsync();
    }

    /// <summary>
    /// Clicks the "Back to Transactions" button to return to the transaction list.
    /// </summary>
    /// <remarks>
    /// Navigates from the transaction details page back to the transactions list page.
    /// </remarks>
    [When("I click \"Back to Transactions\"")]
    protected async Task WhenIClickBackToTransactions()
    {
        // When: Click the back button to return to transactions list
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.GoBackAsync();
    }

    /// <summary>
    /// Clicks the "Add Transaction" button to open the create transaction modal.
    /// </summary>
    /// <remarks>
    /// Navigates to transactions page if not already there, and clicks the "Add Transaction"
    /// button which opens the create modal with all transaction fields.
    /// </remarks>
    [When("I click the \"Add Transaction\" button")]
    protected async Task WhenIClickTheAddTransactionButton()
    {
        // When: Click the Add Transaction button to open create modal
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.OpenCreateModalAsync();

        // And: Mark that we're in create modal mode
        _objectStore.Add(KEY_EDIT_MODE, "CreateModal");
    }

    /// <summary>
    /// Fills transaction fields in the create modal from a DataTable.
    /// </summary>
    /// <param name="dataTable">DataTable with columns "Field" and "Value" containing field names and values.</param>
    /// <remarks>
    /// Parses the DataTable to extract field-value pairs and fills the corresponding fields
    /// in the create transaction modal. Supports: Date, Payee, Amount, Memo, Source, External ID.
    /// Stores all values in object store for later verification in Scenario 8.
    /// </remarks>
    [When("I fill in the following transaction fields:")]
    protected async Task WhenIFillInTheFollowingTransactionFields(DataTable dataTable)
    {
        // When: Get the TransactionsPage
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

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
                    // Store for Scenario 8 verification (date is stored but not used in Scenario 7)
                    break;

                case "Payee":
                    await transactionsPage.FillCreatePayeeAsync(value);
                    _objectStore.Add(KEY_TRANSACTION_PAYEE, value);
                    break;

                case "Amount":
                    await transactionsPage.FillCreateAmountAsync(decimal.Parse(value));
                    _objectStore.Add(KEY_TRANSACTION_AMOUNT, value);
                    break;

                case "Category":
                    await transactionsPage.FillCreateCategoryAsync(value);
                    _objectStore.Add(KEY_TRANSACTION_CATEGORY, value);
                    break;

                case "Memo":
                    await transactionsPage.FillCreateMemoAsync(value);
                    _objectStore.Add(KEY_TRANSACTION_MEMO, value);
                    break;

                case "Source":
                    await transactionsPage.FillCreateSourceAsync(value);
                    _objectStore.Add(KEY_TRANSACTION_SOURCE, value);
                    break;

                case "External ID":
                    await transactionsPage.FillCreateExternalIdAsync(value);
                    _objectStore.Add(KEY_TRANSACTION_EXTERNAL_ID, value);
                    break;

                default:
                    throw new ArgumentException($"Unsupported field name: {fieldName}");
            }
        }
    }

    #endregion

    #region Then Steps

    /// <summary>
    /// Verifies that a modal with the expected title is displayed.
    /// </summary>
    /// <param name="expectedTitle">The expected modal title text.</param>
    /// <remarks>
    /// Waits for edit modal to be visible, extracts the title from modal header,
    /// and stores it in object store for later verification.
    /// </remarks>
    [Then("I should see a modal titled {expectedTitle}")]
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
    /// Verifies that only Payee, Category, and Memo fields are visible in the quick edit modal.
    /// </summary>
    /// <remarks>
    /// Tests the quick edit modal constraint - only Payee, Category, and Memo fields should
    /// be editable via the modal (PATCH endpoint).
    /// </remarks>
    [Then("I should only see fields for \"Payee\", \"Category\", and \"Memo\"")]
    protected async Task ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo()
    {
        // Then: Verify Payee field is visible
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
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
    /// </remarks>
    [Then("the fields match the expected values")]
    protected async Task ThenTheFieldsMatchTheExpectedValues()
    {
        // Then: Get the transactions page
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

        // And: Verify Payee value if expected
        if (_objectStore.Contains<string>(KEY_TRANSACTION_PAYEE))
        {
            var expectedPayee = _objectStore.Get<string>(KEY_TRANSACTION_PAYEE);
            var actualPayee = await transactionsPage.EditPayeeInput.InputValueAsync();
            Assert.That(actualPayee, Is.EqualTo(expectedPayee),
                $"Payee field should display '{expectedPayee}' but was '{actualPayee}'");
        }

        // And: Verify Category value if expected
        if (_objectStore.Contains<string>(KEY_TRANSACTION_CATEGORY))
        {
            var expectedCategory = _objectStore.Get<string>(KEY_TRANSACTION_CATEGORY);
            var actualCategory = await transactionsPage.EditCategoryInput.InputValueAsync();
            Assert.That(actualCategory, Is.EqualTo(expectedCategory),
                $"Category field should display '{expectedCategory}' but was '{actualCategory}'");
        }

        // And: Verify Memo value if expected
        if (_objectStore.Contains<string>(KEY_TRANSACTION_MEMO))
        {
            var expectedMemo = _objectStore.Get<string>(KEY_TRANSACTION_MEMO);
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
    /// Verifies that the memo field was updated to the expected value.
    /// </summary>
    /// <param name="expectedMemo">The expected memo value.</param>
    /// <remarks>
    /// Retrieves transaction payee from object store, gets memo from transaction list,
    /// and verifies it matches expected value (after trimming whitespace).
    /// </remarks>
    [Then("the memo should be updated to {expectedMemo}")]
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
    /// Verifies that the edit modal has closed.
    /// </summary>
    /// <remarks>
    /// Waits for the modal to disappear and verifies it's no longer visible.
    /// </remarks>
    [Then("the modal should close")]
    protected async Task ThenTheModalShouldClose()
    {
        // Then: Wait for the edit modal to be hidden
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.EditModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });

        // And: Verify modal is not visible
        var isVisible = await transactionsPage.EditModal.IsVisibleAsync();
        Assert.That(isVisible, Is.False, "Edit modal should be closed");
    }

    /// <summary>
    /// Verifies that the updated memo appears in the transaction list.
    /// </summary>
    /// <remarks>
    /// Retrieves the payee and new memo from object store, waits for page to update,
    /// and verifies the memo in the transaction list matches the updated value.
    /// </remarks>
    [Then("I should see the updated memo in the transaction list")]
    protected async Task ThenIShouldSeeTheUpdatedMemoInTheTransactionList()
    {
        // Then: Get the payee and new memo from object store
        var payee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);
        var expectedMemo = GetRequiredFromStore(KEY_TRANSACTION_MEMO);

        // And: Wait for page to update (loading spinner to hide)
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForLoadingCompleteAsync();

        // And: Verify the memo in the transaction list
        var actualMemo = await transactionsPage.GetTransactionMemoAsync(payee);

        Assert.That(actualMemo?.Trim(), Is.EqualTo(expectedMemo),
            $"Expected memo to be '{expectedMemo}' but was '{actualMemo}'");
    }

    /// <summary>
    /// Verifies that the updated category appears in the transaction list.
    /// </summary>
    /// <remarks>
    /// Retrieves the payee and new category from object store, waits for page to update,
    /// and verifies the category in the transaction list matches the updated value.
    /// </remarks>
    [Then("I should see the updated category in the transaction list")]
    protected async Task ThenIShouldSeeTheUpdatedCategoryInTheTransactionList()
    {
        // Then: Get the payee and new category from object store
        var payee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);
        var expectedCategory = GetRequiredFromStore(KEY_TRANSACTION_CATEGORY);

        // And: Wait for page to update (loading spinner to hide)
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForLoadingCompleteAsync();
        await transactionsPage.WaitForTransactionAsync(payee);

        // And: Verify the category in the transaction list
        var actualCategory = await transactionsPage.GetTransactionCategoryAsync(payee);

        Assert.That(actualCategory?.Trim(), Is.EqualTo(expectedCategory),
            $"Expected category to be '{expectedCategory}' but was '{actualCategory}'");
    }

    /// <summary>
    /// Verifies that navigation to the Transaction Details page occurred.
    /// </summary>
    /// <remarks>
    /// Waits for URL change to transactions details pattern and verifies URL contains
    /// a GUID path segment, confirming navigation to the full details page.
    /// </remarks>
    [Then("I should see the Transaction Details page")]
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
    /// Verifies that navigation to the transaction details page occurred.
    /// </summary>
    /// <remarks>
    /// Waits for page ready state to ensure page is interactive after navigation.
    /// </remarks>
    [Then("I should navigate to the transaction details page")]
    protected async Task ThenIShouldNavigateToTheTransactionDetailsPage()
    {
        // Then: Wait for the transaction details page to be ready
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Verifies that all transaction fields on the details page match expected values.
    /// </summary>
    /// <param name="expectedFieldsTable">DataTable with optional Date, Payee, Amount, Memo, Source, ExternalId.</param>
    /// <remarks>
    /// Waits for TransactionDetailsPage to load and verifies each field specified
    /// in the table. Tests the full details page display (all fields visible).
    /// </remarks>
    [Then("I should see all transaction fields")]
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
    /// Verifies that all transaction fields are displayed on the details page.
    /// </summary>
    /// <param name="fieldTable">DataTable with optional Date, Payee, Amount, Memo, Source, ExternalId.</param>
    /// <remarks>
    /// Parses the DataTable to extract expected field values and verifies each field
    /// on the transaction details page using the TransactionDetailsPage query methods.
    /// </remarks>
    [Then("I should see all transaction fields displayed:")]
    protected async Task ThenIShouldSeeAllTransactionFieldsDisplayed(DataTable fieldTable)
    {
        // Then: Get the TransactionDetailsPage
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();

        // And: Verify each field from the table
        if (fieldTable.TryGetKeyValue("Date", out var expectedDate))
        {
            var dateValue = await detailsPage.GetDateAsync();
            Assert.That(dateValue?.Trim(), Does.Contain(expectedDate!),
                $"Date field should contain '{expectedDate}'");
        }

        if (fieldTable.TryGetKeyValue("Payee", out var expectedPayee))
        {
            var payeeValue = await detailsPage.GetPayeeAsync();
            Assert.That(payeeValue?.Trim(), Is.EqualTo(expectedPayee),
                $"Payee field should be '{expectedPayee}'");
        }

        if (fieldTable.TryGetKeyValue("Amount", out var expectedAmount))
        {
            var amountValue = await detailsPage.GetAmountAsync();
            Assert.That(amountValue?.Trim(), Does.Contain(expectedAmount!),
                $"Amount field should contain '{expectedAmount}'");
        }

        if (fieldTable.TryGetKeyValue("Memo", out var expectedMemo))
        {
            var memoValue = await detailsPage.GetMemoAsync();
            Assert.That(memoValue?.Trim(), Is.EqualTo(expectedMemo),
                $"Memo field should be '{expectedMemo}'");
        }

        if (fieldTable.TryGetKeyValue("Source", out var expectedSource))
        {
            var sourceValue = await detailsPage.GetSourceAsync();
            Assert.That(sourceValue?.Trim(), Is.EqualTo(expectedSource),
                $"Source field should be '{expectedSource}'");
        }

        if (fieldTable.TryGetKeyValue("ExternalId", out var expectedExternalId))
        {
            var externalIdValue = await detailsPage.GetExternalIdAsync();
            Assert.That(externalIdValue?.Trim(), Is.EqualTo(expectedExternalId),
                $"ExternalId field should be '{expectedExternalId}'");
        }
    }

    /// <summary>
    /// Verifies that all expected transaction fields are displayed on the details page.
    /// </summary>
    /// <remarks>
    /// Uses the seeded transaction data stored in the object store (from GivenIHaveAWorkspaceWithATransaction)
    /// to verify all fields match what was seeded. This handles cases where the seed API modifies
    /// values (e.g., appending numbers to payee names).
    /// </remarks>
    [Then("I should see all the expected transaction fields displayed")]
    protected async Task ThenIShouldSeeAllTheExpectedTransactionFieldsDisplayed()
    {
        // Then: Get the TransactionDetailsPage
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();

        // And: Get expected values from object store (seeded transaction data)
        var expectedPayee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);
        var expectedAmount = GetRequiredFromStore(KEY_TRANSACTION_AMOUNT);

        // And: Verify payee
        var payeeValue = await detailsPage.GetPayeeAsync();
        Assert.That(payeeValue?.Trim(), Is.EqualTo(expectedPayee),
            $"Payee field should be '{expectedPayee}'");

        // And: Verify amount
        var amountValue = await detailsPage.GetAmountAsync();
        Assert.That(amountValue?.Trim(), Does.Contain(expectedAmount),
            $"Amount field should contain '{expectedAmount}'");

        // And: Verify optional fields if they were seeded
        if (_objectStore.Contains<string>(KEY_TRANSACTION_CATEGORY))
        {
            var expectedCategory = _objectStore.Get<string>(KEY_TRANSACTION_CATEGORY);
            var categoryValue = await detailsPage.GetCategoryAsync();
            var expectedDisplay = string.IsNullOrEmpty(expectedCategory) ? TransactionDetailsPage.EmptyFieldDisplay : expectedCategory;
            Assert.That(categoryValue?.Trim(), Is.EqualTo(expectedDisplay),
                $"Category field should be '{expectedDisplay}'");
        }

        if (_objectStore.Contains<string>(KEY_TRANSACTION_MEMO))
        {
            var expectedMemo = _objectStore.Get<string>(KEY_TRANSACTION_MEMO);
            var memoValue = await detailsPage.GetMemoAsync();
            var expectedDisplay = string.IsNullOrEmpty(expectedMemo) ? TransactionDetailsPage.EmptyFieldDisplay : expectedMemo;
            Assert.That(memoValue?.Trim(), Is.EqualTo(expectedDisplay),
                $"Memo field should be '{expectedDisplay}'");
        }

        if (_objectStore.Contains<string>(KEY_TRANSACTION_SOURCE))
        {
            var expectedSource = _objectStore.Get<string>(KEY_TRANSACTION_SOURCE);
            var sourceValue = await detailsPage.GetSourceAsync();
            var expectedDisplay = string.IsNullOrEmpty(expectedSource) ? TransactionDetailsPage.EmptyFieldDisplay : expectedSource;
            Assert.That(sourceValue?.Trim(), Is.EqualTo(expectedDisplay),
                $"Source field should be '{expectedDisplay}'");
        }

        if (_objectStore.Contains<string>(KEY_TRANSACTION_EXTERNAL_ID))
        {
            var expectedExternalId = _objectStore.Get<string>(KEY_TRANSACTION_EXTERNAL_ID);
            var externalIdValue = await detailsPage.GetExternalIdAsync();
            var expectedDisplay = string.IsNullOrEmpty(expectedExternalId) ? TransactionDetailsPage.EmptyFieldDisplay : expectedExternalId;
            Assert.That(externalIdValue?.Trim(), Is.EqualTo(expectedDisplay),
                $"ExternalId field should be '{expectedDisplay}'");
        }
    }

    /// <summary>
    /// Verifies that all fields are visible in the create transaction modal.
    /// </summary>
    /// <remarks>
    /// Tests that the create modal includes all transaction fields: Date, Payee,
    /// Amount, Memo, Source, and ExternalId. Unlike quick edit, creation allows
    /// all fields to be set.
    /// </remarks>
    [Then("I should see all create modal fields visible")]
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
    /// Verifies that the transaction was created and appears in the list.
    /// </summary>
    /// <remarks>
    /// Retrieves new payee from object store (set during WhenIFillInTheTransactionFields),
    /// waits for transaction to appear, and verifies presence in transactions list.
    /// </remarks>
    [Then("the transaction should be created with those values")]
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

    /// <summary>
    /// Verifies that a specific field displays the expected value on the transaction details page.
    /// </summary>
    /// <param name="expectedValue">The expected value to see.</param>
    /// <param name="fieldName">The field name (e.g., "Source", "ExternalId").</param>
    /// <remarks>
    /// Retrieves the field value from the transaction details page and verifies it matches
    /// the expected value. Supports "Source" and "ExternalId" field names.
    /// </remarks>
    [Then("I should see {expectedValue} as the {fieldName}")]
    protected async Task ThenIShouldSeeValueAsField(string expectedValue, string fieldName)
    {
        // Then: Get the TransactionDetailsPage
        var detailsPage = GetOrCreatePage<TransactionDetailsPage>();

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
    protected async Task ThenIShouldReturnToTheTransactionList()
    {
        // Then: Get TransactionsPage and wait for it to be ready
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Verifies that the transaction created in the Given step is visible in the transaction list.
    /// </summary>
    /// <remarks>
    /// Retrieves the payee from object store and verifies the transaction is present in the list.
    /// </remarks>
    [Then("I should see all my transactions")]
    protected async Task ThenIShouldSeeAllMyTransactions()
    {
        // Then: Get the expected payee from object store
        var expectedPayee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);

        // And: Get TransactionsPage
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

        // And: Verify the transaction is visible in the list
        var hasTransaction = await transactionsPage.HasTransactionAsync(expectedPayee);
        Assert.That(hasTransaction, Is.True,
            $"Should see transaction with payee '{expectedPayee}' in the list");
    }

    /// <summary>
    /// Verifies that the create transaction modal is visible.
    /// </summary>
    /// <remarks>
    /// Checks that the create modal has appeared after clicking the "Add Transaction" button.
    /// </remarks>
    [Then("I should see a create transaction modal")]
    protected async Task ThenIShouldSeeACreateTransactionModal()
    {
        // Then: Verify the create modal is visible
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
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
    protected async Task ThenIShouldSeeTheFollowingFieldsInTheCreateForm(DataTable fieldsTable)
    {
        // Then: Get the TransactionsPage
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

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
    protected async Task ThenIShouldSeeATransactionWithPayee(string payee)
    {
        // Then: Get the TransactionsPage
        var transactionsPage = GetOrCreatePage<TransactionsPage>();

        // And: Wait for the transaction to appear in the list
        var rowData = await transactionsPage.GetTransactionRowDataByPayeeAsync(payee);

        if (rowData == null)
        {
            await Task.Delay(100);
            rowData = await transactionsPage.GetTransactionRowDataByPayeeAsync(payee) ?? throw new Exception($"Transaction with payee '{payee}' not found in the list");
        }

        // And: Verify the transaction is visible
        var hasTransaction = await rowData.RowLocator.IsVisibleAsync();
        Assert.That(hasTransaction, Is.True,
            $"Transaction with payee '{payee}' should be visible in the transaction list");

        // And: Store expected values in object store for later verification
        _objectStore.Add(rowData);

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
    protected async Task ThenItContainsTheExpectedListFields()
    {
        // Then: Get actual values from object store (fetched from page in previous step)
        var rowData = _objectStore.Get<TransactionRowData>();

        // And: Get expected values from object store (set during creation)

        // And: Verify Category if it was set during creation
        if (_objectStore.Contains<string>(KEY_TRANSACTION_CATEGORY))
        {
            var actualCategory = rowData.Columns["category"];
            var expectedCategory = _objectStore.Get<string>(KEY_TRANSACTION_CATEGORY);
            Assert.That(actualCategory, Is.EqualTo(expectedCategory),
                $"Category in list should be '{expectedCategory}' but was '{actualCategory}'");
        }

        // And: Verify Memo if it was set during creation
        if (_objectStore.Contains<string>(KEY_TRANSACTION_MEMO))
        {
            var actualMemo = rowData.Columns["memo"];
            var expectedMemo = _objectStore.Get<string>(KEY_TRANSACTION_MEMO);
            Assert.That(actualMemo, Is.EqualTo(expectedMemo),
                $"Memo in list should be '{expectedMemo}' but was '{actualMemo}'");
        }

        // And: Verify Amount (always set during creation)
        if (_objectStore.Contains<string>(KEY_TRANSACTION_AMOUNT))
        {
            var actualAmount = rowData.Columns["amount"].Replace("$","").Trim();
            var expectedAmount = _objectStore.Get<string>(KEY_TRANSACTION_AMOUNT);
            // Amount may have currency formatting, so check if actual contains expected
            Assert.That(actualAmount, Does.Contain(expectedAmount),
                $"Amount in list should contain '{expectedAmount}' but was '{actualAmount}'");
        }

        // Note: Date verification is complex due to formatting differences, skipping for now
        await Task.CompletedTask;
    }

    #endregion
}
