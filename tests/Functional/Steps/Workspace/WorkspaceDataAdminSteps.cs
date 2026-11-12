using ClosedXML.Excel;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Workspace;

/// <summary>
/// Step definitions for workspace data administration.
/// </summary>
/// <remarks>
/// These steps handle REAL user data, as opposed to test data.
/// </remarks>
public class WorkspaceDataAdminSteps(ITestContext context) : WorkspaceStepsBase(context)
{
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
    /// When I preview and confirm the upload
    /// </summary>
    [When("I preview and confirm the upload")]
    public async Task IPreviewAndConfirmTheUpload()
    {
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.PreviewAndConfirmImportAsync();
    }

}
