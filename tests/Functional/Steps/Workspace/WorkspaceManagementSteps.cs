using ClosedXML.Excel;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Infrastructure.TestControl;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Steps.Transaction;

namespace YoFi.V3.Tests.Functional.Steps.Workspace;

/// <summary>
/// Step definitions for workspace CRUD operations (Create, Rename, Update, Delete).
/// </summary>
/// <remarks>
/// Handles workspace management operations:
/// - Creating new workspaces
/// - Viewing workspace lists and details
/// - Renaming workspaces
/// - Updating workspace descriptions
/// - Deleting workspaces
///
/// All operations work with user-readable workspace names (without __TEST__ prefix).
/// The prefix is added automatically when needed via AddTestPrefix().
/// </remarks>
public class WorkspaceManagementSteps(ITestContext context) : WorkspaceStepsBase(context)
{

    TransactionDataSteps transactionDataSteps = new(context);
    PayeeRuleSteps payeeRuleSteps = new(context);
    TransactionListSteps transactionListSteps = new(context);

    #region Steps: GIVEN

    [Given("My workspace has existing data spanning multiple years")]
    [Requires(ObjectStoreKeys.LoggedInAs)]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    [Provides(ObjectStoreKeys.ExistingTransactions)]
    [Provides(ObjectStoreKeys.ExpectedPayeeRules)]
    public async Task GivenMyWorkspaceHasExistingDataSpanningMultipleYears()
    {
        var transactions = new DataTable(
            ["Payee", "Amount", "Category", "Memo", "Date"],
            [
                ["Export Market 2023", "25.50", "Groceries", "2023 export fixture", "2023-06-15"],
                ["Export Store 2024", "75.25", "Shopping", "2024 export fixture", "2024-07-20"]
            ]);
        await transactionDataSteps.GivenIHaveAWorkspaceWithATransaction(transactions);

        var rules = new DataTable(
            ["Payee", "Category", "IsRegex"],
            [
                ["Export Market", "Groceries", "false"],
                ["Export Store", "Shopping", "false"]
            ]);
        await payeeRuleSteps.IHaveTheFollowingPayeeRules(rules);
    }

    #endregion

    #region Steps: WHEN

    [When("I export my entire workspace data")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    [Provides(ObjectStoreKeys.DownloadDirectory)]
    [Provides(ObjectStoreKeys.ExportWorkbookPath)]
    public async Task WhenIExportMyEntireWorkspaceData()
    {
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.NavigateAsync(workspaceKey);

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _context.ObjectStore.Add(ObjectStoreKeys.DownloadDirectory, directory);

        var filePath = await managementPage.DownloadExportAsync(directory, allData: true);
        _context.ObjectStore.Add(ObjectStoreKeys.ExportWorkbookPath, filePath);
    }

    [When("I export transactions for one year only")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    [Provides(ObjectStoreKeys.DownloadDirectory)]
    [Provides(ObjectStoreKeys.ExportWorkbookPath)]
    public async Task WhenIExportTransactionsForOneYearOnly()
    {
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.NavigateAsync(workspaceKey);

        await managementPage.ExportTypeCheckbox("PayeeMatchingRules").UncheckAsync();
        await managementPage.ExportTypeCheckbox("Transactions").CheckAsync();
        await managementPage.ExportDateSelection.SelectOptionAsync("year:2024");

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _context.ObjectStore.Add(ObjectStoreKeys.DownloadDirectory, directory);

        var filePath = await managementPage.DownloadExportAsync(directory, allData: false);
        _context.ObjectStore.Add(ObjectStoreKeys.ExportWorkbookPath, filePath);
    }

    /// <summary>
    /// Creates a new workspace with specified name and optional description.
    /// </summary>
    /// <param name="name">The workspace name (without __TEST__ prefix).</param>
    /// <param name="description">The workspace description (optional, defaults to generated description).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to name. Navigates to workspaces page, creates workspace,
    /// waits for it to appear in the list, and stores it as current workspace.
    /// If description is null, generates a default description.
    /// </remarks>
    [When("I create a new workspace called {name} for {description}")]
    [When("I create a workspace called {name}")]
    [Provides(ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenICreateANewWorkspaceCalled(string name, string? description = null)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var workspaceName = AddTestPrefix(name);
        var finalDescription = description ?? $"__TEST__ Test workspace: {name}";
        await workspacesPage.CreateWorkspaceAsync(workspaceName, finalDescription);

        // Wait for the new workspace card to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await workspacesPage.WaitForWorkspaceAsync(workspaceName);

        // Store the workspace name for future reference
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, workspaceName);
    }

    /// <summary>
    /// Navigates to the workspaces list page.
    /// </summary>
    /// <remarks>
    /// Simply navigates to the workspaces page to view available workspaces.
    /// </remarks>
    [When("I view my workspace list")]
    public async Task WhenIViewMyWorkspaceList()
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();
    }

    /// <summary>
    /// Open the management page for a specific workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Opens Manage from the workspace card and stores it as current workspace.
    /// </remarks>
    [Given("I am managing the details of {workspaceName}")]
    [When("I view the details of {workspaceName}")]
    [When("I manage the details of {workspaceName}")]
    [When("I open the manage page for {workspaceName}")]
    [Provides(ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenIViewTheDetailsOf(string workspaceName)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);
        await workspacesPage.OpenManagementAsync(fullWorkspaceName);

        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);
    }

    [When("I refresh the workspace management page")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenIRefreshTheWorkspaceManagementPage()
    {
        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        await managementPage.ReloadAsync();
        await managementPage.WaitForWorkspaceAsync(GetCurrentOrNewWorkspaceName());
    }

    [When("I cancel deleting {workspaceName}")]
    [Provides(ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenICancelDeleting(string workspaceName)
    {
        await WhenIViewTheDetailsOf(workspaceName);
        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        await managementPage.OpenDeleteModalAsync();
        await Microsoft.Playwright.Assertions.Expect(managementPage.DeletingWorkspaceName)
            .ToHaveTextAsync(AddTestPrefix(workspaceName));
        await managementPage.CancelDeleteAsync();
    }

    /// <summary>
    /// Renames the current workspace.
    /// </summary>
    /// <param name="newName">The new workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to new name. Gets current workspace from object store,
    /// performs rename via the management page, waits for updated name to appear, and
    /// updates current workspace context.
    /// </remarks>
    [When("I rename it to {newName}")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    [Provides(ObjectStoreKeys.NewWorkspaceName)]
    public async Task WhenIRenameItTo(string newName)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        // Get the current workspace name from object store
        var oldName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        // Store the new name for assertions
        var fullNewName = AddTestPrefix(newName);
        _context.ObjectStore.Add(ObjectStoreKeys.NewWorkspaceName, fullNewName);

        await workspacesPage.OpenManagementAsync(oldName);
        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        await managementPage.UpdateWorkspaceAsync(fullNewName);

        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullNewName);
    }

    /// <summary>
    /// Updates the description of the current workspace.
    /// </summary>
    /// <param name="newDescription">The new workspace description.</param>
    /// <remarks>
    /// Gets current or newly renamed workspace name from object store and updates
    /// its description while keeping the same name. Waits for update to complete.
    /// </remarks>
    [When("I update the description to {newDescription}")]
    public async Task WhenIUpdateTheDescriptionTo(string newDescription)
    {
        var workspaceName = GetCurrentOrNewWorkspaceName();
        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        await managementPage.UpdateWorkspaceAsync(workspaceName, newDescription);
    }

    /// <summary>
    /// Deletes a workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix, opens Manage from the list, and deletes the workspace.
    /// Used for positive test cases where deletion is expected to succeed.
    /// Also removes workspace from tracking to prevent cleanup attempts.
    /// </remarks>
    [When("I delete {workspaceName}")]
    public async Task WhenIDelete(string workspaceName)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);
        await workspacesPage.OpenManagementAsync(fullWorkspaceName);
        await _context.GetOrCreatePage<Pages.WorkspaceManagementPage>().DeleteWorkspaceAsync();

        // Also remove this from workspace tracking, so we don't try to clean it up later
        _context.UntrackWorkspace(fullWorkspaceName);
    }

    #endregion

    #region Steps: THEN

    [Then("I should receive a file containing my workspace data")]
    [Requires(ObjectStoreKeys.ExportWorkbookPath)]
    public async Task ThenIShouldReceiveAFileContainingMyWorkspaceData()
    {
        var filePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.ExportWorkbookPath);
        Assert.That(filePath, Is.Not.Null.And.Not.Empty);
        Assert.That(File.Exists(filePath), Is.True, $"Export workbook was not created at '{filePath}'");
        using var workbook = new XLWorkbook(filePath);
        Assert.That(workbook.Worksheets, Is.Not.Empty, "Export workbook should contain worksheets.");
    }

    [Then("the exported file should include all transactions from the workspace")]
    [Requires(ObjectStoreKeys.ExportWorkbookPath)]
    public async Task ThenTheExportedFileShouldIncludeAllTransactionsFromTheWorkspace()
    {
        var filePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.ExportWorkbookPath);
        using var workbook = new XLWorkbook(filePath);
        var strings = workbook.Worksheet("Transactions").CellsUsed().Select(cell => cell.GetString()).ToArray();

        Assert.That(strings, Does.Contain("Export Market 2023"));
        Assert.That(strings, Does.Contain("Export Store 2024"));
    }

    [Then("the exported file should include all payee matching rules from the workspace")]
    [Requires(ObjectStoreKeys.ExportWorkbookPath)]
    [Requires(ObjectStoreKeys.ExpectedPayeeRules)]
    public async Task ThenTheExportedFileShouldIncludeAllPayeeMatchingRulesFromTheWorkspace()
    {
        var filePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.ExportWorkbookPath);
        var expected = _context.ObjectStore.Get<ICollection<PayeeMatchingRuleResultDto>>(ObjectStoreKeys.ExpectedPayeeRules);
        using var workbook = new XLWorkbook(filePath);
        var rules = workbook.Worksheet("PayeeMatchingRules");

        foreach (var rule in expected)
        {
            Assert.That(rules.CellsUsed().Select(cell => cell.GetString()).Contains(rule.PayeePattern),
                Is.True, $"Export should contain payee rule '{rule.PayeePattern}'.");
        }
    }

    [Then("I should receive a file containing only that year's transactions")]
    [Requires(ObjectStoreKeys.ExportWorkbookPath)]
    public async Task ThenIShouldReceiveAFileContainingOnlyThatYearsTransactions()
    {
        var filePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.ExportWorkbookPath);
        using var workbook = new XLWorkbook(filePath);
        var transactions = workbook.Worksheet("Transactions");
        var strings = transactions.CellsUsed().Select(cell => cell.GetString()).ToArray();

        Assert.That(strings, Does.Contain("Export Store 2024"));
        Assert.That(strings, Does.Not.Contains("Export Market 2023"));
    }

    [Then("the exported file should not include transactions from previous years")]
    [Requires(ObjectStoreKeys.ExportWorkbookPath)]
    public async Task ThenTheExportedFileShouldNotIncludeTransactionsFromPreviousYears()
    {
        var filePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.ExportWorkbookPath);
        using var workbook = new XLWorkbook(filePath);
        var strings = workbook.Worksheet("Transactions").CellsUsed().Select(cell => cell.GetString()).ToArray();
        Assert.That(strings, Does.Not.Contains("Export Market 2023"));
    }

    [Then("the exported file should not include any payee matching rules")]
    [Requires(ObjectStoreKeys.ExportWorkbookPath)]
    public async Task ThenTheExportedFileShouldNotIncludeAnyPayeeMatchingRules()
    {
        var filePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.ExportWorkbookPath);
        using var workbook = new XLWorkbook(filePath);
        Assert.That(workbook.Worksheets.Any(sheet => sheet.Name == "PayeeMatchingRules"), Is.False,
            "A Transactions-only export should not include a PayeeMatchingRules worksheet.");
    }

    #endregion

    #region Sample data

    /// <summary>
    /// When I load the {scenario} sample data
    /// </summary>
    /// <param name="scenario">The sample data scenario to load.</param>
    [When("I load the {scenario} sample data")]
    [Given("I have loaded the {scenario} sample data")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ILoadTheSampleData(string scenario)
    {
        // Get the current workspace name from object store
        var currentName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();
        await workspacesPage.OpenManagementAsync(currentName);
        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        await managementPage.LoadSampleDataAsync(scenario);
    }

    /// <summary>
    /// When I download sample OFX data with the default data range
    /// </summary>
    /// <remarks>
    /// Note that CleanupTestResourcesAsync will attempt to delete the downloaded OFX file and the download directory.
    /// </remarks>
    [When("I download sample OFX data with the default data range")]
    [Provides(ObjectStoreKeys.OfxFilePath)]
    [Provides(ObjectStoreKeys.DownloadDirectory)]
    public async Task IDownloadSampleOFXDataWithTheDefaultDataRange()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _context.ObjectStore.Add(ObjectStoreKeys.DownloadDirectory, directory);

        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        var filePath = await managementPage.DownloadSampleOFXAsync(directory);
        _context.ObjectStore.Add(ObjectStoreKeys.OfxFilePath, filePath);
    }



    /// <summary>
    /// Then I see confirmation of the transactions and rules added
    /// </summary>
    [Then("I see confirmation of the transactions and rules added")]
    public async Task ISeeConfirmationOfTheTransactionsAndRulesAdded()
    {
        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        await managementPage.ExpectSampleLoadResultAsync();
    }

    /// <summary>
    /// Then my existing transactions and rules remain unchanged
    /// </summary>
    /// <remarks>
    /// This is a meta-step, which will:
    /// * Check that the "expected" transactions and rules are still present in the workspace, which were set by MyWorkspaceHasExistingTransactionsAndPayeeMatchingRules
    /// </remarks>
    [Then("my existing transactions and rules remain unchanged")]
    [Requires(ObjectStoreKeys.ExistingTransactionKeys)]
    [Requires(ObjectStoreKeys.ExpectedPayeeRules)]
    public async Task MyExistingTransactionsAndRulesRemainUnchanged()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        await transactionDataSteps.IShouldSeeTheOriginalTransactions();

        var payeeRulesPage = _context.GetOrCreatePage<PayeeRulesPage>();
        await payeeRulesPage.NavigateAsync();

        await payeeRuleSteps.IShouldSeeTheExpectedRulesIncluded();
    }

    /// <summary>
    /// Then the sample transactions and rules are available alongside them
    /// </summary>
    /// <remarks>
    /// This is a meta-step, which will:
    /// *   And I can see many pages of transactions (ICanSeeManyPagesOfTransactions)
    /// *   And I can see many payee matching rules
    ///
    /// Note that the MyWorkspaceHasExistingTransactionsAndPayeeMatchingRules needs to include LESS than the number of rules that
    /// "And I can see many payee matching rules" checks for
    /// </remarks>
    [Then("the sample transactions and rules are available alongside them")]
    public async Task TheSampleTransactionsAndRulesAreAvailableAlongsideThem()
    {
        // Note that these steps internally do their own navigation, so we don't need to do that here.
        await transactionListSteps.ICanSeeManyPagesOfTransactions();
        await payeeRuleSteps.ICanSeeManyPayeeMatchingRules();
    }

    #endregion

    #region Delete all data

    /// <summary>
    /// When I delete my workspace data
    /// </summary>
    [When("I delete my workspace data")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IDeleteMyWorkspaceData()
    {
        // Get the current workspace name from object store
        var currentName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();
        await workspacesPage.OpenManagementAsync(currentName);
        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        await managementPage.DeleteAllDataAsync(currentName);
    }

    /// <summary>
    /// Then I see confirmation of the data deleted
    /// </summary>
    [Then("I see confirmation of the data deleted")]
    public async Task ISeeConfirmationOfTheDataDeleted()
    {
        var managementPage = _context.GetOrCreatePage<Pages.WorkspaceManagementPage>();
        await managementPage.ExpectDeleteDataResultAsync();
    }

    #endregion

    #region Import data

    /// <summary>
    /// When I upload a file containing original YoFi Payees
    /// </summary>
    [When("I upload a file containing original YoFi Payees")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IUploadAFileContainingOriginalYoFiPayees()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Payee");
        worksheet.Cell(1, 1).Value = "ID";
        worksheet.Cell(1, 2).Value = "Name";
        worksheet.Cell(1, 3).Value = "Category";
        worksheet.Cell(1, 4).Value = "Selected";
        for (var index = 1; index <= 12; index++)
        {
            worksheet.Cell(index + 1, 1).Value = index;
            worksheet.Cell(index + 1, 2).Value = $"Original YoFi Payee {index}";
            worksheet.Cell(index + 1, 3).Value = "Shopping";
            worksheet.Cell(index + 1, 4).Value = false;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var currentName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();
        await workspacesPage.OpenManagementAsync(currentName);
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.UploadImportWorkbookAsync("original-yofi-payees.xlsx", stream.ToArray());
    }

    /// <summary>
    /// When I upload a file containing {count} rules in native YoFi format
    /// </summary>
    [When("I upload a file containing {count} rules in native YoFi format")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IUploadAFileContainingRulesInNativeYoFiFormat(int count)
    {
        using var workbook = new XLWorkbook();
        var metadata = workbook.Worksheets.Add("Metadata");
        metadata.Cell(1, 1).Value = "Name";
        metadata.Cell(1, 2).Value = "Value";
        metadata.Cell(2, 1).Value = "Format";
        metadata.Cell(2, 2).Value = "YoFi.V3";

        var worksheet = workbook.Worksheets.Add("PayeeMatchingRules");
        worksheet.Cell(1, 1).Value = "Key";
        worksheet.Cell(1, 2).Value = "PayeePattern";
        worksheet.Cell(1, 3).Value = "PayeeIsRegex";
        worksheet.Cell(1, 4).Value = "Category";
        for (var index = 1; index <= count; index++)
        {
            worksheet.Cell(index + 1, 2).Value = $"Native Payee {index}";
            worksheet.Cell(index + 1, 3).Value = false;
            worksheet.Cell(index + 1, 4).Value = "Shopping";
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var currentName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();
        await workspacesPage.OpenManagementAsync(currentName);
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.UploadImportWorkbookAsync("native-yofi-rules.xlsx", stream.ToArray());
        await managementPage.PreviewImportAsync();
        await managementPage.ConfirmImportAsync();
    }

    /// <summary>
    /// When I upload a file containing {count1} transactions and {count2} split transaction in native YoFi format
    /// </summary>
    [When("I upload a file containing {count1} transactions and {count2} split transaction in native YoFi format")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IUploadAFileContainingTransactionsAndSplitTransactionInNativeYoFiFormat(int count1, int count2)
    {
        using var workbook = new XLWorkbook();
        var metadata = workbook.Worksheets.Add("Metadata");
        metadata.Cell(1, 1).Value = "Name";
        metadata.Cell(1, 2).Value = "Value";
        metadata.Cell(2, 1).Value = "Format";
        metadata.Cell(2, 2).Value = "YoFi.V3";

        var transactions = workbook.Worksheets.Add("Transactions");
        transactions.Cell(1, 1).Value = "Key";
        transactions.Cell(1, 2).Value = "Date";
        transactions.Cell(1, 3).Value = "Payee";
        transactions.Cell(1, 4).Value = "Amount";
        transactions.Cell(1, 5).Value = "Source";
        transactions.Cell(1, 6).Value = "ExternalId";
        transactions.Cell(1, 7).Value = "Memo";
        transactions.Cell(1, 8).Value = "Category";

        var splitTransactionKey = Guid.NewGuid();
        for (var index = 1; index <= count1; index++)
        {
            var key = index == count1 ? splitTransactionKey : Guid.NewGuid();
            transactions.Cell(index + 1, 1).Value = key.ToString("D");
            transactions.Cell(index + 1, 2).Value = new DateTime(2026, 1, index);
            transactions.Cell(index + 1, 3).Value = $"Native Transaction {index}";
            transactions.Cell(index + 1, 4).Value = index == count1 ? 100m : -25m * index;
            transactions.Cell(index + 1, 8).Value = index == count1 ? "SPLIT" : "Shopping";
        }

        var splits = workbook.Worksheets.Add("Splits");
        splits.Cell(1, 1).Value = "Key";
        splits.Cell(1, 2).Value = "TransactionKey";
        splits.Cell(1, 3).Value = "Amount";
        splits.Cell(1, 4).Value = "Category";
        splits.Cell(1, 5).Value = "Memo";
        splits.Cell(1, 6).Value = "Order";
        var splitRowCount = count2 > 0 ? count2 + 1 : 0;
        for (var index = 0; index < splitRowCount; index++)
        {
            var row = index + 2;
            splits.Cell(row, 2).Value = splitTransactionKey.ToString("D");
            splits.Cell(row, 3).Value = 100m / splitRowCount;
            splits.Cell(row, 4).Value = "Shopping";
            splits.Cell(row, 5).Value = $"Split {index + 1}";
            splits.Cell(row, 6).Value = index;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var currentName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();
        await workspacesPage.OpenManagementAsync(currentName);
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.UploadImportWorkbookAsync("native-yofi-transactions.xlsx", stream.ToArray());
        await managementPage.PreviewImportAsync();
        await managementPage.ConfirmImportAsync();
    }

    /// <summary>
    /// When I preview and confirm the upload
    /// </summary>
    [When("I preview and confirm the upload")]
    public async Task IPreviewAndConfirmTheUpload()
    {
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.PreviewAndConfirmImportAsync();
    }

    /// <summary>
    /// Then I should see the detected format {string1}
    /// </summary>
    /// <param name="string1">The detected format string (currently expected to be "YoFi.V3")</param>
    [Then("I should see the detected format {string1}")]
    public async Task IShouldSeeTheDetectedFormat(string string1)
    {
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.ExpectDetectedFormatAsync(string1);
    }

    /// <summary>
    /// Then I should see the {dataType} worksheet preview
    /// </summary>
    /// <param name="dataType">The type of worksheet to preview (Payees, Transactions, or Splits)</param>
    [Then("I should see the {dataType} worksheet preview")]
    public async Task IShouldSeeTheDataTypeWorksheetPreview(string dataType)
    {
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.ImportPreview.WaitForAsync();
        await managementPage.ExpectWorksheetPreviewAsync(dataType);
    }

    #endregion
}
