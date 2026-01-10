using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.BankImport;

/// <summary>
/// Step definitions for bank import file upload operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles OFX file generation, upload, and upload completion verification.
/// </remarks>
public class BankImportUploadSteps(ITestContext context) : BankImportStepsBase(context)
{
    #region Steps: GIVEN

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
    [Given("I have uploaded an OFX file with {count} transactions")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    [ProvidesObjects(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task IHaveUploadedAnOFXFileWithNewTransactions(int count)
    {
        // Given: Generate OFX file with specified number of transactions
        var ofxFilePath = OfxFileGenerator.GenerateOfxFile(count);

        // And: Get workspace name
        var workspaceName = GetCurrentWorkspace();

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
        var workspaceName = GetCurrentWorkspace();

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

    #endregion
}
