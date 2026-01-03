using YoFi.V3.Tests.Functional.Steps;
using YoFi.V3.Tests.Functional.Helpers;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// Bank Import
/// </summary>
/// <remarks>
/// Users can upload OFX/QFX bank files and review/import transactions
/// into their workspace. The system detects duplicates and allows selective import.
/// </remarks>
public class BankImportTests : BankImportSteps
{
    [SetUp]
    public async Task Background()
    {
        // Given the application is running
        await GivenLaunchedSite();

        // And I have an existing account
        await GivenIHaveAnExistingAccount();

        // And I have an active workspace "My Finances"
        await GivenIHaveAnActiveWorkspace("My Finances");

        // And I am logged into my existing account
        await GivenIAmLoggedInAs();
    }

    /// <summary>
    /// User uploads bank file and sees import review page
    /// </summary>
    [Test]
    public async Task UserUploadsBankFileAndSeesImportReviewPage()
    {
        // Given I have existing transactions with external IDs:
        var table = new DataTable(
            ["ExternalId", "Date", "Payee", "Amount"],
            ["2024010701", "2024-01-07", "Gas Station", "-89.99"],
            ["2024011201", "2024-01-12", "Online Store", "-199.99"],
            ["2024012201", "2024-01-22", "Rent Payment", "-1200.00"]
        );
        await GivenIHaveExistingTransactionsWithExternalIDs(table);

        // And I am on the import review page
        await GivenIAmOnTheImportReviewPage();

        // When I upload OFX file "checking-jan-2024.ofx"
        await WhenIUploadOFXFile("checking-jan-2024.ofx");

        // Then page should display 15 transactions
        await ThenPageShouldDisplayTransactions(15);

        // And 12 transactions should be selected by default
        await ThenTransactionsShouldBeSelectedByDefault(12);

        // And 3 transactions should be deselected by default
        await ThenTransactionsShouldBeDeselectedByDefault(3);
    }
}
