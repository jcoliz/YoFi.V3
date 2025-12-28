using NUnit.Framework;
using YoFi.V3.Tests.Functional.Steps;
using YoFi.V3.Tests.Functional.Helpers;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// Transaction Record Fields
/// As a user managing transactions
/// I want to record additional details about each transaction
/// So that I can track memo notes, source accounts, and external identifiers
/// </summary>
public class TransactionRecordFieldsTests : TransactionRecordSteps
{
    [SetUp]
    public async Task SetupAsync()
    {
        // Given the application is running
        await GivenLaunchedSite();

        // And I am logged in as a user with "Editor" role
        await GivenIAmLoggedInAsAUserWithEditorRole();
    }

    #region Rule: Quick Edit Modal
    // The quick edit modal should only show Payee, Category, and Memo fields for rapid updates

    /// <summary>
    /// Quick edit modal shows Payee, Category, and Memo fields
    /// </summary>
    [Test]
    public async Task QuickEditModalShowsPayeeCategoryAndMemoFields()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Coffee Shop"],
            ["Amount", "5.50"],
            ["Category", "Beverages"],
            ["Memo", "Morning coffee"]
        );
        await GivenIHaveAWorkspaceWithATransaction(table);

        // And I am on the transactions page
        await GivenIAmOnTheTransactionsPage();

        // When I click the "Edit" button on the transaction
        await WhenIClickTheEditButtonOnTheTransaction();

        // Then I should see a modal titled "Quick Edit Transaction"
        await ThenIShouldSeeAModalTitled("Quick Edit Transaction");

        // And I should only see fields for "Payee", "Category", and "Memo"
        await ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo();

        // And the fields match the expected values
        await ThenTheFieldsMatchTheExpectedValues();

        // And I should not see fields for "Date", "Amount", "Source", or "ExternalId"
        await ThenIShouldNotSeeFieldsForDateAmountSourceOrExternalId();
    }

    /// <summary>
    /// User updates Memo via quick edit modal
    /// </summary>
    [Test]
    public async Task UserUpdatesMemoViaQuickEditModal()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Coffee Co"],
            ["Amount", "-5.50"],
            ["Memo", "Morning latte"],
            ["Source", "Chase Checking"],
            ["ExternalId", "CHK-001"]
        );
        await GivenIHaveAWorkspaceWithATransaction(table);

        // And I am on the transactions page
        await GivenIAmOnTheTransactionsPage();

        // When I quick edit the transaction
        await WhenIQuickEditTheTransaction();

        // And I change Memo to "Large latte with extra shot"
        await WhenIChangeMemoTo("Large latte with extra shot");

        // And I click "Update"
        await WhenIClickUpdate();

        // Then the modal should close
        await ThenTheModalShouldClose();

        // And I should see the updated memo in the transaction list
        await ThenIShouldSeeTheUpdatedMemoInTheTransactionList();
    }

    /// <summary>
    /// User edits category via quick edit and sees it in list
    /// </summary>
    [Test]
    public async Task UserEditsCategoryViaQuickEditAndSeesItInList()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Grocery Co"],
            ["Amount", "-45.67"],
            ["Category", "Food"]
        );
        await GivenIHaveAWorkspaceWithATransaction(table);

        // And I am on the transactions page
        await GivenIAmOnTheTransactionsPage();

        // When I quick edit the transaction
        await WhenIQuickEditTheTransaction();

        // And I change Category to "Groceries"
        await WhenIChangeCategoryTo("Groceries");

        // And I click "Update"
        await WhenIClickUpdate();

        // Then the modal should close
        await ThenTheModalShouldClose();

        // And I should see the updated category in the transaction list
        await ThenIShouldSeeTheUpdatedCategoryInTheTransactionList();
    }

    #endregion

    #region Rule: Transaction Details Page
    // Users can view, edit, and navigate transaction details

    /// <summary>
    /// Transaction details page displays category
    /// </summary>
    [Test]
    public async Task TransactionDetailsPageDisplaysCategory()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Restaurant XYZ"],
            ["Amount", "-32.50"],
            ["Category", "Dining"]
        );
        await GivenIHaveAWorkspaceWithATransaction(table);

        // And I am on the transactions page
        await GivenIAmOnTheTransactionsPage();

        // When I click on the transaction row
        await WhenIClickOnTheTransactionRow();

        // Then I should navigate to the transaction details page
        await ThenIShouldNavigateToTheTransactionDetailsPage();

        // And I should see all the expected transaction fields displayed
        await ThenIShouldSeeAllTheExpectedTransactionFieldsDisplayed();
    }

    /// <summary>
    /// User navigates from transaction list to details page
    /// </summary>
    [Test]
    public async Task UserNavigatesFromTransactionListToDetailsPage()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Gas Mart"],
            ["Amount", "-40.00"],
            ["Memo", "Fuel up"],
            ["Source", "Chase Checking"],
            ["ExternalId", "CHK-002"]
        );
        await GivenIHaveAWorkspaceWithATransaction(table);

        // And I am on the transactions page
        await GivenIAmOnTheTransactionsPage();

        // When I click on the transaction row
        await WhenIClickOnTheTransactionRow();

        // Then I should navigate to the transaction details page
        await ThenIShouldNavigateToTheTransactionDetailsPage();

        // And I should see all the expected transaction fields displayed
        await ThenIShouldSeeAllTheExpectedTransactionFieldsDisplayed();
    }

    /// <summary>
    /// User edits all fields on transaction details page
    /// </summary>
    [Test]
    public async Task UserEditsAllFieldsOnTransactionDetailsPage()
    {
        // Given I am viewing the details page for a transaction with:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Gas Mart"],
            ["Amount", "-40.00"],
            ["Memo", "Fuel up"],
            ["Source", "Chase Checking"],
            ["ExternalId", "CHK-002"]
        );
        await GivenIAmViewingTheDetailsPageForATransactionWith(table);

        // When I click the "Edit" button
        await WhenIClickTheEditButton();

        // And I change Source to "Chase Visa"
        await WhenIChangeSourceTo("Chase Visa");

        // And I change ExternalId to "VISA-123"
        await WhenIChangeExternalIdTo("VISA-123");

        // And I click "Save"
        await WhenIClickSave();

        // Then I should see "Chase Visa" as the Source
        await ThenIShouldSeeValueAsField("Chase Visa", "Source");

        // And I should see "VISA-123" as the ExternalId
        await ThenIShouldSeeValueAsField("VISA-123", "ExternalId");
    }

    /// <summary>
    /// User returns to list from transaction details page
    /// </summary>
    [Test]
    public async Task UserReturnsToListFromTransactionDetailsPage()
    {
        // Given I am viewing the details page for a transaction
        await GivenIAmViewingTheDetailsPageForATransaction();

        // When I click "Back to Transactions"
        await WhenIClickBackToTransactions();

        // Then I should return to the transaction list
        await ThenIShouldReturnToTheTransactionList();

        // And I should see all my transactions
        await ThenIShouldSeeAllMyTransactions();
    }

    #endregion

    #region Rule: Users can create new transactions with all transaction record fields

    /// <summary>
    /// User sees all fields in create transaction modal
    /// </summary>
    [Test]
    public async Task UserSeesAllFieldsInCreateTransactionModal()
    {
        // Given I am on the transactions page
        await GivenIAmOnTheTransactionsPage();

        // When I click the "Add Transaction" button
        await WhenIClickTheAddTransactionButton();

        // Then I should see a create transaction modal
        await ThenIShouldSeeACreateTransactionModal();

        // And I should see the following fields in the create form:
        var fieldsTable = new DataTable(
            ["Field"],
            ["Date"],
            ["Payee"],
            ["Amount"],
            ["Memo"],
            ["Source"],
            ["External ID"]
        );
        await ThenIShouldSeeTheFollowingFieldsInTheCreateForm(fieldsTable);
    }

    /// <summary>
    /// User creates transaction with all fields populated
    /// </summary>
    [Test]
    public async Task UserCreatesTransactionWithAllFieldsPopulated()
    {
        // Given I am on the transactions page
        await GivenIAmOnTheTransactionsPage();

        // When I click the "Add Transaction" button
        await WhenIClickTheAddTransactionButton();

        // And I fill in the following transaction fields:
        var fieldsTable = new DataTable(
            ["Field", "Value"],
            ["Date", "2024-06-15"],
            ["Payee", "Office Depot"],
            ["Amount", "250.75"],
            ["Category", "Office Supplies"],
            ["Memo", "Printer paper and toner"],
            ["Source", "Business Card"],
            ["External ID", "OD-2024-0615-001"]
        );
        await WhenIFillInTheFollowingTransactionFields(fieldsTable);

        // And I click "Save"
        await WhenIClickSave();

        // Then the modal should close
        await ThenTheModalShouldClose();

        // And I should see a transaction with Payee "Office Depot"
        await ThenIShouldSeeATransactionWithPayee("Office Depot");

        // And it contains the expected list fields
        await ThenItContainsTheExpectedListFields();
    }

    /// <summary>
    /// Created transaction displays all fields on details page
    /// </summary>
    [Test]
    public async Task CreatedTransactionDisplaysAllFieldsOnDetailsPage()
    {
        // Given I am on the transactions page
        await GivenIAmOnTheTransactionsPage();

        // When I click the "Add Transaction" button
        await WhenIClickTheAddTransactionButton();

        // And I fill in the following transaction fields:
        var fieldsTable = new DataTable(
            ["Field", "Value"],
            ["Date", "2024-06-15"],
            ["Payee", "Office Depot"],
            ["Amount", "250.75"],
            ["Category", "Office Supplies"],
            ["Memo", "Printer paper and toner"],
            ["Source", "Business Card"],
            ["External ID", "OD-2024-0615-001"]
        );
        await WhenIFillInTheFollowingTransactionFields(fieldsTable);

        // And I click "Save"
        await WhenIClickSave();

        // And I click on the transaction row
        await WhenIClickOnTheTransactionRow();

        // Then I should see all the expected transaction fields displayed
        await ThenIShouldSeeAllTheExpectedTransactionFieldsDisplayed();
    }

    #endregion
}
