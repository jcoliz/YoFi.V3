using Gherkin.Generator.Utils;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for bank import operations in composition architecture.
/// </summary>
/// <param name="_context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Provides bank import-related operations for functional tests using composition pattern.
/// Methods handle OFX file upload, import review, and transaction selection verification.
/// </remarks>
public class BankImportSteps(ITestContext _context)
{
    #region Steps: GIVEN

    /// <summary>
    /// Navigates to the import review page with the correct workspace selected.
    /// </summary>
    /// <remarks>
    /// Sets up minimal state to be on the import review page: navigates to the page and
    /// selects the current workspace. Does not upload any files.
    ///
    /// Requires Objects
    /// - CurrentWorkspace
    /// </remarks>
    [Given("I am on the import review page")]
    [When("I am on the Import Review page")]
    [When("I navigate to the Import page")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IAmOnTheImportReviewPage()
    {
        // Given: Get workspace name
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");

        // And: Navigate to import page
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the workspace
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
    }

    /// <summary>
    /// Sets up import review state with specified number of transactions ready for import.
    /// </summary>
    /// <param name="count">Number of transactions to seed in import review queue.</param>
    /// <param name="selectedCount">Number of transactions to mark as selected.</param>
    /// <remarks>
    /// Seeds import review transactions via Test Control API (SeedImportReviewTransactions endpoint).
    /// Uses deterministic test data generation to create predictable transaction data.
    /// This approach is faster and more reliable than uploading OFX files through the UI.
    ///
    /// Requires Objects
    /// - LoggedInAs (username from auth)
    /// - CurrentWorkspace (workspace name)
    /// </remarks>
    [Given("There are {count} transactions ready for import review, with {selectedCount} selected")]
    [RequiresObjects(ObjectStoreKeys.LoggedInAs, ObjectStoreKeys.CurrentWorkspace)]
    public async Task ThereAreTransactionsReadyForImportReview(int count, int selectedCount)
    {
        // Given: Get logged in username
        var username = _context.ObjectStore.Get<string>(ObjectStoreKeys.LoggedInAs)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.LoggedInAs} not found in object store");

        // And: Get current workspace name and resolve workspace key
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");
        var workspaceKey = _context.GetWorkspaceKey(workspaceName);

        // When: Seed import review transactions via Test Control API
        await _context.TestControlClient.SeedImportReviewTransactionsAsync(
            username,
            workspaceKey,
            count,
            selectedCount);
    }

    /// <summary>
    /// Creates an OFX file with the specified number of new transactions and uploads it through the UI.
    /// </summary>
    /// <param name="count">Number of new transactions to include in the OFX file.</param>
    /// <remarks>
    /// Generates a temporary OFX file with unique transaction IDs to ensure all transactions are new.
    /// Navigates to the import page, uploads the file, and waits for processing to complete.
    /// The temporary file is cleaned up after upload.
    ///
    /// Requires Objects
    /// - CurrentWorkspace (workspace name)
    /// </remarks>
    [Given("I have uploaded an OFX file with {count} new transactions")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    [ProvidesObjects(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task IHaveUploadedAnOFXFileWithNewTransactions(int count)
    {
        // Given: Generate OFX file with specified number of transactions
        var ofxFilePath = OfxFileGenerator.GenerateOfxFile(count);

        // And: Get workspace name
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");

        // And: Navigate to import page
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the workspace
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);

        // When: Upload the generated OFX file
        await importPage.UploadFileAsync(ofxFilePath);

        // And: Wait for upload to complete
        await importPage.WaitForUploadCompleteAsync();

        // And: Ensure the import button is enabled, indicating transactions are loaded
        await importPage.WaitForEnabled(importPage.ImportButton);

        // And: Gather the payee names now displayed
        var payees = await importPage.GetAllTransactionPayeesAsync();

        // Store them in the object store for later verification
        _context.ObjectStore.Add(ObjectStoreKeys.UploadedTransactionPayees, payees);

        // NOTE: OfxfilePath cleanup is handled by test cleanup
    }

    /// <summary>
    /// Generates a valid OFX file with the specified number of transactions.
    /// </summary>
    /// <param name="count">Number of transactions to include in the OFX file.</param>
    /// <remarks>
    /// Creates a temporary OFX file with unique transaction IDs to ensure all transactions are new.
    /// Stores the file path in the object store for later upload via IUploadTheOFXFile step.
    /// The file will be cleaned up after upload.
    ///
    /// Provides Objects
    /// - OfxFilePath (string) - Path to the generated OFX file
    /// </remarks>
    [Given("I have a valid OFX file with {count} transactions")]
    [ProvidesObjects(ObjectStoreKeys.OfxFilePath)]
    public async Task IHaveAValidOFXFileWith10Transactions(int count)
    {
        // Given: Generate OFX file with specified number of transactions
        var ofxFilePath = OfxFileGenerator.GenerateOfxFile(count);

        // And: Store the file path in object store for later use
        _context.ObjectStore.Add(ObjectStoreKeys.OfxFilePath, ofxFilePath);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Given I have uploaded an OFX file containing all the same transactions
    /// </summary>
    [Given("I have uploaded an OFX file containing all the same transactions")]
    [RequiresObjects(ObjectStoreKeys.ExistingTransactions)]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    [ProvidesObjects(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task IHaveUploadedAnOFXFileContainingAllTheSameTransactions()
    {
        // Given: Retrieve existing transactions from object store
        var existingTransactions = _context.ObjectStore.Get<List<TransactionResultDto>>(ObjectStoreKeys.ExistingTransactions)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.ExistingTransactions} not found in object store. Did you call 'I have N existing transactions in my workspace' first?");

        // And: Generate OFX file content with the same transactions
        // TODO: This won't work, because the FITIDs will be different.
        var ofxFilePath = OfxFileGenerator.GenerateOfxFileFromTransactions(existingTransactions);

        // And: Store the file path in object store for later upload
        _context.ObjectStore.Add(ObjectStoreKeys.OfxFilePath, ofxFilePath);

        // When: Upload the OFX file
        await IUploadTheOFXFile();
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// When I attempt to navigate to the Import page
    /// </summary>
    /// <remarks>
    /// This is a special navigation via the nav bar, which will ensure that the page
    /// is always fully ready, which it may not be via direct URL access. It also
    /// doesn't assume the user will have access to this page!
    /// </remarks>
    [When("I attempt to navigate to the Import page")]
    public async Task IAttemptToNavigateToTheImportPage()
    {
        // When: Navigate via the nav bar
        var basePage = _context.GetOrCreatePage<BasePage>();
        await basePage.SiteHeader.Nav.SelectOptionAsync("Import");
    }

    /// <summary>
    /// Uploads an OFX file from the test sample data directory.
    /// </summary>
    /// <param name="filename">The filename (e.g., "checking-jan-2024.ofx")</param>
    [When("I upload OFX file {filename}")]
    public async Task WhenIUploadOFXFile(string filename)
    {
        // When: Upload the file
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.UploadFileAsync(filename);

        // And: Wait for upload to complete
        await importPage.WaitForUploadCompleteAsync();

        // And: Ensure the import button is enabled, indicating transactions are loaded
        await importPage.WaitForEnabled(importPage.ImportButton);
    }

    /// <summary>
    /// Uploads the previously generated OFX file through the UI.
    /// </summary>
    /// <remarks>
    /// Retrieves the OFX file path from the object store (set by IHaveAValidOFXFileWith10Transactions),
    /// navigates to the import page, uploads the file, and waits for processing to complete.
    /// The temporary file is cleaned up after upload.
    ///
    /// Requires Objects
    /// - CurrentWorkspace (workspace name)
    /// - OfxFilePath (string) - Path to the OFX file to upload
    /// </remarks>
    [Given("I have uploaded the OFX file")]
    [When("I upload the OFX file")]
    [When("I upload the same OFX file again")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.OfxFilePath)]
    [ProvidesObjects(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task IUploadTheOFXFile()
    {
        // Given: Retrieve the OFX file path from object store
        var ofxFilePath = _context.ObjectStore.Get<string>(ObjectStoreKeys.OfxFilePath)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.OfxFilePath} not found in object store. Did you call 'I have a valid OFX file with N transactions' first?");

        // And: Get workspace name
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");

        // And: Navigate to import page
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the workspace
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);

        // When: Upload the generated OFX file
        await importPage.UploadFileAsync(ofxFilePath);

        // And: Wait for upload to complete
        await importPage.WaitForUploadCompleteAsync();

        // And: Ensure the import button is enabled, indicating transactions are loaded
        // BUG: You can't count on this, if all the items are deselected!!
        await importPage.WaitForEnabled(importPage.DeleteAllButton);

        // And: Gather the payee names now displayed
        var payees = await importPage.GetAllTransactionPayeesAsync();

        // Store them in the object store for later verification
        _context.ObjectStore.Add(ObjectStoreKeys.UploadedTransactionPayees, payees);

        // NOTE: OfxfilePath cleanup is handled by test cleanup
    }

    /// <summary>
    /// Clicks the Import button, confirms the import, and waits for completion.
    /// </summary>
    /// <remarks>
    /// Opens the import confirmation modal, clicks confirm, and waits for the
    /// import to complete and navigation to transactions page.
    /// </remarks>
    [Given("I have imported these transactions")]
    [When("I import the selected transactions")]
    public async Task IImportTheSelectedTransactions()
    {
        // When: Click the Import button to open confirmation modal
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.ClickImportButtonAsync();

        // And: Confirm the import
        await importPage.ConfirmImportAsync();
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that the import review page displays the expected number of transactions.
    /// </summary>
    /// <param name="count">The expected number of transactions.</param>
    [Then("page should display {count} transactions")]
    [Then("I should see {count} transactions in the review list")]
    public async Task PageShouldDisplayTheTransactions(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify transaction count
        var actualCount = await importPage.GetTransactionCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the expected number of transactions are selected by default.
    /// </summary>
    /// <param name="count">The expected number of selected transactions.</param>
    [Then("{count} transactions should be selected by default")]
    [Then("all {count} transactions should be selected by default")]
    public async Task ThenTransactionsShouldBeSelectedByDefault(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify selected count
        var actualCount = await importPage.GetSelectedCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions to be selected but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the expected number of transactions are deselected by default.
    /// </summary>
    /// <param name="count">The expected number of deselected transactions.</param>
    [Then("{count} transactions should be deselected by default")]
    public async Task ThenTransactionsShouldBeDeselectedByDefault(int count)
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify deselected count
        var actualCount = await importPage.GetDeselectedCountAsync();
        NUnit.Framework.Assert.That(actualCount, NUnit.Framework.Is.EqualTo(count),
            $"Expected {count} transactions to be deselected but found {actualCount}");
    }

    /// <summary>
    /// Verifies that the import review queue has been completely cleared.
    /// </summary>
    /// <remarks>
    /// Navigates back to the import page and verifies that the empty state is displayed,
    /// indicating no pending import review transactions remain.
    ///
    /// Requires Objects
    /// - CurrentWorkspace
    /// </remarks>
    [Then("import review queue should be completely cleared")]
    [Then("import review queue should be empty")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ImportReviewQueueShouldBeCompletelyCleared()
    {
        // Then: Navigate back to import page
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the current workspace
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);

        // And: Verify empty state is displayed (no pending imports)
        var isEmpty = await importPage.IsEmptyStateAsync();
        Assert.That(isEmpty, Is.True,
            "Expected import review queue to be empty but transactions are still pending");
    }

    /// <summary>
    /// Then the review list contains the transactions uploaded earlier
    /// </summary>
    [Then("the review list contains the transactions uploaded earlier")]
    [RequiresObjects(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task TheReviewListContainsTheTransactionsUploadedEarlier()
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Get all payees currently displayed
        var actualPayees = await importPage.GetAllTransactionPayeesAsync();

        // And: Get the expected payees from object store
        var expectedPayees = _context.ObjectStore.Get<List<string>>(ObjectStoreKeys.UploadedTransactionPayees)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.UploadedTransactionPayees} not found in object store");

        // Verify that all of the uploaded transactions are visible in the review list
        var missingPayees = expectedPayees.Except(actualPayees).ToList();
        Assert.That(missingPayees, Is.Empty,
            $"All uploaded transactions should appear in the review list, but missing: {string.Join(", ", missingPayees)}");

    }

    /// <summary>
    /// Then all 5 transactions should be deselected by default
    /// </summary>
    [Then("all {count} transactions should be deselected by default")]
    public async Task AllTransactionsShouldBeDeselectedByDefault(int count)
    {
        await ThenTransactionsShouldBeDeselectedByDefault(count);
        await ThenTransactionsShouldBeSelectedByDefault(0);
    }

    /// <summary>
    /// Then no transactions should be highlighted for further review
    /// </summary>
    [Then("no transactions should be highlighted for further review")]
    public Task NoTransactionsShouldBeHighlightedForFurtherReview()
        => TransactionsShouldBeHighlightedForFurtherReview(0);

    /// <summary>
    /// Then no transactions should be highlighted for further review
    /// </summary>
    [Then("there should be {count} transactions highlighted for further review")]
    public async Task TransactionsShouldBeHighlightedForFurtherReview(int count)
    {
        var importPage = _context.GetOrCreatePage<ImportPage>();
        var highlightedCount = await importPage.GetWarningTransactionCountAsync();
        Assert.That(highlightedCount, Is.EqualTo(count), $"Expected {count} transactions to be highlighted for further review, but found {highlightedCount}");
    }



    #endregion

    /// <summary>
    /// Then I should be able to upload files
    /// </summary>
    [Then("I should be able to upload files")]
    public async Task IShouldBeAbleToUploadFiles()
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify that the file upload input is present and enabled
        var isUploadEnabled = await importPage.FileInput.IsEnabledAsync();
        Assert.That(isUploadEnabled, Is.True, "Expected to be able to upload files, but upload input is not enabled");
    }

    /// <summary>
    /// Then I should be able to upload files
    /// </summary>
    [Then("I should not be able to upload files")]
    public async Task IShouldNotBeAbleToUploadFiles()
    {
        // Then: Get the import page
        var importPage = _context.GetOrCreatePage<ImportPage>();

        // And: Verify that the file upload input is not available
        var isUploadVisible = await importPage.FileInput.IsVisibleAsync();
        Assert.That(isUploadVisible, Is.False, "Expected not to be able to upload files, but upload input is visible");
    }

    /// <summary>
    /// Then I should see a permission error message
    /// </summary>
    [Then("I should see a permission error message")]
    public async Task IShouldSeeAPermissionErrorMessage()
    {
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.PermissionDeniedError.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible });
        var hasErrorMessage = await importPage.PermissionDeniedError.IsVisibleAsync();
        Assert.That(hasErrorMessage, Is.True, "Expected to see a permission error message, but none was found");
    }

    /// <summary>
    /// Then I should see a warning about potential duplicates
    /// </summary>
    [Then("I should see a warning about potential duplicates")]
    public async Task IShouldSeeAWarningAboutPotentialDuplicates()
    {
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.DuplicateReviewAlert.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible });
        var hasErrorMessage = await importPage.DuplicateReviewAlert.IsVisibleAsync();
        Assert.That(hasErrorMessage, Is.True, "Expected to see a warning about potential duplicates, but none was found");
    }

    /// <summary>
    /// Then I should be able to complete the import review
    /// </summary>
    [Then("I should be able to complete the import review")]
    public async Task IShouldBeAbleToCompleteTheImportReview()
    {
        var importPage = _context.GetOrCreatePage<ImportPage>();
        var isImportButtonEnabled = await importPage.ImportButton.IsEnabledAsync();
        Assert.That(isImportButtonEnabled, Is.True, "Expected the import button to be enabled, but it was not");
    }

}
