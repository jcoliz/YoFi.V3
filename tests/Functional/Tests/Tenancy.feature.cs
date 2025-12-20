using NUnit.Framework;
using YoFi.V3.Tests.Functional.Steps;
using YoFi.V3.Tests.Functional.Helpers;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// Workspace Management
/// As a YoFi user
/// I want to create and manage separate financial workspaces
/// So that I can organize my finances by purpose and share them with others
/// </summary>
public class WorkspaceManagementTests : WorkspaceTenancySteps
{
    [SetUp]
    public async Task Background()
    {
        // Given the application is running
        await GivenTheApplicationIsRunning();

        // And these users exist:
        var table = new DataTable(
            ["Username"],
            ["alice"],
            ["bob"],
            ["charlie"]
        );
        await GivenTheseUsersExist(table);
    }

    #region Rule: Getting Started
    // New users automatically receive their own workspace to begin tracking finances

    /// <summary>
    /// New user automatically has a personal workspace
    /// </summary>
    [Test]
    public async Task NewUserAutomaticallyHasAPersonalWorkspace()
    {
        // Given the application is running
        await GivenTheApplicationIsRunning();

        // When a new user "david" registers and logs in
        await WhenANewUserRegistersAndLogsIn("david");

        // Then user should have a workspace ready to use
        await ThenUserShouldHaveAWorkspaceReadyToUse();

        // And the workspace should be personalized with the name "david"
        await ThenTheWorkspaceShouldBePersonalizedWithTheName("david");
    }

    #endregion

    #region Rule: Creating Workspaces
    // Users can create multiple workspaces for different purposes

    /// <summary>
    /// User creates a workspace for a specific purpose
    /// </summary>
    [Test]
    public async Task UserCreatesAWorkspaceForASpecificPurpose()
    {
        // Given I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // When I create a new workspace called "Alice's Finances" for "My personal finances"
        await WhenICreateANewWorkspaceCalledFor("Alice's Finances", "My personal finances");

        // Then I should see "Alice's Finances" in my workspace list
        await ThenIShouldSeeInMyWorkspaceList("Alice's Finances");

        // And I should be able to manage that workspace
        await ThenIShouldBeAbleToManageThatWorkspace();
    }

    /// <summary>
    /// User organizes finances across multiple workspaces
    /// </summary>
    [Test]
    public async Task UserOrganizesFinancesAcrossMultipleWorkspaces()
    {
        // Given I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // When I create a workspace called "Personal Budget"
        await WhenICreateAWorkspaceCalled("Personal Budget");

        // And I create a workspace called "Side Business"
        await WhenICreateAWorkspaceCalled("Side Business");

        // Then I should have 2 workspaces available
        await ThenIShouldHaveWorkspacesAvailable(2);

        // And I can work with either workspace independently
        await ThenICanWorkWithEitherWorkspaceIndependently();
    }

    #endregion

    #region Rule: Viewing Workspaces
    // Users can see all workspaces they have access to

    /// <summary>
    /// User views all their workspaces
    /// </summary>
    [Test]
    public async Task UserViewsAllTheirWorkspaces()
    {
        // Given "alice" has access to these workspaces:
        var table = new DataTable(
            ["Workspace Name", "My Role"],
            ["Personal", "Owner"],
            ["Family Budget", "Editor"],
            ["Tax Records", "Viewer"]
        );
        await GivenUserHasAccessToTheseWorkspaces("alice", table);

        // And I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // When I view my workspace list
        await WhenIViewMyWorkspaceList();

        // Then I should see all 3 workspaces
        await ThenIShouldSeeAllWorkspaces(3);

        // And I should see what I can do in each workspace
        // FIXME: This step should ensure I have the CORRECT permissions shown per workspace
        // right now, it literally checks that I have some role. Currently the bulk
        // upload only sets roles.
        await ThenIShouldSeeWhatICanDoInEachWorkspace();
    }

    /// <summary>
    /// User views workspace details
    /// </summary>
    [Test]
    public async Task UserViewsWorkspaceDetails()
    {
        // Given "bob" owns a workspace called "My Finances"
        await GivenUserOwnsAWorkspaceCalled("bob", "My Finances");

        // And I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // Bug AV#1979 call stack here
        // When I view the details of "My Finances"
        await WhenIViewTheDetailsOf("My Finances");

        // Then I should see the workspace information
        await ThenIShouldSeeTheWorkspaceInformation();

        // And I should see when it was created
        await ThenIShouldSeeWhenItWasCreated();
    }

    #endregion

    #region Rule: Managing Workspace Settings
    // Workspace owners can customize their workspace settings

    /// <summary>
    /// Owner updates workspace information
    /// </summary>
    [Test]
    public async Task OwnerUpdatesWorkspaceInformation()
    {
        // Given "alice" owns a workspace called "Old Name"
        await GivenUserOwnsAWorkspaceCalled("alice", "Old Name");

        // And I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // When I rename it to "New Name"
        await WhenIRenameItTo("New Name");

        // And I update the description to "Updated description text"
        await WhenIUpdateTheDescriptionTo("Updated description text");

        // Then the workspace should reflect the changes
        await ThenTheWorkspaceShouldReflectTheChanges();

        // And I should see "New Name" in my workspace list
        await ThenIShouldSeeInMyWorkspaceList("New Name");
    }

    /// <summary>
    /// Non-owner cannot change workspace settings
    /// </summary>
    [Test]
    public async Task NonOwnerCannotChangeWorkspaceSettings()
    {
        // Given "bob" can edit data in "Family Budget"
        await GivenUserCanEditDataIn("bob", "Family Budget");

        // And I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // When I try to change the workspace name or description
        await WhenITryToChangeTheWorkspaceNameOrDescription();

        // Then I should not be able to make those changes
        await ThenIShouldNotBeAbleToMakeThoseChanges();
    }

    #endregion

    #region Rule: Removing Workspaces
    // Workspace owners can permanently delete workspaces they no longer need

    /// <summary>
    /// Owner removes an unused workspace
    /// </summary>
    [Test]
    public async Task OwnerRemovesAnUnusedWorkspace()
    {
        // Given "alice" owns a workspace called "Test Workspace"
        await GivenUserOwnsAWorkspaceCalled("alice", "Test Workspace");

        // And I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // When I delete "Test Workspace"
        // AB#1976 Call Stack Here
        await WhenIDelete("Test Workspace");

        // Then "Test Workspace" should no longer appear in my list
        await ThenShouldNoLongerAppearInMyList("Test Workspace");
    }

    /// <summary>
    /// Non-owner cannot delete a workspace
    /// </summary>
    [Test]
    public async Task NonOwnerCannotDeleteAWorkspace()
    {
        // Given "charlie" can view data in "Shared Workspace"
        await GivenUserCanViewDataIn("charlie", "Shared Workspace");

        // And I am logged in as "charlie"
        await GivenIAmLoggedInAs("charlie");

        // When I try to delete "Shared Workspace"
        await WhenITryToDelete("Shared Workspace");

        // Then the workspace should remain intact
        await ThenTheWorkspaceShouldRemainIntact();
    }

    #endregion

    #region Rule: Data Isolation Between Workspaces
    // Each workspace maintains its own separate financial data

    /// <summary>
    /// User views transactions in a specific workspace
    /// </summary>
    [Test]
    public async Task UserViewsTransactionsInASpecificWorkspace()
    {
        // Given "alice" has access to these workspaces:
        var table = new DataTable(
            ["Workspace Name", "My Role"],
            ["Personal", "Owner"],
            ["Business", "Owner"]
        );
        await GivenUserHasAccessToTheseWorkspaces("alice", table);

        // And "Personal" contains 5 transactions
        await GivenWorkspaceContainsTransactions("Personal", 5);

        // And "Business" contains 3 transactions
        await GivenWorkspaceContainsTransactions("Business", 3);

        // And I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // When I view transactions in "Personal"
        await WhenIViewTransactionsIn("Personal");

        // Then I should see exactly 5 transactions
        await ThenIShouldSeeExactlyTransactions(5);

        // And they should all be from "Personal" workspace
        await ThenTheyShouldAllBeFromWorkspace("Personal");

        // And I should not see any transactions from "Business"
        await ThenIShouldNotSeeAnyTransactionsFrom("Business");
    }

    /// <summary>
    /// User cannot access data in workspaces they don't have access to
    /// </summary>
    [Test]
    public async Task UserCannotAccessDataInWorkspacesTheyDontHaveAccessTo()
    {
        // Given "bob" has access to "Family Budget"
        await GivenUserHasAccessTo("bob", "Family Budget");

        // And there is a workspace called "Private Finances" that "bob" doesn't have access to
        await GivenThereIsAWorkspaceCalledThatUserDoesntHaveAccessTo("Private Finances", "bob");

        // And I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // When I try to view transactions in "Private Finances"
        await WhenITryToViewTransactionsIn("Private Finances");

        // Then I should not be able to access that data
        await ThenIShouldNotBeAbleToAccessThatData();
    }

    #endregion

    #region Rule: Permission Levels
    // Different permission levels control what users can do in a workspace

    /// <summary>
    /// Viewer can see but not change data
    /// </summary>
    [Test]
    public async Task ViewerCanSeeButNotChangeData()
    {
        // Given "charlie" can view data in "Family Budget"
        await GivenUserCanViewDataIn("charlie", "Family Budget");

        // And "Family Budget" contains 3 transactions
        await GivenWorkspaceContainsTransactions("Family Budget", 3);

        // And I am logged in as "charlie"
        await GivenIAmLoggedInAs("charlie");

        // When I view transactions in "Family Budget"
        await WhenIViewTransactionsIn("Family Budget");

        // Then I should see the transactions
        await ThenIShouldSeeTheTransactions();

        // But when I try to add or edit transactions
        await WhenITryToAddOrEditTransactions();

        // Then I should not be able to make those changes
        await ThenIShouldNotBeAbleToMakeThoseChanges();
    }

    /// <summary>
    /// Editor can view and modify data
    /// </summary>
    [Test]
    public async Task EditorCanViewAndModifyData()
    {
        // Given "bob" can edit data in "Family Budget"
        await GivenUserCanEditDataIn("bob", "Family Budget");

        // And I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // When I add a transaction to "Family Budget"
        await WhenIAddATransactionTo("Family Budget");

        // Then the transaction should be saved successfully
        await ThenTheTransactionShouldBeSavedSuccessfully();

        // And when I update that transaction
        await WhenIUpdateThatTransaction();

        // FAILS here due to changes NOT saved
        // Then my changes should be saved
        await ThenMyChangesShouldBeSaved();

        // And when I delete that transaction
        await WhenIDeleteThatTransaction();

        // Then it should be removed
        await ThenItShouldBeRemoved();
    }

    /// <summary>
    /// Owner can do everything including managing the workspace
    /// </summary>
    [Test]
    public async Task OwnerCanDoEverythingIncludingManagingTheWorkspace()
    {
        // Given "alice" owns "My Workspace"
        await GivenUserOwnsAWorkspaceCalled("alice", "My Workspace");

        // And "My Workspace" contains 3 transactions
        await GivenWorkspaceContainsTransactions("My Workspace", 3);

        // And I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // Then I can add, edit, and delete transactions
        await ThenICanAddEditAndDeleteTransactions();

        // And I can change workspace settings
        await ThenICanChangeWorkspaceSettings();

        // And I can remove the workspace if needed
        await ThenICanRemoveTheWorkspaceIfNeeded();
    }

    #endregion

    #region Rule: Privacy and Security
    // Users can only see and access workspaces they have permission to use

    /// <summary>
    /// Workspace list shows only accessible workspaces
    /// </summary>
    [Test]
    public async Task WorkspaceListShowsOnlyAccessibleWorkspaces()
    {
        // Given "bob" has access to "Family Budget"
        await GivenUserHasAccessTo("bob", "Family Budget");

        // And there are other workspaces in the system:
        var table = new DataTable(
            ["Workspace Name", "Owner"],
            ["Private Data", "alice"],
            ["Charlie's Taxes", "charlie"]
        );
        await GivenThereAreOtherWorkspacesInTheSystem(table);

        // And I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // When I view my workspace list
        await WhenIViewMyWorkspaceList();

        // Then I should see only "Family Budget" in my list
        await ThenIShouldSeeOnlyInMyList("Family Budget");

        // And I should not see "Private Data" in my list
        await ThenIShouldNotSeeInMyList("Private Data");

        // And I should not see "Charlie's Taxes" in my list
        await ThenIShouldNotSeeInMyList("Charlie's Taxes");

        // And the workspace count should be 1
        await ThenIShouldHaveWorkspacesAvailable(1);
    }

    #endregion
}
