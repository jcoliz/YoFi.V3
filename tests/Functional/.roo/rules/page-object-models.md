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

3. **ARIA roles with accessible names** - ACCEPTABLE
   - ✅ Use: `Page!.GetByRole(AriaRole.Button, new() { Name = "Submit" })`
   - Semantic HTML, supports accessibility
   - Only when ID or test ID is not practical

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

2. **Class-based selectors** - `Locator(".css-class")`
   - Breaks when styling changes
   - Classes are for styling, not testing
   - **Solution**: Add `data-test-id` to the element instead

3. **XPath selectors** - `Locator("//div[@class='foo']/span[2]")`
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

## See Also

- [`functional-tests.md`](functional-tests.md) - Gherkin to C# conversion rules
- [`tests/Functional/Pages/README.md`](../../Pages/README.md) - Page Object Model principles
- [`tests/Functional/Components/README.md`](../../Components/README.md) - Component Object Model principles
