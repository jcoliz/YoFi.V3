# Workspace Step Classes

This directory contains workspace-related step definition classes organized by domain responsibility.

## Architecture

All workspace step classes inherit from [`WorkspaceStepsBase`](WorkspaceStepsBase.cs), which provides:
- Common helper methods (prefix handling, object store access)
- Object store key constants
- Page object factory methods
- Composition of shared step classes (NavigationSteps, AuthSteps)

## Step Classes

### WorkspaceManagementSteps
**Purpose:** Workspace CRUD operations (Create, Rename, Update, Delete)

**Responsibilities:**
- Creating new workspaces
- Renaming workspaces
- Updating workspace descriptions
- Deleting workspaces
- Navigating to workspace pages

**Example Steps:**
- `[When("I create a new workspace called {name} for {description}")]`
- `[When("I rename it to {newName}")]`
- `[When("I update the description to {newDescription}")]`
- `[When("I delete {workspaceName}")]`

### WorkspacePermissionsSteps
**Purpose:** Role-based access control checks (Owner/Editor/Viewer)

**Responsibilities:**
- Checking if user can perform actions (edit, delete, etc.)
- Verifying permission-based UI element visibility
- Role-based access control assertions

**Example Steps:**
- `[When("I try to change the workspace name or description")]`
- `[When("I try to delete {workspaceName}")]`
- `[Then("I should not be able to make those changes")]`
- `[Then("the workspace should remain intact")]`

### WorkspaceDataSteps
**Purpose:** Test data setup and workspace entitlements

**Responsibilities:**
- Setting up workspaces with specific roles for users
- Seeding transactions into workspaces
- Creating multi-workspace test scenarios
- Bulk workspace setup operations

**Example Steps:**
- `[Given("I have an active workspace {workspaceName}")]`
- `[Given("{username} owns a workspace called {workspaceName}")]`
- `[Given("{username} has access to these workspaces:")]`
- `[Given("{username} can edit data in {workspaceName}")]`
- `[Given("{workspaceName} contains {transactionCount} transactions")]`

**Current Implementation:** [`WorkspaceDataSteps.cs`](WorkspaceDataSteps.cs)

### WorkspaceAssertionSteps
**Purpose:** Verification operations (Then steps)

**Responsibilities:**
- Verifying workspace visibility in lists
- Checking workspace counts
- Verifying workspace state changes
- Transaction visibility assertions
- Role badge verification

**Example Steps:**
- `[Then("I should see {workspaceName} in my workspace list")]`
- `[Then("I should have {expectedCount} workspaces available")]`
- `[Then("the workspace should reflect the changes")]`
- `[Then("I should see what I can do in each workspace")]`

## Composition Pattern

Test classes compose the workspace step classes as needed:

```csharp
public class WorkspaceManagementTests : FunctionalTestBase
{
    protected WorkspaceManagementSteps ManagementSteps => _mgmtSteps ??= new(this);
    private WorkspaceManagementSteps? _mgmtSteps;

    protected WorkspacePermissionsSteps PermissionsSteps => _permSteps ??= new(this);
    private WorkspacePermissionsSteps? _permSteps;

    protected WorkspaceDataSteps DataSteps => _dataSteps ??= new(this);
    private WorkspaceDataSteps? _dataSteps;

    protected WorkspaceAssertionSteps AssertionSteps => _assertSteps ??= new(this);
    private WorkspaceAssertionSteps? _assertSteps;

    [Test]
    public async Task UserCreatesWorkspace()
    {
        // Setup
        await AuthSteps.GivenIAmLoggedInAs("alice");

        // Action
        await ManagementSteps.WhenICreateANewWorkspaceCalled("My Workspace");

        // Verification
        await AssertionSteps.ThenIShouldSeeInMyWorkspaceList("My Workspace");
    }
}
```

## Design Principles

1. **Single Responsibility** - Each class handles one aspect of workspace functionality
2. **Composition over Inheritance** - Step classes compose AuthSteps and NavigationSteps rather than inheriting complex behavior
3. **DRY** - Common functionality lives in WorkspaceStepsBase
4. **Testability** - Clear separation makes it easy to test individual concerns
5. **Maintainability** - Smaller, focused classes are easier to understand and modify

## Migration from WorkspaceTenancySteps

The monolithic [`WorkspaceTenancySteps.cs`](../WorkspaceTenancySteps.cs) (1445 lines) is being split into these domain-focused classes. During migration:
- Existing tests continue to work with the old class
- New tests use the new composition architecture
- Gradual migration of tests from inheritance to composition

## Object Store Keys

All workspace-related object store keys are defined in [`WorkspaceStepsBase`](WorkspaceStepsBase.cs:13-22):
- `KEY_CURRENT_WORKSPACE` - Currently selected workspace name
- `KEY_NEW_WORKSPACE_NAME` - New name after rename operation
- `KEY_CAN_DELETE_WORKSPACE` - Permission check result
- `KEY_CAN_MAKE_DESIRED_CHANGES` - Generic permission check result
- `KEY_HAS_WORKSPACE_ACCESS` - Workspace access check result

## Test Prefix Handling

All workspace names in step methods use **user-readable names** (e.g., "Personal Budget"). The `__TEST__` prefix is added automatically via `AddTestPrefix()` when calling APIs or storing in object store. This ensures:
- Feature files remain readable
- API calls include the test prefix
- Object store keys are consistent
- Test isolation is maintained
