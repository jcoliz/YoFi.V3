# Functional Tests Rules

## Important: Step Confirmation vs Test Implementation

When asked to "confirm steps exist and create missing steps":
- **DO**: Check if step methods exist in the `Steps/` directory
- **DO**: Create any missing step method implementations
- **DO NOT**: Generate or add test methods to `*.feature.cs` files in `Tests/` directory
- **ONLY** generate test implementations when explicitly asked to "implement the test" or "generate the test"

This distinction is important:
- Step confirmation/creation = Work in `Steps/` directory only
- Test implementation = Work in `Tests/` directory (generate from feature file)

## Generating Test Implementations from Feature Files

When generating C# test files from Gherkin feature files (`.feature` → `.feature.cs`):

### File Generation Process

1. **One-to-One Mapping**: For each `*.feature` file in [`Features/`](../Features/), create one `*.feature.cs` file in [`Tests/`](../Tests/)
2. **Template-Based Generation**: Use the mustache template specified by the `@template` tag in the feature file
3. **Base Class Reference**: Find step methods in the class specified by `@baseclass` tag (located in [`Steps/`](../Steps/))

### Feature Tags to Template Variables

Map feature file tags to mustache template variables:

- `@using` → `{{Using}}` - Namespace for using statement
- `@namespace` → `{{Namespace}}` - Namespace for the test class
- `@baseclass` → `{{BaseClass}}` - Base class name
- `@template` → Specifies which mustache template file to use
- `@hook:before-first-then` → Inserts a hook call before the first `Then` step

### Feature Content to Template Variables

Map feature file content to template variables:

- Feature name (without parenthetical prefix) → `{{FeatureName}}` - Class name
- Feature description (full text after "Feature:") → `{{FeatureDescription}}` - Summary comment
- Feature description lines (if multi-line) → `{{Description}}` array - Remarks
- Scenario name → `{{Name}}` - Test method summary
- Scenario name (camelCase, no spaces) → `{{Method}}` - Test method name
- Rule name → `{{Name}}` - Region name for organizing scenarios
- Rule description → `{{Description}}` - Comment explaining the rule's purpose

### Rule Organization

When a feature file contains `Rule:` sections:

1. **Region Structure**: Each Rule becomes a `#region` in the generated test file
2. **Region Name**: `#region Rule: {{RuleName}}`
3. **Rule Description**: Added as a comment after the region start: `// {{RuleDescription}}`
4. **Scenario Grouping**: All scenarios under a Rule are placed within that Rule's region
5. **Region End**: Each Rule's region ends with `#endregion`

**Example from [`Authentication.feature`](../Features/Authentication.feature:15-47):**
```gherkin
Rule: User Registration
    Users can create new accounts with valid credentials

    Scenario: User registers for a new account
        Given I am on the registration page
        When I enter valid registration details
        And I submit the registration form
        Then My registration request should be acknowledged
```

**Generated C# in [`Authentication.feature.cs`](../Tests/Authentication.feature.cs:26-49):**
```csharp
#region Rule: User Registration
// Users can create new accounts with valid credentials

/// <summary>
/// User registers for a new account
/// </summary>
[Test]
public async Task UserRegistersForANewAccount()
{
    // Given I am on the registration page
    await GivenIAmOnTheRegistrationPage();

    // When I enter valid registration details
    await WhenIEnterValidRegistrationDetails();

    // And I submit the registration form
    await WhenISubmitTheRegistrationForm();

    // Then My registration request should be acknowledged
    await ThenMyRegistrationRequestShouldBeAcknowledged();
}

#endregion
```

**Benefits:**
- Logical grouping of related test scenarios
- Collapsible regions for easier navigation in IDE
- Business context preserved through rule descriptions
- Generated code structure mirrors Gherkin organization

### Background Steps

When a feature file contains a `Background:` section:

1. Create a `[SetUp]` method named `Background()`
2. Signature: `[SetUp] public async Task Background()`
3. Process Background steps using the same step mapping rules as scenarios
4. Background steps execute before each test scenario

**Example:**
```gherkin
Background:
    Given the application is running
    And I am not logged in
```

**Generated C#:**
```csharp
[SetUp]
public async Task Background()
{
    // Given the application is running
    await GivenTheApplicationIsRunning();

    // And I am not logged in
    await GivenIAmNotLoggedIn();
}
```

### Data Table Handling

When a step is followed by a table (lines starting with `|`):

1. **Create DataTable**: Generate code to create and populate a `DataTable` from `YoFi.V3.Tests.Functional.Helpers`
2. **Pass as Parameter**: Pass the `DataTable` as a parameter to the step method
3. **Row Structure**: Each `| Field | Value |` row becomes `table.AddRow("Field", "Value")`

**Example:**
```gherkin
When I enter valid registration details:
    | Field            | Value                    |
    | Email            | newuser@example.com      |
    | Password         | SecurePassword123!       |
```

**Generated C#:**
```csharp
// When I enter valid registration details:
var table = new DataTable();
table.AddRow("Email", "newuser@example.com");
table.AddRow("Password", "SecurePassword123!");
await WhenIEnterValidRegistrationDetails(table);
```

### Mapping Gherkin Steps to C# Methods

**CRITICAL RULE**: Match steps to methods by their custom step attribute, NOT by method name or XML comments.

1. **Find Method by Step Attribute**: Search for methods whose `[Given]`, `[When]`, or `[Then]` attribute matches the step pattern
   - Attribute format: `[Given("step pattern")]`, `[When("step pattern")]`, `[Then("step pattern")]`
   - The pattern may include regex like `(.+)` or placeholders like `{text}`
   - Example: `[Then("I should see an error message containing {errorMessage}")]`
   - Methods can have multiple attributes for different step variations

2. **Handle `And` Keyword**: Interpret `And` as the most recent non-`And` keyword
   - After `Then`, interpret `And` as `Then` when searching for methods
   - After `Given`, interpret `And` as `Given` when searching for methods

3. **Extract Parameters**: Identify quoted strings (`"text"`) or placeholders matching capture groups or `{name}` placeholders

4. **Generate Method Call**:
   - Add comment with original step text (preserve `And` keyword)
   - Call the method with extracted parameters
   - Add blank line after each step

5. **Missing Methods**: If no matching method exists in base class, add a comment noting this

**Mapping Examples:**

| Gherkin Step | Step Attribute | Method Name | Generated Code |
|--------------|----------------|-------------|----------------|
| `Given user has launched site` | `[Given("user has launched site")]` | `GivenLaunchedSite()` | `// Given user has launched site`<br>`await GivenLaunchedSite();` |
| `When user selects option "Weather" in nav bar` | `[When("user selects option {option} in nav bar")]` | `SelectOptionInNavbar(string option)` | `// When user selects option "Weather" in nav bar`<br>`await SelectOptionInNavbar("Weather");` |
| `And page heading is "Home"` (after `Then`) | `[Then("page heading is {text}")]` | `PageHeadingIs(string text)` | `// And page heading is "Home"`<br>`await PageHeadingIs("Home");` |
| `Then I should see an error message containing "Invalid"` | `[Then("I should see an error message containing {errorMessage}")]` | `ThenIShouldSeeAnErrorMessage(string errorMessage)` | `// Then I should see an error message containing "Invalid"`<br>`await ThenIShouldSeeAnErrorMessage("Invalid");` |

**Note**: In the last example, the attribute pattern includes `{errorMessage}` placeholder while the method is `ThenIShouldSeeAnErrorMessage`. Always match by step attribute, not method name.

### Scenario Outlines

When handling `Scenario Outline:` and `Examples:`:

- Each row in `Examples:` table becomes a `[TestCase(...)]` attribute
- Placeholders like `<page>` become method parameters
- Test method signature includes parameters: `public async Task MethodName(string page)`

### Hook Handling

The `@hook:before-first-then` tag specifies a method to call before the first `Then` step:

- Extract method name after colon (e.g., `SaveScreenshot` from `@hook:before-first-then:SaveScreenshot`)
- Insert `await {MethodName}Async();` before first `Then` step
- Add comment: `// Hook Before first Then Step`
- If on Feature, apply to all scenarios; if on Scenario, apply only to that scenario

### Test Attributes

- `[Test]` for scenarios without Examples
- `[TestCase(...)]` for each Examples row
- `[Explicit]` if `@explicit` tag is present
- All test methods should be `async Task`

### Required Using Statements

Include in generated files:
- `using YoFi.V3.Tests.Functional.Helpers;` (for DataTable)
- Any additional using statements from `@using` tag

## Gherkin Step Implementation

When implementing or updating step methods in the `Steps/` directory:

### Step Attribute Pattern Matching (CRITICAL)

1. **Step Attributes Are Authoritative - Method Names Are Irrelevant**: The step attribute (`[Given]`, `[When]`, `[Then]`) is the ONLY thing that matters for matching Gherkin steps to methods. Method names are for code readability only.
   - ✅ CORRECT: Match steps ONLY by attribute: `[Then("I should see an error message containing {errorMessage}")]`
   - ❌ WRONG: Assume method must be named `ThenIShouldSeeAnErrorMessageContaining`
   - **There is ABSOLUTELY NO expectation or requirement that method names match step patterns**
   - Method names should be clear and concise for developers, NOT mirror the Gherkin text
   - This separation allows refactoring method names without breaking step matching

2. **Multiple Step Patterns in One Method**: A single method can match multiple Gherkin step patterns by including multiple step attributes (using `AllowMultiple = true`).
   - Use this to consolidate duplicate/alias methods into a single implementation
   - Each attribute represents a different step pattern that maps to this method

   **Example:**
   ```csharp
   /// <summary>
   /// Establishes that the user owns a workspace.
   /// </summary>
   [Given("I have a workspace called {workspaceName}")]
   [Given("I own a workspace called {workspaceName}")]
   [Given("I own {workspaceName}")]
   protected async Task GivenIHaveAWorkspaceCalled(string workspaceName)
   {
       // Single implementation handles all three step variations
   }
   ```

   **Benefits:**
   - Reduces code duplication when steps have identical behavior
   - Keeps related step variations together
   - Makes refactoring easier (change implementation once, affects all patterns)

   **When to use:**
   - Steps are semantically identical (e.g., "I own X" = "I have X" for Owner role)
   - Steps are just shortened versions (e.g., "I own X" is shorthand for "I have a workspace called X")
   - Different phrasings of the same action

   **When NOT to use:**
   - Steps have different semantics even if implementation is similar
   - Steps might diverge in behavior later
   - Clarity would be compromised

3. **Exact Matching Required**: Each attribute pattern must match its Gherkin step text exactly, including all words and adverbs.
   - ✅ CORRECT: `[When("I try to navigate directly to the login page")]` matches Gherkin step exactly
   - ❌ WRONG: Assuming `[When("I try to navigate to the login page")]` matches when Gherkin says "directly"
   - **Why**: Words like "directly" may indicate different behavior (e.g., bypass redirects, force navigation)
   - If similar steps exist with slightly different wording, create a new step method with the exact pattern

4. **Method Names Should Be Clear, Not Literal**: Method names should be descriptive for code maintainability, NOT verbatim copies of Gherkin steps.
   - **Good**: `[When("I try to navigate directly to the login page")]` → Method `WhenITryToNavigateToLoginPage()`
   - **Bad**: `[When("I try to navigate directly to the login page")]` → Method `WhenITryToNavigateDirectlyToTheLoginPage()`
   - **Good**: `[Then("I should see an error message containing {errorMessage}")]` → Method `ThenIShouldSeeAnErrorMessage(string message)`
   - **Bad**: `[Then("I should see an error message containing {errorMessage}")]` → Method `ThenIShouldSeeAnErrorMessageContaining(string message)`
   - Keep method names concise - omit words like "directly", "containing", "exactly" if they don't add clarity
   - The attribute pattern captures the exact step text; the method name serves code readability

5. **Creating New Step Methods**:
   - Add the appropriate step attribute (`[Given]`, `[When]`, or `[Then]`) with a pattern matching the **exact** Gherkin step text word-for-word
   - Include all adverbs, adjectives, and qualifiers from the Gherkin step
   - Use placeholders like `{text}`, `{value}`, `{parameterName}` for parameters
   - For steps with DataTable parameters, add a trailing colon `:` to the pattern
   - Method name should be descriptive but doesn't need to include every word if clear from context
   - XML comments remain for human-readable documentation

6. **Finding Existing Steps**:
   - Always search for methods by their step attribute pattern first
   - Use `search_files` to find methods by attribute pattern: `\[Given\("pattern"\)\]`
   - Don't create duplicate methods just because the name doesn't match exactly
   - **Do** create a new method if no attribute matches the Gherkin step exactly
   - Use [`scripts/Analyze-StepPatterns.ps1`](../../scripts/Analyze-StepPatterns.ps1) to verify no duplicate patterns exist

### Examples

```csharp
// ✅ CORRECT: Attribute pattern matches Gherkin, method name is concise, XML comment for documentation
/// <summary>
/// Verifies that an error message containing specific text is displayed.
/// </summary>
/// <param name="errorMessage">The expected error message text (or substring).</param>
[Then("I should see an error message containing {errorMessage}")]
protected async Task ThenIShouldSeeAnErrorMessage(string errorMessage)
{
    // Implementation
}

// ❌ WRONG: Unnecessarily verbose method name (attribute pattern is fine)
/// <summary>
/// Verifies that an error message containing specific text is displayed.
/// </summary>
[Then("I should see an error message containing {errorMessage}")]
protected async Task ThenIShouldSeeAnErrorMessageContaining(string errorMessage)
{
    // This creates confusion and is redundant
}

// ✅ CORRECT: Multiple attributes for step variations
/// <summary>
/// Establishes that the user owns a workspace with the specified name.
/// </summary>
[Given("{username} owns a workspace called {workspaceName}")]
[Given("{username} owns {workspaceName}")]
protected async Task GivenUserOwnsAWorkspaceCalled(string username, string workspaceName)
{
    // Single implementation handles both patterns
}

// ✅ CORRECT: DataTable step with trailing colon
/// <summary>
/// Sets up multiple workspace access assignments from a table.
/// </summary>
[Given("{username} has access to these workspaces:")]
protected async Task GivenUserHasAccessToTheseWorkspaces(string username, DataTable workspacesTable)
{
    // Process table data
}
```

### Step Method Organization

- Place GIVEN steps in `#region Steps: GIVEN`
- Place WHEN steps in `#region Steps: WHEN`
- Place THEN steps in `#region Steps: THEN`
- Keep helper methods in `#region Helpers`

### Parameter Handling

- Use DataTable for tabular data from Gherkin
- Extract quoted strings as separate parameters
- Support both parameterless and parameterized overloads when needed

## See Also

- [INSTRUCTIONS.md](../INSTRUCTIONS.md) - Full instructions for converting Gherkin to C#
- [README.md](../README.md) - Functional test principles and getting started
