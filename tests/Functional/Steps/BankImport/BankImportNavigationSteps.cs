using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.BankImport;

/// <summary>
/// Step definitions for bank import navigation operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles navigation to import pages and workspace selection.
/// </remarks>
public class BankImportNavigationSteps(ITestContext context) : BankImportStepsBase(context)
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
        var workspaceName = GetCurrentWorkspace();

        // And: Navigate to import page
        var importPage = _context.GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();

        // And: Select the workspace
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
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

    #endregion
}
