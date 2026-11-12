using YoFi.V3.Tests.Functional.Infrastructure.TestControl;
using YoFi.V3.Tests.Functional.Infrastructure;

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
    public async Task IUploadAFileContainingOriginalYoFiPayees()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// When I preview and confirm the upload
    /// </summary>
    [When("I preview and confirm the upload")]
    public async Task IPreviewAndConfirmTheUpload()
    {
        throw new NotImplementedException();
    }

}
