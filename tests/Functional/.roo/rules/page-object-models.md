# Page Object Model Rules

## Locator Strategy

Page Object Models MUST use reliable, maintainable selectors that are resistant to UI changes.

### Preferred Selector Priority (in order)

1. **`data-test-id` attributes** - MOST PREFERRED
   - ✅ Use: `Page!.GetByTestId("element-identifier")`
   - Explicitly marks elements for testing
   - Immune to styling and text changes
   - Clear intent for test automation

2. **Element IDs** - SECOND CHOICE
   - ✅ Use: `Page!.Locator("#element-id")`
   - Stable and unique identifiers
   - Only when `data-test-id` is not available

3. **ARIA roles WITHOUT names** - ACCEPTABLE FOR STRUCTURAL ELEMENTS
   - ⚠️ Use sparingly: `Page!.GetByRole(AriaRole.Navigation)`
   - Only for unique structural elements (navigation, main, banner, etc.)
   - Must be truly unique on the page
   - **DO NOT** use with `Name` parameter

4. **Element tags** - LAST RESORT
   - ⚠️ Use sparingly: `Page!.Locator("button")`
   - Only for structural elements where specificity isn't needed
   - Must be combined with other selectors for uniqueness

### Prohibited Selectors

❌ **NEVER use these selectors:**

1. **Text-based selectors** - `GetByText("Some text")`
   - Breaks when text changes (translations, copy updates)
   - Fragile and maintenance-intensive
   - **Solution**: Add `data-test-id` to the element instead

2. **ARIA roles WITH accessible names** - `GetByRole(AriaRole.Button, new() { Name = "Submit" })`
   - **Just as bad as text-based selectors!**
   - Breaks when button text changes (translations, copy updates)
   - The `Name` parameter matches against text content
   - **Solution**: Add `data-test-id` to the element instead
   - **Exception**: ARIA roles WITHOUT names are acceptable for structural elements

3. **Class-based selectors** - `Locator(".css-class")`
   - Breaks when styling changes
   - Classes are for styling, not testing
   - **Solution**: Add `data-test-id` to the element instead

4. **XPath selectors** - `Locator("//div[@class='foo']/span[2]")`
   - Extremely fragile
   - Difficult to read and maintain
   - **Solution**: Add `data-test-id` to the target element

## Adding data-test-id Attributes

When a Page Object Model needs to locate an element that doesn't have a `data-test-id`:

1. **Add the attribute to the Vue component** - Modify the source component to include `data-test-id`
2. **Use descriptive names** - Follow kebab-case: `data-test-id="submit-button"`
3. **Document in page model** - XML comment should explain what the element does

### Example: Before and After

**Before (BAD):**
```csharp
// ❌ Text-based selector - fragile!
public ILocator SubmitButton => Page!.GetByText("Submit");
```

**After (GOOD):**

*Vue component:*
```vue
<button
  class="btn btn-primary"
  data-test-id="submit-button"
  @click="handleSubmit"
>
  Submit
</button>
```

*Page Object Model:*
```csharp
/// <summary>
/// Submit button for the form
/// </summary>
public ILocator SubmitButton => Page!.GetByTestId("submit-button");
```

## Component Composition

Page Object Models should expose child components rather than duplicating their locators.

### Pattern

When a page contains a reusable component (like `ErrorDisplay` or `WorkspaceSelector`):

1. **Create a component page object** in `tests/Functional/Components/`
2. **Expose it as a property** in the page object model
3. **Use the component's methods** rather than reimplementing locators

### Example

**Component Model (`Components/ErrorDisplay.cs`):**
```csharp
public class ErrorDisplay(IPage page, ILocator parent)
{
    public ILocator Root => parent.GetByTestId("error-display");
    public ILocator Title => Root.GetByTestId("title-display");
    public ILocator Detail => Root.GetByTestId("detail-display");

    public async Task<string?> GetTitleAsync() => await Title.TextContentAsync();
}
```

**Page Model:**
```csharp
public class TransactionsPage(IPage page) : BasePage(page)
{
    /// <summary>
    /// Error display component for page-level errors
    /// </summary>
    public ErrorDisplay ErrorDisplay => new ErrorDisplay(Page!, Page!.Locator("body"));

    // Don't duplicate ErrorDisplay locators here!
}
```

**Usage in Test:**
```csharp
var page = new TransactionsPage(_page);
var errorTitle = await page.ErrorDisplay.GetTitleAsync();
Assert.That(errorTitle, Is.EqualTo("Validation Error"));
```

## Locator Documentation

All locator properties MUST have XML documentation describing:

1. **What the element is** - Its purpose or function
2. **When it's visible** - Any conditional visibility
3. **Special behaviors** - If it requires interaction to appear

### Examples

```csharp
/// <summary>
/// Submit button in the create form
/// </summary>
public ILocator CreateButton => Page!.GetByTestId("create-submit");

/// <summary>
/// Error message displayed when validation fails
/// Only visible when there are validation errors
/// </summary>
public ILocator ValidationError => Page!.GetByTestId("validation-error");

/// <summary>
/// Dropdown menu panel
/// Appears after clicking the menu trigger
/// </summary>
public ILocator MenuPanel => Page!.GetByTestId("dropdown-menu");
```

## Method Documentation

All page object methods MUST have XML documentation with:

1. **Summary** - What the method does
2. **Parameters** (if any) - Description of each parameter
3. **Returns** (if applicable) - What the method returns
4. **Remarks** (if needed) - Important behavior notes

### Example

```csharp
/// <summary>
/// Creates a new workspace with the given name and description
/// </summary>
/// <param name="name">The workspace name</param>
/// <param name="description">Optional workspace description</param>
/// <remarks>
/// This method waits for the page to reload after creation
/// </remarks>
public async Task CreateWorkspaceAsync(string name, string? description = null)
{
    await OpenCreateFormAsync();
    await CreateNameInput.FillAsync(name);
    if (!string.IsNullOrEmpty(description))
    {
        await CreateDescriptionInput.FillAsync(description);
    }
    await CreateButton.ClickAsync();
    await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
}
```

## Naming Conventions

### Locator Properties

- Use PascalCase
- Be descriptive and specific
- Include element type if ambiguous
- Examples: `SubmitButton`, `EmailInput`, `ErrorMessage`, `WorkspaceCard`

### Methods

- Use PascalCase with `Async` suffix for async methods
- Start with verb describing the action
- Be specific about what the method does
- Examples: `CreateWorkspaceAsync()`, `DeleteTransactionAsync()`, `GetErrorMessageAsync()`

### data-test-id Attributes

- Use kebab-case
- Be descriptive and unique within the component
- Include context if needed for uniqueness
- Examples: `submit-button`, `email-input`, `error-message`, `workspace-card-123`

## Organization

### File Structure

```
tests/Functional/
├── Components/          # Reusable component page objects
│   ├── ErrorDisplay.cs
│   ├── WorkspaceSelector.cs
│   └── LoginState.cs
├── Pages/              # Page-level page objects
│   ├── BasePage.cs     # Base class for all pages
│   ├── WorkspacesPage.cs
│   └── TransactionsPage.cs
└── Steps/              # Gherkin step implementations (uses page objects)
```

### Within a Page Object Class

1. Constructor / Primary constructor
2. Component properties (e.g., `ErrorDisplay`, `WorkspaceSelector`)
3. Locator properties (grouped by section if page has multiple sections)
4. Navigation methods
5. Action methods (CRUD operations)
6. Query methods (getters, checks)
7. Helper methods (if needed)

### Example Structure

```csharp
public class WorkspacesPage(IPage page) : BasePage(page)
{
    // Components
    public WorkspaceSelector WorkspaceSelector => new WorkspaceSelector(Page!, Page!.Locator("body"));
    public ErrorDisplay ErrorDisplay => new ErrorDisplay(Page!, Page!.Locator("body"));

    // Header Locators
    public ILocator PageHeading => Page!.GetByRole(AriaRole.Heading, new() { Name = "Workspace Management" });
    public ILocator CreateButton => Page!.GetByRole(AriaRole.Button, new() { Name = "Create Workspace" });

    // Form Locators
    public ILocator CreateFormCard => Page!.GetByTestId("create-form-card");
    public ILocator CreateNameInput => Page!.Locator("#create-name");

    // Navigation
    public async Task NavigateAsync() { /* ... */ }

    // Actions
    public async Task CreateWorkspaceAsync(string name, string? description = null) { /* ... */ }
    public async Task DeleteWorkspaceAsync(string workspaceName) { /* ... */ }

    // Queries
    public async Task<bool> HasWorkspaceAsync(string workspaceName) { /* ... */ }
    public async Task<int> GetWorkspaceCountAsync() { /* ... */ }
}
```

## BasePage Pattern

All page objects should inherit from `BasePage` which provides:

- Common page functionality (navigation, screenshots)
- Shared components (SiteHeader)
- Helper methods (WaitForApi, WaitUntilLoaded)

### Example

```csharp
public class MyPage(IPage page) : BasePage(page)
{
    // Page-specific implementation
}
```

## Test Synchronization with Vue.js

### NEVER Use NetworkIdle Waits

❌ **PROHIBITED**: `await Page.WaitForLoadStateAsync(LoadState.NetworkIdle)`

**Why**: NetworkIdle waits are unreliable with modern reactive frameworks like Vue.js:
- Network activity may complete before Vue finishes rendering DOM updates
- Vue batches DOM updates asynchronously (nextTick)
- NetworkIdle doesn't account for client-side state updates
- Creates intermittent test failures and increases test execution time

### WaitForPageReadyAsync Pattern

✅ **REQUIRED**: Every page object MUST implement `WaitForPageReadyAsync()` that waits for key visible elements.

```csharp
/// <summary>
/// Waits for the page to be ready by ensuring key elements are visible
/// </summary>
public async Task WaitForPageReadyAsync(float timeout = 5000)
{
    await KeyElement.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
}
```

**When to use:**
- After navigation (in `NavigateAsync()` methods)
- After clicking navigation links/buttons
- In component methods that navigate to other pages
- In THEN steps that verify redirection

### Wait for Created/Updated Elements

✅ **REQUIRED**: After create/update operations, explicitly wait for the created/updated DOM element.

```csharp
/// <summary>
/// Waits for a specific transaction to appear in the list
/// </summary>
public async Task WaitForTransactionAsync(string payeeName, float timeout = 5000)
{
    var row = GetTransactionRow(payeeName);
    await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
}

// Usage after update
await transactionsPage.SubmitEditFormAsync();
await transactionsPage.WaitForTransactionAsync(updatedPayeeName);
```

**When to use:**
- After creating a new entity (workspace, transaction, etc.)
- After updating an existing entity
- Before assertions that check for the entity's presence

### Handling Redirects

✅ **REQUIRED**: When navigation may trigger a redirect, wait for the destination page to be ready.

```csharp
protected async Task WhenITryToNavigateDirectlyToAProtectedPageLike(string page)
{
    // Navigate directly - should redirect to login page for anonymous users
    await Page.GotoAsync(page);

    // Wait for redirect to complete by waiting for login page to be ready
    var loginPage = GetOrCreateLoginPage();
    await loginPage.WaitForPageReadyAsync();
}
```

### Avoid Redundant Waits

❌ **BAD**: Waiting for the same element multiple times
```csharp
var homePage = new HomePage(Page);
await homePage.WaitForPageReadyAsync();  // Waits for BrochureSection
await homePage.BrochureSection.WaitForAsync(...);  // ❌ Redundant!
```

✅ **GOOD**: Wait once, then use
```csharp
var homePage = new HomePage(Page);
await homePage.WaitForPageReadyAsync();  // Waits for BrochureSection
Assert.That(await homePage.BrochureSection.IsVisibleAsync(), Is.True);
```

### Component Navigation Pattern

✅ **REQUIRED**: Navigation components should call destination page's `WaitForPageReadyAsync()`.

```csharp
/// <summary>
/// Clicks the Sign In menu item and waits for login page to be ready
/// </summary>
public async Task ClickSignInAsync()
{
    await SignInMenuItem.ClickAsync();
    var loginPage = new LoginPage(_page);
    await loginPage.WaitForPageReadyAsync();
}
```

**DO NOT** use NetworkIdle in components - let the destination page define its ready state.

### Best Practices Summary

1. **Never use NetworkIdle** - Use DOM-based waits instead
2. **Every page defines WaitForPageReadyAsync()** - Wait for key visible elements
3. **Wait for created/updated elements** - After CRUD operations
4. **Handle redirects explicitly** - Wait for destination page to be ready
5. **Avoid redundant waits** - Don't wait for the same element twice
6. **Components delegate to pages** - Let pages define their ready state
7. **Prefer specific over generic waits** - Wait for the exact element being tested

These patterns ensure reliable test execution with Vue.js's asynchronous rendering while maintaining optimal test performance.

## See Also

- [`functional-tests.md`](functional-tests.md) - Gherkin to C# conversion rules
- [`tests/Functional/Pages/README.md`](../../Pages/README.md) - Page Object Model principles
- [`tests/Functional/Components/README.md`](../../Components/README.md) - Component Object Model principles
