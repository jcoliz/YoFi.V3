# Page Model Thickness: Detailed Comparison

## Current State Analysis

Your project currently uses **thick page models** where complex workflows are encapsulated in page objects. Let's analyze this with concrete examples from your codebase.

## Option A: Thick Page Models (Current Approach)

### Current Example: WorkspacesPage

**Page Model ([`Pages/WorkspacesPage.cs:161-170`](../Pages/WorkspacesPage.cs:161-170)):**
```csharp
/// <summary>
/// Creates a new workspace with the given name and optional description
/// </summary>
public async Task CreateWorkspaceAsync(string name, string? description = null)
{
    await OpenCreateFormAsync();
    await CreateNameInput.FillAsync(name);
    if (!string.IsNullOrEmpty(description))
    {
        await CreateDescriptionInput.FillAsync(description);
    }
    await ClickCreateButtonAsync();
}
```

**Step Definition ([`Steps/WorkspaceTenancySteps.cs:424-434`](../Steps/WorkspaceTenancySteps.cs:424-434)):**
```csharp
protected async Task WhenICreateANewWorkspaceCalledFor(string name, string description)
{
    var workspaceName = AddTestPrefix(name);
    var workspacesPage = GetOrCreateWorkspacesPage();
    await workspacesPage.NavigateAsync();
    await workspacesPage.CreateWorkspaceAsync(workspaceName, description);

    // Store the workspace name for future reference
    _objectStore.Add(KEY_LAST_CREATED_WORKSPACE, workspaceName);
    _objectStore.Add(KEY_CURRENT_WORKSPACE, workspaceName);
}
```

**Characteristics:**
- ✅ **Single line** to create workspace in step
- ✅ **Reusable** across multiple scenarios
- ✅ **API waiting** handled in page model
- ❌ **Inflexible** - Can't easily test partial workflows
- ❌ **Mixed concerns** - Navigation + workflow in one method

---

## Option B: Thin Page Models + Complex Steps

### How It Would Look: WorkspacesPage (Refactored)

**Page Model (Thin):**
```csharp
// LOCATORS (unchanged)
public ILocator CreateWorkspaceButton => PageHeading.GetByTestId("create-workspace-button");
public ILocator CreateFormCard => Page!.GetByTestId("create-form-card");
public ILocator CreateNameInput => Page!.Locator("#create-name");
public ILocator CreateDescriptionInput => Page!.Locator("#create-description");
public ILocator CreateButton => CreateFormCard.GetByTestId("create-submit-button");

// SINGLE ACTIONS ONLY
/// <summary>
/// Clicks the Create Workspace button to open the form
/// </summary>
public async Task ClickCreateWorkspaceButtonAsync()
{
    await CreateWorkspaceButton.ClickAsync();
    await CreateFormCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
}

/// <summary>
/// Fills the workspace name field
/// </summary>
public async Task FillNameAsync(string name)
{
    await CreateNameInput.FillAsync(name);
}

/// <summary>
/// Fills the workspace description field
/// </summary>
public async Task FillDescriptionAsync(string description)
{
    await CreateDescriptionInput.FillAsync(description);
}

/// <summary>
/// Clicks the create button and waits for API call
/// </summary>
public async Task ClickCreateButtonAsync()
{
    await WaitForApi(async () =>
    {
        await CreateButton.ClickAsync();
    }, CreateTenantApiRegex);
}
```

**Step Definition (Complex):**
```csharp
protected async Task WhenICreateANewWorkspaceCalledFor(string name, string description)
{
    var workspaceName = AddTestPrefix(name);
    var workspacesPage = GetOrCreateWorkspacesPage();

    // Navigate to page
    await workspacesPage.NavigateAsync();

    // Open the create form
    await workspacesPage.ClickCreateWorkspaceButtonAsync();

    // Fill in the fields
    await workspacesPage.FillNameAsync(workspaceName);
    if (!string.IsNullOrEmpty(description))
    {
        await workspacesPage.FillDescriptionAsync(description);
    }

    // Submit the form
    await workspacesPage.ClickCreateButtonAsync();

    // Store the workspace name for future reference
    _objectStore.Add(KEY_LAST_CREATED_WORKSPACE, workspaceName);
    _objectStore.Add(KEY_CURRENT_WORKSPACE, workspaceName);
}
```

**Characteristics:**
- ✅ **Flexible** - Can test partial workflows (e.g., fill fields but don't submit)
- ✅ **Clear** - Each UI action is explicit
- ✅ **Testable edge cases** - Easy to insert waits, checks, or errors between steps
- ❌ **More code** in steps (6 lines vs 1 line)
- ❌ **Potential duplication** if multiple scenarios create workspaces

### Example: Testing Validation with Thin Models

**Scenario:** Test that form validation prevents submission without a name

**With Thin Models (Easy):**
```csharp
protected async Task WhenITryToCreateWorkspaceWithoutName()
{
    var workspacesPage = GetOrCreateWorkspacesPage();
    await workspacesPage.NavigateAsync();
    await workspacesPage.ClickCreateWorkspaceButtonAsync();
    // Skip FillNameAsync
    await workspacesPage.FillDescriptionAsync("Test Description");
    await workspacesPage.ClickCreateButtonAsync();
}
```

**With Thick Models (Requires New Method):**
```csharp
// Need to add to WorkspacesPage:
public async Task CreateWorkspaceWithoutNameAsync(string? description = null)
{
    await OpenCreateFormAsync();
    // Skip name
    if (!string.IsNullOrEmpty(description))
    {
        await CreateDescriptionInput.FillAsync(description);
    }
    await ClickCreateButtonAsync();
}

protected async Task WhenITryToCreateWorkspaceWithoutName()
{
    var workspacesPage = GetOrCreateWorkspacesPage();
    await workspacesPage.NavigateAsync();
    await workspacesPage.CreateWorkspaceWithoutNameAsync("Test Description");
}
```

---

## Option C: Hybrid Approach (Recommended)

### How It Would Look: WorkspacesPage (Hybrid)

**Page Model (Hybrid - Both Levels):**
```csharp
#region Single Actions (Primitives)

/// <summary>
/// Clicks the Create Workspace button to open the form
/// </summary>
public async Task ClickCreateWorkspaceButtonAsync()
{
    await CreateWorkspaceButton.ClickAsync();
    await CreateFormCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
}

/// <summary>
/// Fills the workspace name field
/// </summary>
public async Task FillNameAsync(string name)
{
    await CreateNameInput.FillAsync(name);
}

/// <summary>
/// Fills the workspace description field
/// </summary>
public async Task FillDescriptionAsync(string description)
{
    await CreateDescriptionInput.FillAsync(description);
}

/// <summary>
/// Clicks the create button and waits for API call
/// </summary>
public async Task SubmitCreateFormAsync()
{
    await WaitForApi(async () =>
    {
        await CreateButton.ClickAsync();
    }, CreateTenantApiRegex);
}

#endregion

#region Common Workflows (Happy Paths)

/// <summary>
/// Creates a new workspace with the given name and optional description
/// </summary>
/// <remarks>
/// This is a convenience method for the common "happy path" workflow.
/// For testing edge cases or validation, use the individual action methods.
/// </remarks>
public async Task CreateWorkspaceAsync(string name, string? description = null)
{
    await ClickCreateWorkspaceButtonAsync();
    await FillNameAsync(name);
    if (!string.IsNullOrEmpty(description))
    {
        await FillDescriptionAsync(description);
    }
    await SubmitCreateFormAsync();
}

/// <summary>
/// Updates a workspace with new values
/// </summary>
public async Task UpdateWorkspaceAsync(string originalName, string newName, string? newDescription = null)
{
    await StartEditAsync(originalName);
    await FillEditNameAsync(newName);
    if (newDescription != null)
    {
        await FillEditDescriptionAsync(newDescription);
    }
    await SubmitEditFormAsync();
}

#endregion
```

**Step Definitions (Choose Appropriate Level):**
```csharp
// Simple scenario - use high-level method
protected async Task WhenICreateANewWorkspaceCalledFor(string name, string description)
{
    var workspaceName = AddTestPrefix(name);
    var workspacesPage = GetOrCreateWorkspacesPage();
    await workspacesPage.NavigateAsync();
    await workspacesPage.CreateWorkspaceAsync(workspaceName, description);

    _objectStore.Add(KEY_LAST_CREATED_WORKSPACE, workspaceName);
    _objectStore.Add(KEY_CURRENT_WORKSPACE, workspaceName);
}

// Complex scenario - use low-level methods
protected async Task WhenITryToCreateWorkspaceButCancelBeforeSubmit()
{
    var workspaceName = AddTestPrefix("Cancelled Workspace");
    var workspacesPage = GetOrCreateWorkspacesPage();
    await workspacesPage.NavigateAsync();

    // Fine-grained control
    await workspacesPage.ClickCreateWorkspaceButtonAsync();
    await workspacesPage.FillNameAsync(workspaceName);
    await workspacesPage.FillDescriptionAsync("This will be cancelled");

    // Screenshot for debugging
    await SaveScreenshotAsync("before-cancel");

    // Cancel instead of submit
    await workspacesPage.CreateCancelButton.ClickAsync();
}

// Validation scenario - use low-level methods
protected async Task WhenITryToCreateWorkspaceWithDuplicateName(string existingName)
{
    var workspacesPage = GetOrCreateWorkspacesPage();
    await workspacesPage.NavigateAsync();
    await workspacesPage.ClickCreateWorkspaceButtonAsync();
    await workspacesPage.FillNameAsync(AddTestPrefix(existingName)); // Duplicate!
    await workspacesPage.SubmitCreateFormAsync();

    // Don't update object store - creation should fail
}
```

**Characteristics:**
- ✅ **Best of both worlds** - Simple for common cases, flexible for edge cases
- ✅ **Progressive enhancement** - Start simple, add primitives as needed
- ✅ **Clear intent** - Method names indicate level of abstraction
- ✅ **Easy to extend** - Add new workflows without breaking existing ones
- ⚠️ **More methods** in page model (but organized in regions)

---

## Comparison Table

| Aspect | Thick (Current) | Thin | Hybrid |
|--------|-----------------|------|--------|
| **Step simplicity** | ⭐⭐⭐ Very simple | ⭐ More verbose | ⭐⭐⭐ Simple by default |
| **Flexibility** | ⭐ Limited | ⭐⭐⭐ Very flexible | ⭐⭐⭐ Very flexible |
| **Edge case testing** | ⭐ Need new methods | ⭐⭐⭐ Easy | ⭐⭐⭐ Easy |
| **Page model size** | ⭐⭐ Medium (450 lines) | ⭐⭐⭐ Small | ⭐ Larger (but organized) |
| **Code duplication** | ⭐⭐⭐ Minimal | ⭐ Potential in steps | ⭐⭐⭐ Minimal |
| **Maintenance** | ⭐⭐ Workflows can break | ⭐⭐⭐ Isolated changes | ⭐⭐⭐ Isolated + reusable |
| **Debugging** | ⭐ Black box workflows | ⭐⭐⭐ Clear steps | ⭐⭐⭐ Clear steps |

---

## Real-World Example: Complex Scenario

### Scenario: User tries to edit workspace but loses connection mid-way

**With Thick Model (Hard):**
```csharp
// Would need to add new method to WorkspacesPage
public async Task StartEditButSimulateNetworkFailureAsync(...)
{
    // Mix of UI and test infrastructure concerns
}
```

**With Thin Model (Natural):**
```csharp
protected async Task WhenIEditWorkspaceButLoseConnection()
{
    var workspacesPage = GetOrCreateWorkspacesPage();
    await workspacesPage.ClickEditButtonAsync(workspaceName);
    await workspacesPage.FillEditNameAsync("New Name");

    // Test-specific logic
    await Page.Context.SetOfflineAsync(true);

    await workspacesPage.ClickUpdateButtonAsync();
    // Expect error...
}
```

**With Hybrid Model (Best):**
```csharp
protected async Task WhenIEditWorkspaceButLoseConnection()
{
    var workspacesPage = GetOrCreateWorkspacesPage();

    // Use primitive for fine control
    await workspacesPage.StartEditAsync(workspaceName);
    await workspacesPage.FillEditNameAsync("New Name");

    // Test-specific logic
    await Page.Context.SetOfflineAsync(true);

    await workspacesPage.SubmitEditFormAsync();
    // Expect error...
}
```

---

## Impact on Your Current Tests

### Tests That Work Well with Current Approach

From [`Steps/WorkspaceTenancySteps.cs`](../Steps/WorkspaceTenancySteps.cs):

✅ **Simple CRUD operations** - Current approach is excellent
```csharp
await workspacesPage.CreateWorkspaceAsync(workspaceName, description);
await workspacesPage.UpdateWorkspaceAsync(oldName, newName);
await workspacesPage.DeleteWorkspaceAsync(workspaceName);
```

### Tests That Would Benefit from Primitives

❌ **Permission/validation testing** - Would be easier with primitives
- Testing that Viewer can't edit (need to check button state)
- Testing form validation (need to submit without filling all fields)
- Testing cancel workflows (need to stop mid-flow)

❌ **Debugging scenarios** - Would benefit from step-by-step control
- Taking screenshots between steps
- Checking intermediate states
- Simulating slow networks or interruptions

---

## Recommendation

Based on analysis of your codebase, I recommend **Option C: Hybrid Approach**:

### Why Hybrid Works Best for Your Project

1. **You already have 80% of it** - Keep existing workflows
2. **Add primitives incrementally** - Only when testing edge cases
3. **Matches your test patterns** - Mix of happy-path and edge-case testing
4. **Minimal refactoring** - Doesn't break existing tests

### Implementation Strategy

1. **Keep all existing high-level methods** (`CreateWorkspaceAsync`, `UpdateWorkspaceAsync`, etc.)
2. **Extract primitives from existing methods** (just refactor internals)
3. **Make primitives public** so steps can use them
4. **Organize with regions** (`#region Single Actions`, `#region Common Workflows`)
5. **Document which to use when** (XML comments)

### Example Refactoring of One Method

**Before (Thick):**
```csharp
public async Task CreateWorkspaceAsync(string name, string? description = null)
{
    await OpenCreateFormAsync();
    await CreateNameInput.FillAsync(name);
    if (!string.IsNullOrEmpty(description))
    {
        await CreateDescriptionInput.FillAsync(description);
    }
    await ClickCreateButtonAsync();
}
```

**After (Hybrid):**
```csharp
#region Single Actions

public async Task OpenCreateFormAsync()
{
    await CreateWorkspaceButton.ClickAsync();
    await CreateFormCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
}

public async Task FillCreateNameAsync(string name)
{
    await CreateNameInput.FillAsync(name);
}

public async Task FillCreateDescriptionAsync(string description)
{
    await CreateDescriptionInput.FillAsync(description);
}

private async Task ClickCreateButtonAsync()
{
    await WaitForApi(async () => await CreateButton.ClickAsync(), CreateTenantApiRegex);
}

#endregion

#region Common Workflows

/// <summary>
/// Creates a new workspace with the given name and optional description
/// </summary>
/// <remarks>
/// High-level workflow method. For fine-grained control, use OpenCreateFormAsync,
/// FillCreateNameAsync, FillCreateDescriptionAsync, and ClickCreateButtonAsync separately.
/// </remarks>
public async Task CreateWorkspaceAsync(string name, string? description = null)
{
    await OpenCreateFormAsync();
    await FillCreateNameAsync(name);
    if (!string.IsNullOrEmpty(description))
    {
        await FillCreateDescriptionAsync(description);
    }
    await ClickCreateButtonAsync();
}

#endregion
```

**Impact:** Zero breaking changes, added flexibility!

---

## Next Steps

If you choose **Hybrid Approach**:

1. Identify your 5 most complex workflows
2. Extract primitives from each (make internal methods public)
3. Add XML documentation explaining when to use each level
4. Organize with `#region` tags for clarity
5. Update any edge-case tests to use primitives

**Files to refactor:**
- [`Pages/WorkspacesPage.cs`](../Pages/WorkspacesPage.cs) (~450 lines)
- [`Pages/TransactionsPage.cs`](../Pages/TransactionsPage.cs) (~594 lines)

**Estimated effort:** 2-4 hours per page model

---

## Questions to Consider

1. **How often do you test edge cases vs happy paths?**
   - More edge cases → Hybrid or Thin
   - Mostly happy paths → Thick is fine

2. **How often do tests break when UI changes?**
   - Often → Thin (easier to locate issues)
   - Rarely → Thick is fine

3. **Do you need to insert test-specific logic mid-workflow?**
   - Yes → Hybrid or Thin
   - No → Thick is fine

4. **Are new team members confused by page models?**
   - Yes → Thin (clearer what UI does)
   - No → Keep current approach

5. **Do you take screenshots or check state mid-workflow?**
   - Yes → Hybrid or Thin
   - No → Thick is fine

Based on your project having both simple CRUD tests AND complex permission/role tests, **Hybrid seems ideal**.
