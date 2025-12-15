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

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
        // Given I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // And I have access to these workspaces:
        var table = new DataTable(
            ["Workspace Name", "My Role"],
            ["Personal", "Owner"],
            ["Family Budget", "Editor"],
            ["Tax Records", "Viewer"]
        );
        await GivenIHaveAccessToTheseWorkspaces(table);

        // When I view my workspace list
        await WhenIViewMyWorkspaceList();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
        // Given I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // And I have a workspace called "My Finances"
        await GivenIHaveAWorkspaceCalled("My Finances");

        // When I view the details of "My Finances"
        await WhenIViewTheDetailsOf("My Finances");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
    [Explicit("WIP")]
    public async Task OwnerUpdatesWorkspaceInformation()
    {
        // Given I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // And I own a workspace called "Old Name"
        await GivenIAWorkspaceCalled("Old Name");

        // When I rename it to "New Name"
        await WhenIRenameItTo("New Name");

        // And I update the description to "Updated description text"
        await WhenIUpdateTheDescriptionTo("Updated description text");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then the workspace should reflect the changes
        await ThenTheWorkspaceShouldReflectTheChanges();

        // And I should see "New Name" in my workspace list
        await ThenIShouldSeeInMyWorkspaceList("New Name");
    }

    /// <summary>
    /// Non-owner cannot change workspace settings
    /// </summary>
    [Test]
    [Explicit("WIP")]
    public async Task NonOwnerCannotChangeWorkspaceSettings()
    {
        // Given I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // And I can edit data in "Family Budget"
        await GivenICanEditDataIn("Family Budget");

        // When I try to change the workspace name or description
        await WhenITryToChangeTheWorkspaceNameOrDescription();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
    [Explicit("WIP")]
    public async Task OwnerRemovesAnUnusedWorkspace()
    {
        // Given I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // And I own a workspace called "Test Workspace"
        await GivenIAWorkspaceCalled("Test Workspace");

        // When I delete "Test Workspace"
        await WhenIDelete("Test Workspace");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then "Test Workspace" should no longer appear in my list
        await ThenShouldNoLongerAppearInMyList("Test Workspace");
    }

    /// <summary>
    /// Non-owner cannot delete a workspace
    /// </summary>
    [Test]
    [Explicit("WIP")]
    public async Task NonOwnerCannotDeleteAWorkspace()
    {
        // Given I am logged in as "charlie"
        await GivenIAmLoggedInAs("charlie");

        // And I can view data in "Shared Workspace"
        await GivenICanViewDataIn("Shared Workspace");

        // When I try to delete "Shared Workspace"
        await WhenITryToDelete("Shared Workspace");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
    [Explicit("WIP")]
    public async Task UserViewsTransactionsInASpecificWorkspace()
    {
        // Given I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // And I have two workspaces:
        var table = new DataTable(
            ["Workspace Name", "My Role"],
            ["Personal", "Owner"],
            ["Business", "Owner"]
        );
        await GivenIHaveTwoWorkspaces(table);

        // And "Personal" contains 5 transactions
        await GivenWorkspaceContainsTransactions("Personal", 5);

        // And "Business" contains 3 transactions
        await GivenWorkspaceContainsTransactions("Business", 3);

        // When I view transactions in "Personal"
        await WhenIViewTransactionsIn("Personal");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
    [Explicit("WIP")]
    public async Task UserCannotAccessDataInWorkspacesTheyDontHaveAccessTo()
    {
        // Given I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // And I have access to "Family Budget"
        await GivenIHaveAccessTo("Family Budget");

        // And there is a workspace called "Private Finances" that I don't have access to
        await GivenThereIsAWorkspaceCalledThatIDontHaveAccessTo("Private Finances");

        // When I try to view transactions in "Private Finances"
        await WhenITryToViewTransactionsIn("Private Finances");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
    [Explicit("WIP")]
    public async Task ViewerCanSeeButNotChangeData()
    {
        // Given I am logged in as "charlie"
        await GivenIAmLoggedInAs("charlie");

        // And I can view data in "Family Budget"
        await GivenICanViewDataIn("Family Budget");

        // When I view transactions in "Family Budget"
        await WhenIViewTransactionsIn("Family Budget");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
    [Explicit("WIP")]
    public async Task EditorCanViewAndModifyData()
    {
        // Given I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // And I can edit data in "Family Budget"
        await GivenICanEditDataIn("Family Budget");

        // When I add a transaction to "Family Budget"
        await WhenIAddATransactionTo("Family Budget");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then the transaction should be saved successfully
        await ThenTheTransactionShouldBeSavedSuccessfully();

        // And when I update that transaction
        await WhenIUpdateThatTransaction();

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
    [Explicit("WIP")]
    public async Task OwnerCanDoEverythingIncludingManagingTheWorkspace()
    {
        // Given I am logged in as "alice"
        await GivenIAmLoggedInAs("alice");

        // And I own "My Workspace"
        await GivenIOwn("My Workspace");

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

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
    [Explicit("WIP")]
    public async Task WorkspaceListShowsOnlyAccessibleWorkspaces()
    {
        // Given I am logged in as "bob"
        await GivenIAmLoggedInAs("bob");

        // And I have access to "Family Budget"
        await GivenIHaveAccessTo("Family Budget");

        // And there are other workspaces in the system:
        var table = new DataTable(
            ["Workspace Name", "Owner"],
            ["Private Data", "alice"],
            ["Charlie's Taxes", "charlie"]
        );
        await GivenThereAreOtherWorkspacesInTheSystem(table);

        // When I view my workspace list
        await WhenIViewMyWorkspaceList();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see only "Family Budget" in my list
        await ThenIShouldSeeOnlyInMyList("Family Budget");

        // And I should not see "Private Data" in my list
        await ThenIShouldNotSeeInMyList("Private Data");

        // And I should not see "Charlie's Taxes" in my list
        await ThenIShouldNotSeeInMyList("Charlie's Taxes");

        // And the workspace count should be 1
        await ThenTheWorkspaceCountShouldBe(1);
    }

    #endregion
}
