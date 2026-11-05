using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Steps.Transaction;

namespace YoFi.V3.Tests.Functional.Steps.Workspace;

/// <summary>
/// Step definitions for workspace verification and assertion operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles verification of workspace state and visibility:
/// - Verifying workspace visibility in lists
/// - Checking workspace counts
/// - Verifying workspace state changes
/// - Transaction visibility assertions
/// - Role badge verification
///
/// These steps perform assertions on workspace data and UI state to verify
/// that workspace operations completed successfully.
/// </remarks>
public class WorkspaceAssertionSteps(ITestContext context) : WorkspaceStepsBase(context)
{
    #region Helper Steps

    TransactionDataSteps transactionDataSteps = new(context);
    TransactionListSteps transactionListSteps = new(context);
    PayeeRuleSteps payeeRuleSteps = new(context);

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that the user has at least one workspace available.
    /// </summary>
    /// <remarks>
    /// Navigates to transactions page and checks that the "no workspace" message
    /// is not visible, indicating at least one workspace exists.
    /// </remarks>
    [Then("user should have a workspace ready to use")]
    public async Task ThenUserShouldHaveAWorkspaceReadyToUse()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        // Get the current workspace name
        var noWorkspaceMessage = await transactionsPage.WorkspaceSelector.NoWorkspaceMessage.IsVisibleAsync();

        Assert.That(noWorkspaceMessage, Is.False, "User should have at least one workspace available");
    }

    /// <summary>
    /// Verifies that the workspace name contains the expected personalized name.
    /// </summary>
    /// <param name="expectedName">The expected name portion (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page, waits for workspace
    /// selector to show a name (up to 5 seconds), and verifies name contains
    /// expected value. Used after registration to verify personalized workspace creation.
    /// </remarks>
    [Then("the workspace should be personalized with the name {expectedName}")]
    public async Task ThenTheWorkspaceShouldBePersonalizedWithTheName(string expectedName)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var fullExpectedName = AddTestPrefix(expectedName);

        // Wait for the workspace selector to show a workspace name
        // After registration/login, the workspace might not be immediately visible
        await transactionsPage.WorkspaceSelector.CurrentWorkspaceName.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var workspaceName = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(workspaceName, Does.Contain(expectedName), $"Workspace name should contain '{expectedName}'");
    }

    /// <summary>
    /// Verifies that a workspace appears in the user's workspace list.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix and waits for the workspace card on the list page.
    /// </remarks>
    [Then("I should see {workspaceName} in my workspace list")]
    public async Task ThenIShouldSeeInMyWorkspaceList(string workspaceName)
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        await workspacesPage.WaitForWorkspaceAsync(fullWorkspaceName);
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.True,
            $"Workspace '{fullWorkspaceName}' should be visible in the list");
    }

    /// <summary>
    /// Verifies that the user has Owner permissions on the current workspace.
    /// </summary>
    /// <remarks>
    /// Retrieves current workspace from object store, navigates to workspaces page,
    /// and verifies the user's role is "Owner".
    /// </remarks>
    [Then("I should be able to manage that workspace")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]

    public async Task ThenIShouldBeAbleToManageThatWorkspace()
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        // Get the current workspace name
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        if (workspaceName != null)
        {
            var role = await workspacesPage.GetWorkspaceRoleAsync(workspaceName);
            Assert.That(role, Is.EqualTo("Owner"), "User should be Owner of the workspace");
            Assert.That(await workspacesPage.IsManageAvailableAsync(workspaceName), Is.True);
            await workspacesPage.OpenManagementAsync(workspaceName);
        }
    }

    /// <summary>
    /// Verifies that the user has exactly the expected number of workspaces.
    /// </summary>
    /// <param name="expectedCount">The expected workspace count.</param>
    /// <remarks>
    /// Navigates to workspaces page and counts visible workspaces.
    /// Supports multiple step phrasings via multiple attributes.
    /// </remarks>
    [Then("I should have {expectedCount} workspaces available")]
    [Then("the workspace count should be {expectedCount}")]
    [Then("I should see all {expectedCount} workspaces")]
    public async Task ThenIShouldHaveWorkspacesAvailable(int expectedCount)
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var actualCount = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(actualCount, Is.EqualTo(expectedCount),
            $"Should have exactly {expectedCount} workspaces");
    }

    /// <summary>
    /// Verifies that the user can switch between at least 2 workspaces.
    /// </summary>
    /// <remarks>
    /// Navigates to transactions page and checks that workspace selector shows
    /// at least 2 available workspaces, confirming workspace independence.
    /// </remarks>
    [Then("I can work with either workspace independently")]
    public async Task ThenICanWorkWithEitherWorkspaceIndependently()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var availableWorkspaces = await transactionsPage.WorkspaceSelector.GetAvailableWorkspacesAsync();
        Assert.That(availableWorkspaces.Length, Is.GreaterThanOrEqualTo(2), "Should have at least 2 workspaces available");
    }

    /// <summary>
    /// Verifies that role badges are displayed for all workspaces.
    /// </summary>
    /// <remarks>
    /// Navigates to workspaces page and verifies each workspace in tracked workspaces
    /// has a valid role badge (Owner, Editor, or Viewer). Confirms at least one
    /// workspace exists.
    /// </remarks>
    [Then("I should see what I can do in each workspace")]
    public async Task ThenIShouldSeeWhatICanDoInEachWorkspace()
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        await workspacesPage.AssertCardActionsAsync();
    }

    /// <summary>
    /// Verifies the route-targeted workspace information and management cards.
    /// </summary>
    /// <remarks>
    /// Retrieves expected workspace name from object store (KEY_CURRENT_WORKSPACE)
    /// and verifies the management page's route, heading, and settings.
    /// </remarks>
    [Then("I should see the workspace information")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]

    public async Task ThenIShouldSeeTheWorkspaceInformation()
    {
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();

        var expected = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        await managementPage.AssertWorkspaceInformationAsync(expected, _context.GetWorkspaceKey(expected));
    }

    /// <summary>
    /// Verifies that the workspace displays a valid creation date.
    /// </summary>
    /// <remarks>
    /// Retrieves current workspace from object store, navigates to workspaces page,
    /// gets the created date from workspace card, and verifies it's a valid DateTime.
    /// </remarks>
    [Then("I should see when it was created")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]

    public async Task ThenIShouldSeeWhenItWasCreated()
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var currentWorkspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);
        var createdDate = await workspacesPage.GetWorkspaceCardCreatedDate(currentWorkspaceName);

        // Can I verify that it looks like a date?
        DateTime parsedDate;
        bool isValidDate = DateTime.TryParse(createdDate, out parsedDate);
        Assert.That(isValidDate, Is.True, "Workspace created date should be a valid date");
    }

    /// <summary>
    /// Verifies that workspace rename or description update was successful.
    /// </summary>
    /// <remarks>
    /// Retrieves new workspace name from object store (KEY_NEW_WORKSPACE_NAME),
    /// and verifies the management page displays the updated workspace.
    ///
    /// Requires Objects:
    /// - NewWorkspaceName
    /// </remarks>
    [Then("the workspace should reflect the changes")]
    [Requires(ObjectStoreKeys.NewWorkspaceName)]
    public async Task ThenTheWorkspaceShouldReflectTheChanges()
    {
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        var newName = _context.ObjectStore.Get<string>(ObjectStoreKeys.NewWorkspaceName);
        await managementPage.WaitForWorkspaceAsync(newName);
    }

    /// <summary>
    /// Verifies that a workspace no longer appears in the user's list after deletion.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Checks workspaces page and verifies workspace is absent.
    /// </remarks>
    [Then("{workspaceName} should no longer appear in my list")]
    public async Task ThenShouldNoLongerAppearInMyList(string workspaceName)
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();

        var fullWorkspaceName = AddTestPrefix(workspaceName);
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.False,
            $"Workspace '{fullWorkspaceName}' should not be in the list");
    }

    /// <summary>
    /// Verifies that only one specific workspace is visible in the list.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to workspaces page, verifies count is exactly 1,
    /// and confirms it's the expected workspace. Used for access isolation tests.
    /// </remarks>
    [Then("I should see only {workspaceName} in my list")]
    public async Task ThenIShouldSeeOnlyInMyList(string workspaceName)
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Verify exactly one workspace is visible
        var count = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(count, Is.EqualTo(1), "Should see exactly one workspace");

        // Verify it's the expected workspace
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.True,
            $"The only workspace should be '{fullWorkspaceName}'");
    }

    /// <summary>
    /// Verifies that a specific workspace is not visible in the list.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Checks workspaces page and verifies workspace is absent.
    /// Used for access control and isolation tests.
    /// </remarks>
    [Then("I should not see {workspaceName} in my list")]
    public async Task ThenIShouldNotSeeInMyList(string workspaceName)
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();

        var fullWorkspaceName = AddTestPrefix(workspaceName);
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.False,
            $"Workspace '{fullWorkspaceName}' should not be visible");
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
    /// Given My workspace has existing transactions and payee matching rules
    /// </summary>
    /// <remarks>
    /// This is a meta-step to allow the Gherkin to express higher-level BDD scenarios.
    /// * IHaveSomeExistingTransactionsInMyWorkspace(5)
    /// * IHaveSomeExistingPayeeMatchingRulesInMyWorkspace(5)
    /// </remarks>
    [Given("My workspace has existing transactions and payee matching rules")]
    [Requires(ObjectStoreKeys.LoggedInAs)]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    [Provides(ObjectStoreKeys.ExistingTransactionKeys)]
    [Provides(ObjectStoreKeys.ExistingTransactions)]
    [Provides(ObjectStoreKeys.ExpectedPayeeRules)]
    public async Task MyWorkspaceHasExistingTransactionsAndPayeeMatchingRules()
    {
        await transactionDataSteps.IHaveSomeExistingTransactionsInMyWorkspace(5);
        await payeeRuleSteps.IHaveSomeExistingPayeeMatchingRulesInMyWorkspace(5);
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
}
