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
        await GivenTheApplicationIsRunning();

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

    #endregion
}
