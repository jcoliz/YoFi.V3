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
    // The quick edit modal should only show Payee and Memo fields for rapid updates

    /// <summary>
    /// Quick edit modal shows only Payee and Memo fields
    /// </summary>
    [Test]
    public async Task QuickEditModalShowsOnlyPayeeAndMemoFields()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Coffee Shop"],
            ["Amount", "5.50"],
            ["Memo", "Morning coffee"]
        );
        await GivenIHaveAWorkspaceWithATransaction(table);

        // When I click the "Edit" button on the transaction
        await WhenIClickTheEditButtonOnTheTransaction();

        // Then I should see a modal titled "Quick Edit Transaction"
        await ThenIShouldSeeAModalTitled("Quick Edit Transaction");

        // And I should only see fields for "Payee" and "Memo"
        await ThenIShouldOnlySeeFieldsForPayeeAndMemo();

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

        // When I quick edit the "Coffee Co" transaction
        await WhenIQuickEditTheTransaction("Coffee Co");

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

    #endregion
}
