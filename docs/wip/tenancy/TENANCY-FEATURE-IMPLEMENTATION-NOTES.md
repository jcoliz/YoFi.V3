# Tenancy Feature Implementation Notes

**Status:** Implementation Planning
**Related:** [`tests/Functional/Features/Tenancy.feature`](../../tests/Functional/Features/Tenancy.feature)
**Test Control API Plan:** [`TEST-CONTROL-API-TENANCY-ENHANCEMENTS.md`](TEST-CONTROL-API-TENANCY-ENHANCEMENTS.md)

## Overview

Additional implementation notes and patterns for the Tenancy feature functional tests that complement the Test Control API enhancements.

---

## Permission Verification Pattern

**Scenario:** Testing role-based permission enforcement in the UI (e.g., Viewer can see but not edit).

### ✅ Best Approach: Abstract Availability Logic in BasePage

The page object should hide the implementation details of whether a control is unavailable due to being hidden vs. disabled. Steps should just check "is it available?"

### Add to BasePage.cs

```csharp
/// <summary>
/// Checks if a control is available to the user (visible and enabled)
/// </summary>
/// <param name="locator">The locator for the control to check</param>
/// <returns>True if the control is both visible and enabled, false otherwise</returns>
/// <remarks>
/// This abstracts away the implementation detail of whether unavailable controls
/// are hidden or disabled. From the user's perspective, if a control is either
/// hidden or disabled, it's not available for interaction.
/// </remarks>
public async Task<bool> IsAvailableAsync(ILocator locator)
{
    // First check visibility - if not visible, it's definitely not available
    var isVisible = await locator.IsVisibleAsync();
    if (!isVisible)
    {
        return false;
    }

    // If visible, check if it's enabled
    return await locator.IsEnabledAsync();
}
```

### Usage in TransactionsPage

Page objects can optionally provide convenience methods for specific controls:

```csharp
/// <summary>
/// Checks if the New Transaction button is available to the user
/// </summary>
public Task<bool> IsNewTransactionAvailableAsync() => IsAvailableAsync(NewTransactionButton);

/// <summary>
/// Checks if edit functionality is available for a specific transaction
/// </summary>
public Task<bool> IsEditAvailableAsync(string payeeName) => IsAvailableAsync(GetEditButton(payeeName));

/// <summary>
/// Checks if delete functionality is available for a specific transaction
/// </summary>
public Task<bool> IsDeleteAvailableAsync(string payeeName) => IsAvailableAsync(GetDeleteButton(payeeName));
```

### Step Implementation Example

**Scenario:** [Lines 124-129](../../tests/Functional/Features/Tenancy.feature#L124-L129) - Viewer can see but not change data

**Clean, simple implementation:**
```csharp
/// <summary>
/// Then: I should not be able to make those changes
/// </summary>
protected async Task ThenIShouldNotBeAbleToMakeThoseChanges()
{
    var transactionsPage = It<TransactionsPage>();

    // Then: New Transaction should not be available
    var canCreateNew = await transactionsPage.IsNewTransactionAvailableAsync();
    Assert.That(canCreateNew, Is.False,
        "New Transaction should not be available for Viewer role");

    // And: Edit/Delete should not be available for existing transactions
    if (await transactionsPage.GetTransactionCountAsync() > 0)
    {
        // Get first transaction's payee to test edit/delete availability
        var firstTransactionRow = transactionsPage.TransactionRows.First();
        var payeeCell = firstTransactionRow.Locator("td").Nth(1);
        var payeeName = await payeeCell.TextContentAsync();

        var canEdit = await transactionsPage.IsEditAvailableAsync(payeeName!);
        Assert.That(canEdit, Is.False,
            "Edit should not be available for Viewer role");

        var canDelete = await transactionsPage.IsDeleteAvailableAsync(payeeName!);
        Assert.That(canDelete, Is.False,
            "Delete should not be available for Viewer role");
    }
}
```

**Or, using the generic `IsAvailableAsync()` directly in steps:**
```csharp
/// <summary>
/// Then: I should not be able to make those changes
/// </summary>
protected async Task ThenIShouldNotBeAbleToMakeThoseChanges()
{
    var transactionsPage = It<TransactionsPage>();
    var basePage = (BasePage)transactionsPage;

    // Check availability using generic method
    var canCreateNew = await basePage.IsAvailableAsync(transactionsPage.NewTransactionButton);
    Assert.That(canCreateNew, Is.False,
        "New Transaction should not be available for Viewer role");
}
```

### Benefits of This Approach

✅ **Abstraction** - Steps don't care if control is hidden or disabled
✅ **Consistency** - Same availability logic everywhere
✅ **Flexibility** - UI can change from hidden to disabled without breaking tests
✅ **Readability** - `IsAvailableAsync()` is clearer than checking visibility AND enabled state
✅ **Reusability** - Generic `IsAvailableAsync(locator)` works for any control
✅ **Convenience** - Page-specific methods like `IsNewTransactionAvailableAsync()` for common checks

---

## Multi-User Session Management

### Pattern: Store User Context in Object Store

When tests need to switch between users (e.g., alice, bob, charlie):

```csharp
/// <summary>
/// Given: I am logged in as {username}
/// </summary>
protected async Task GivenIAmLoggedInAs(string username)
{
    // Get user credentials from Test Control API
    var credentials = await _testControlClient.GetUserCredentialsAsync(username);

    // Login via UI or API
    var loginPage = GetOrCreateLoginPage();
    await loginPage.NavigateAsync();
    await loginPage.EnterCredentialsAsync(credentials.Email, credentials.Password);
    await loginPage.ClickLoginButtonAsync();

    // Store current user context
    _objectStore.Add("CurrentUser", credentials);
    _objectStore.Add($"User_{username}", credentials); // For later retrieval
}
```

### Pattern: Background Section Setup

The Background section creates multiple users but doesn't log in as any specific one:

```csharp
/// <summary>
/// Given: these users exist
/// </summary>
protected async Task GivenTheseUsersExist(DataTable users)
{
    var usernames = users.Rows.Select(r => r["Username"]).ToArray();

    // Create all users via Test Control API
    var createdUsers = await _testControlClient.CreateBulkUsersAsync(usernames);

    // Store credentials for later use
    foreach (var user in createdUsers)
    {
        var username = user.Username.Replace("__TEST__", "");
        _objectStore.Add($"User_{username}_Credentials", user);
    }
}
```

---

## Data Table Patterns for Workspace Setup

### Pattern: Setup Workspaces with Roles

From [lines 49-53](../../tests/Functional/Features/Tenancy.feature#L49-L53):

```gherkin
Given I have access to these workspaces:
    | Workspace Name | My Role |
    | Personal       | Owner   |
    | Family Budget  | Editor  |
    | Tax Records    | Viewer  |
```

**Implementation:**
```csharp
/// <summary>
/// Given: I have access to these workspaces
/// </summary>
protected async Task GivenIHaveAccessToTheseWorkspaces(DataTable workspaces)
{
    var currentUser = The<TestUser>("CurrentUser");
    var username = currentUser.Username.Replace("__TEST__", "");

    // Build workspace setup requests
    var requests = workspaces.Rows.Select(row => new WorkspaceSetupRequest(
        Name: row["Workspace Name"],
        Description: $"Test workspace: {row["Workspace Name"]}",
        Role: row["My Role"]
    )).ToArray();

    // Create all workspaces via bulk API
    var createdWorkspaces = await _testControlClient.BulkWorkspaceSetupAsync(username, requests);

    // Store for later verification
    _objectStore.Add("UserWorkspaces", createdWorkspaces);
}
```

---

## Transaction Seeding Pattern

### Pattern: Seed Specific Transaction Counts

From [lines 106-107](../../tests/Functional/Features/Tenancy.feature#L106-L107):

```gherkin
And "Personal" contains 5 transactions
And "Business" contains 3 transactions
```

**Implementation:**
```csharp
/// <summary>
/// Given: {workspaceName} contains {count} transactions
/// </summary>
protected async Task GivenWorkspaceContainsTransactions(string workspaceName, int count)
{
    // Get workspace key from stored workspaces
    var workspaces = The<WorkspaceSetupResult[]>("UserWorkspaces");
    var workspace = workspaces.FirstOrDefault(w => w.Name == workspaceName);

    if (workspace == null)
    {
        throw new InvalidOperationException($"Workspace '{workspaceName}' not found in setup data");
    }

    // Seed transactions via Test Control API
    var request = new TransactionSeedRequest(
        Count: count,
        PayeePrefix: $"{workspaceName} Transaction"
    );

    var transactions = await _testControlClient.SeedTransactionsAsync(workspace.Key, request);

    // Store for later verification
    _objectStore.Add($"{workspaceName}_Transactions", transactions);
}
```

---

## Workspace Selection Pattern

### Pattern: Navigate to Specific Workspace

Many scenarios require switching the active workspace:

```csharp
/// <summary>
/// When: I view transactions in {workspaceName}
/// </summary>
protected async Task WhenIViewTransactionsIn(string workspaceName)
{
    var transactionsPage = GetOrCreateTransactionsPage();

    // Use workspace selector to switch workspaces
    await transactionsPage.WorkspaceSelector.OpenMenuAsync();
    await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);

    // Navigate to transactions page (may already be there)
    await transactionsPage.NavigateAsync();
    await transactionsPage.WaitForLoadingCompleteAsync();
}
```

---

## Security Scenario Pattern

### Updated Scenario: Workspace List Filtering

From [lines 151-162](../../tests/Functional/Features/Tenancy.feature#L151-L162):

```csharp
/// <summary>
/// Then: I should see only {workspaceName} in my list
/// </summary>
protected async Task ThenIShouldSeeOnlyWorkspaceInMyList(string workspaceName)
{
    var workspacesPage = GetOrCreateWorkspacesPage();
    await workspacesPage.NavigateAsync();
    await workspacesPage.WaitForLoadingCompleteAsync();

    // Verify only one workspace is visible
    var count = await workspacesPage.GetWorkspaceCountAsync();
    Assert.That(count, Is.EqualTo(1),
        $"Should see exactly 1 workspace, but found {count}");

    // Verify it's the expected workspace
    var hasWorkspace = await workspacesPage.HasWorkspaceAsync(workspaceName);
    Assert.That(hasWorkspace, Is.True,
        $"Should see workspace '{workspaceName}' in list");
}

/// <summary>
/// And: I should not see {workspaceName} in my list
/// </summary>
protected async Task AndIShouldNotSeeWorkspaceInMyList(string workspaceName)
{
    var workspacesPage = It<WorkspacesPage>();

    var hasWorkspace = await workspacesPage.HasWorkspaceAsync(workspaceName);
    Assert.That(hasWorkspace, Is.False,
        $"Should NOT see workspace '{workspaceName}' in list (security isolation)");
}
```

---

## Summary of Implementation Patterns

### ✅ Use Existing Infrastructure
- Page objects already expose locators - use them directly
- Playwright provides `IsEnabledAsync()`, `IsVisibleAsync()` - no wrappers needed
- Test Control API handles data seeding - no complex UI automation required

### ✅ Store Context in Object Store
- Current user credentials
- Created workspaces with keys
- Seeded transactions
- Allows flexible test data access across steps

### ✅ Leverage Test Control API
- Bulk user creation for Background
- Workspace setup with specific roles
- Transaction seeding with counts
- Clean slate between tests

### ✅ Keep Step Methods Simple
- One clear purpose per step
- Use helper methods for common patterns
- Assert early and clearly
- Use descriptive error messages

---

## Related Documentation

- [Test Control API Enhancement Plan](TEST-CONTROL-API-TENANCY-ENHANCEMENTS.md)
- [Tenancy Feature File](../../tests/Functional/Features/Tenancy.feature)
- [TransactionsPage POM](../../tests/Functional/Pages/TransactionsPage.cs)
- [WorkspacesPage POM](../../tests/Functional/Pages/WorkspacesPage.cs)
- [WorkspaceSelector Component](../../tests/Functional/Components/WorkspaceSelector.cs)
