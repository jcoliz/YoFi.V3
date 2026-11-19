using System.Globalization;
using ClosedXML.Excel;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for creating and managing transaction splits via the create modal.
/// </summary>
/// <param name="context">Test context providing access to shared test infrastructure.</param>
/// <remarks>
/// Handles all operations related to the transaction creation modal including opening,
/// field verification, form filling, and validation of created transactions.
/// </remarks>
public class TransactionSplitSteps(ITestContext context) : TransactionStepsBase(context)
{
    /// <summary>
    /// When I add a new split with the following details:
    /// </summary>
    /// <remarks>
    /// Expects that we are on the transaction detail page for the transaction we're adding to
    /// </remarks>
    [When("I add a new split with the following details:")]
    public async Task IAddANewSplitWithTheFollowingDetails(DataTable table)
    {
        Assert.That(table.Rows, Has.Count.EqualTo(1), "Adding a single split requires exactly one row.");
        var row = table.Rows.Single();
        var amount = row.GetRequired<decimal>("Amount");
        var category = row.GetOptional("Category") ?? string.Empty;
        var memo = row.GetOptional("Memo") ?? string.Empty;

        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.AddSplitAsync(amount, category, memo);
    }

    /// <summary>
    /// When I edit the {category} split amount to {amount}
    /// </summary>
    /// <param name="category">The category of the split to edit.</param>
    /// <param name="amount">The new amount for the split as a stringified decimal.</param>
    [When("I edit the {category} split amount to {amount}")]
    public async Task IEditTheSplitAmountTo(string category, string amount)
    {
        Assert.That(decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount),
            Is.True, $"Could not parse split amount '{amount}'.");

        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.EditSplitAmountAsync(category, parsedAmount);
    }

    /// <summary>
    /// When I delete the {category} split
    /// </summary>
    [When("I delete the {category} split")]
    public async Task IDeleteSplit(string category)
    {
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.DeleteSplitAsync(category);
    }

    /// <summary>
    /// Then the splits list is empty
    /// </summary>
    [Then("the splits list is empty")]
    public async Task TheSplitsListIsEmpty()
    {
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.ExpectSplitsListToBeEmptyAsync();
    }

    /// <summary>
    /// When I upload a split workbook with these first-worksheet rows:
    /// </summary>
    /// <param name="table">The table containing the split rows to upload. Contains columns "Category", "Amount", and optionally "Memo".</param>
    [When("I upload a split workbook with these first-worksheet rows:")]
    public async Task IUploadASplitWorkbookWithTheseFirstWorksheetRows(DataTable table)
    {
        var workbook = SpreadSheetGenerator.GenerateWorkbook(table, "Splits");
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.UploadSplitWorkbookAsync("splits.xlsx", workbook);
    }

    /// <summary>
    /// When I download the transaction split upload template
    /// </summary>
    [When("I download the transaction split upload template")]
    [Provides(ObjectStoreKeys.DownloadDirectory)]
    [Provides(ObjectStoreKeys.WorkbookFilePath)]
    public async Task IDownloadTheTransactionSplitUploadTemplate()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _context.ObjectStore.Add(ObjectStoreKeys.DownloadDirectory, directory);

        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        var filePath = await detailsPage.DownloadSplitTemplateAsync(directory);
        _context.ObjectStore.Add(ObjectStoreKeys.WorkbookFilePath, filePath);
    }

    /// <summary>
    /// Then an XLSX file is downloaded
    /// </summary>
    [Then("an XLSX file is downloaded")]
    [Requires(ObjectStoreKeys.WorkbookFilePath)]
    public async Task AnXLSXFileIsDownloaded()
    {
        var filePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.WorkbookFilePath);
        Assert.That(File.Exists(filePath), Is.True, "Expected XLSX file to be downloaded.");
    }

    /// <summary>
    /// Then the first worksheet contains only the headers {string1}, {string2}, and {string3}
    /// </summary>
    [Then("the first worksheet contains only the headers {string1}, {string2}, and {string3}")]
    [Requires(ObjectStoreKeys.WorkbookFilePath)]
    public async Task TheFirstWorksheetContainsOnlyTheHeadersAnd(string string1, string string2, string string3)
    {
        var filePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.WorkbookFilePath);
        using var workbook = new XLWorkbook(filePath);

        // Check the headers of the first worksheet
        var worksheet = workbook.Worksheet(1);
        Assert.That(worksheet.Cell(1, 1).Value.ToString(), Is.EqualTo(string1), "Header 1 differs.");
        Assert.That(worksheet.Cell(1, 2).Value.ToString(), Is.EqualTo(string2), "Header 2 differs.");
        Assert.That(worksheet.Cell(1, 3).Value.ToString(), Is.EqualTo(string3), "Header 3 differs.");

        // Also ensure that it has no extra rows beyond the headers
        Assert.That(worksheet.LastRowUsed()!.RowNumber(), Is.EqualTo(1), "Expected only header row to be present.");
    }

    /// <summary>
    /// Checks the success indicator without depending on its wording.
    /// </summary>
    [Then("the splits should be uploaded successfully")]
    public Task TheSplitsShouldBeUploadedSuccessfully()
        => _context.GetOrCreatePage<TransactionDetailsPage>().ExpectUploadSuccessMessageAsync();

    /// <summary>
    /// Then the following splits are displayed:
    /// </summary>
    [Then("the following splits are displayed:")]
    public async Task TheFollowingSplitsAreDisplayed(DataTable table)
    {
        var expectedSplits = table.Rows.Select(row => new
        {
            Category = row.GetOptional("Category") ?? string.Empty,
            Amount = row.GetRequired<decimal>("Amount"),
            Memo = row.GetOptional("Memo") ?? string.Empty
        }).ToArray();

        // First ensure the expected number of splits is displayed
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.ExpectSplitCountAsync(expectedSplits.Length);

        // Now loop through each one and check versus the expected splits
        var actualSplits = await detailsPage.GetDisplayedSplitsAsync();
        for (var index = 0; index < expectedSplits.Length; index++)
        {
            var expected = expectedSplits[index];
            var actual = actualSplits[index];
            Assert.That(actual.Category, Is.EqualTo(string.IsNullOrWhiteSpace(expected.Category) ? "(none)" : expected.Category),
                $"Split {index} category differs.");
            Assert.That(ParseDisplayedAmount(actual.Amount), Is.EqualTo(expected.Amount),
                $"Split {index} amount differs.");
            Assert.That(actual.Memo, Is.EqualTo(expected.Memo), $"Split {index} memo differs.");
        }
    }

    /// <summary>
    /// Then the transaction is balanced
    /// </summary>
    /// <remarks>
    /// Checks that the displayed split amounts sum to the transaction amount.
    /// </remarks>
    [Then("the transaction is balanced")]
    [Then("the splits total matches the transaction amount")]
    public async Task TheTransactionAmountsMatch()
    {
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        var transactionAmountText = await detailsPage.GetAmountAsync();
        Assert.That(transactionAmountText, Is.Not.Null.And.Not.Empty, "Transaction amount is not displayed.");

        var transactionAmount = ParseDisplayedAmount(transactionAmountText!);
        var splits = await detailsPage.GetDisplayedSplitsAsync();
        var splitTotal = splits.Sum(split => ParseDisplayedAmount(split.Amount));
        Assert.That(splitTotal, Is.EqualTo(transactionAmount),
            $"Displayed splits total {splitTotal} but transaction amount is {transactionAmount}.");
    }

    private static decimal ParseDisplayedAmount(string amount)
    {
        Assert.That(decimal.TryParse(amount, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"), out var parsed),
            Is.True, $"Could not parse displayed currency amount '{amount}'.");
        return parsed;
    }

    /// <summary>
    /// Then the split balance warning is visible
    /// </summary>
    [Then("the split balance warning is visible")]
    public async Task TheSplitBalanceWarningIsVisible()
    {
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.ExpectTransactionSplitBalanceAlertAsync(true);
    }

    /// <summary>
    /// Then no split balance warning is shown
    /// </summary>
    [Then("no split balance warning is shown")]
    public async Task NoSplitBalanceWarningIsShown()
    {
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.ExpectTransactionSplitBalanceAlertAsync(false);
    }

    /// <summary>
    /// Then the split balance warning shows transaction amount {string1} and split total {string2}
    /// </summary>
    /// <param name="transactionAmount">The expected transaction amount as displayed (decimal)</param>
    /// <param name="splitTotal">The expected split total as displayed (decimal)</param>
    [Then("the split balance warning shows transaction amount {transactionAmount} and split total {splitTotal}")]
    public async Task TheSplitBalanceWarningShowsTransactionAmountAndSplitTotal(string transactionAmount, string splitTotal)
    {
        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();

        var transactionAmountText = await detailsPage.GetTransactionSplitBalanceAlert_TransactionAmountAsync();
        Assert.That(transactionAmountText, Is.Not.Null.And.Not.Empty, "Transaction amount is not displayed.");

        var transactionAmountDisplayed = ParseDisplayedAmount(transactionAmountText!);
        Assert.That(transactionAmountDisplayed, Is.EqualTo(ParseDisplayedAmount(transactionAmount)), "Transaction amount does not match expected.");

        var splitTotalText = await detailsPage.GetTransactionSplitBalanceAlert_SplitTotalAsync();
        Assert.That(splitTotalText, Is.Not.Null.And.Not.Empty, "Split total is not displayed.");

        var splitTotalDisplayed = ParseDisplayedAmount(splitTotalText!);
        Assert.That(splitTotalDisplayed, Is.EqualTo(ParseDisplayedAmount(splitTotal)), "Split total does not match expected.");
    }
}
