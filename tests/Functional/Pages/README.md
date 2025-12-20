# Page Object Models (POMs)

Here we keep one class for each of the major pages on the client.

## Exclusive Use of Locators

The page objects are the only places where locators can be generated.
This allows us to have a single place where changes have to be kept up
when the page changes.

## Single Locator

Within the page view, there should only be a single place where a locator
definition needs to change, e.g. if a data-test-id changes on the client,
it only has to be changed on **one line** in the POM.

## All waiting

Because waiting on load conditions is often quite page-specific, and changes
based on design, all waiting is done in POMs.

## Hybrid Model Pattern

Page object models in this project use a **Hybrid Approach** that provides both high-level workflow methods and low-level primitive actions. This gives test authors the flexibility to use simple one-line calls for happy-path scenarios or compose fine-grained steps for complex edge cases.

### Organization

Page methods are organized into regions by operation type:

```csharp
#region Create Operations - Single Actions
// Primitive actions for fine-grained control
#endregion

#region Create Operations - Common Workflows
// High-level happy-path methods
#endregion
```

### Method Types

**Single Action Methods** - Low-level primitives that perform one specific action:
- [`OpenCreateFormAsync()`](WorkspacesPage.cs:154-158) - Opens a form/modal
- [`FillCreateNameAsync(string)`](WorkspacesPage.cs:167-170) - Fills one field
- [`SubmitCreateFormAsync()`](WorkspacesPage.cs:191-197) - Submits with API wait
- [`CancelCreateAsync()`](WorkspacesPage.cs:205-209) - Cancels the operation

**Workflow Methods** - High-level methods that combine primitives for common scenarios:
- [`CreateWorkspaceAsync(string, string?)`](WorkspacesPage.cs:226-235) - Complete create flow
- [`UpdateTransactionAsync(string, string, string, decimal)`](TransactionsPage.cs:434-443) - Complete update flow
- [`DeleteWorkspaceAsync(string)`](WorkspacesPage.cs:465-469) - Complete delete flow

### When to Use High-Level Workflows

✅ **Use** for standard happy-path scenarios:

```csharp
// Creating a workspace normally
await workspacesPage.CreateWorkspaceAsync("My Workspace", "Description");

// Updating a transaction normally
await transactionsPage.UpdateTransactionAsync("Old Payee", date, "New Payee", amount);
```

### When to Use Single Actions

✅ **Use** when you need:
- To test form validation
- To cancel mid-flow
- To take screenshots between steps
- To check intermediate states
- To simulate errors (network, permissions)
- To test complex edge cases

**Examples:**

```csharp
// Testing cancellation
await workspacesPage.OpenCreateFormAsync();
await workspacesPage.FillCreateNameAsync("Test");
await workspacesPage.CancelCreateAsync();

// Testing validation
await transactionsPage.OpenCreateModalAsync();
await transactionsPage.FillCreateDateAsync("invalid-date");
await transactionsPage.SubmitCreateFormAsync();
// Expect error

// Taking screenshots between steps
await transactionsPage.OpenCreateModalAsync();
await SaveScreenshotAsync("modal-opened");
await transactionsPage.FillCreateDateAsync(date);
await transactionsPage.FillCreatePayeeAsync(payee);
await SaveScreenshotAsync("form-filled");
await transactionsPage.SubmitCreateFormAsync();
await SaveScreenshotAsync("transaction-created");

// Simulating network failure mid-flow
await workspacesPage.StartEditAsync(workspaceName);
await workspacesPage.FillEditNameAsync("New Name");
await Page.Context.SetOfflineAsync(true);
await workspacesPage.SubmitEditFormAsync();
// Expect error handling
```

### Benefits

**For Simple Happy-Path Tests:**
- One-line method calls keep tests concise
- No need to know implementation details

**For Complex Scenarios:**
- Full control over test flow
- Easy to insert debugging logic
- Can test intermediate states
- Enables validation testing without full submission

**For Maintenance:**
- Single actions are easier to update than complex workflows
- Clear documentation of method purpose and usage
- Better organization with regions separating concerns

### Documentation Standards

Every method includes XML documentation with:
- **Summary**: What the method does
- **Parameters**: Description of each parameter
- **Remarks**:
  - Whether it's a single action or workflow method
  - When to use it vs alternatives
  - Dependencies (e.g., "Must be called after OpenEditModalAsync")

### Examples from Production Code

See [`WorkspacesPage.cs`](WorkspacesPage.cs) and [`TransactionsPage.cs`](TransactionsPage.cs) for complete implementations following this pattern.
