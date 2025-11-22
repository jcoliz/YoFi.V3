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

### Background Steps

When a feature file contains a `Background:` section:

1. Create a `[SetUp]` method named `SetupAsync()`
2. Signature: `[SetUp] public async Task SetupAsync()`
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
public async Task SetupAsync()
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

**CRITICAL RULE**: Match steps to methods by their XML summary comment, NOT by method name.

1. **Find Method by XML Comment**: Search for methods whose XML `<summary>` matches the step pattern
   - XML comment format: `/// <summary>{Keyword}: {step pattern}</summary>`
   - The pattern may include regex like `(.+)` or placeholders like `{text}`
   - Example: `/// <summary>Then: I should see an error message containing (.+)</summary>`

2. **Handle `And` Keyword**: Interpret `And` as the most recent non-`And` keyword
   - After `Then`, interpret `And` as `Then` when searching for methods
   - After `Given`, interpret `And` as `Given` when searching for methods

3. **Extract Parameters**: Identify quoted strings (`"text"`) or placeholders matching capture groups

4. **Generate Method Call**:
   - Add comment with original step text (preserve `And` keyword)
   - Call the method with extracted parameters
   - Add blank line after each step

5. **Missing Methods**: If no matching method exists in base class, add a comment noting this

**Mapping Examples:**

| Gherkin Step | XML Comment | Method Name | Generated Code |
|--------------|-------------|-------------|----------------|
| `Given user has launched site` | `/// <summary>Given: user has launched site</summary>` | `GivenLaunchedSite()` | `// Given user has launched site`<br>`await GivenLaunchedSite();` |
| `When user selects option "Weather" in nav bar` | `/// <summary>When: user selects option {option} in nav bar</summary>` | `SelectOptionInNavbar(string option)` | `// When user selects option "Weather" in nav bar`<br>`await SelectOptionInNavbar("Weather");` |
| `And page heading is "Home"` (after `Then`) | `/// <summary>Then: page heading is {text}</summary>` | `PageHeadingIs(string text)` | `// And page heading is "Home"`<br>`await PageHeadingIs("Home");` |
| `Then I should see an error message containing "Invalid"` | `/// <summary>Then: I should see an error message containing (.+)</summary>` | `ThenIShouldSeeAnErrorMessage(string errorMessage)` | `// Then I should see an error message containing "Invalid"`<br>`await ThenIShouldSeeAnErrorMessage("Invalid");` |

**Note**: In the last example, the XML comment pattern is "containing (.+)" while the method is `ThenIShouldSeeAnErrorMessage`. Always match by XML comment, not method name.

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

### XML Comment Pattern Matching (CRITICAL)

1. **XML Comments Are Authoritative**: The XML summary comment is the authoritative pattern matcher for Gherkin steps, NOT the method name.
   - ✅ CORRECT: Match against `/// <summary>Then: I should see an error message containing (.+)</summary>`
   - ❌ WRONG: Assume method must be named `ThenIShouldSeeAnErrorMessageContaining`

2. **Exact Matching Required**: The XML comment must match the Gherkin step text exactly, including all words and adverbs.
   - ✅ CORRECT: `When: I try to navigate directly to the login page` matches Gherkin step exactly
   - ❌ WRONG: Assuming `When: I try to navigate to the login page` matches when Gherkin says "directly"
   - **Why**: Words like "directly" may indicate different behavior (e.g., bypass redirects, force navigation)
   - If similar steps exist with slightly different wording, create a new step method with the exact pattern

3. **Method Names May Differ**: Method names can be shorter or different from the full XML comment pattern.
   - Example: XML comment says "containing (.+)" but method is `ThenIShouldSeeAnErrorMessage(string errorMessage)`
   - The regex pattern `(.+)` in the XML comment captures the parameter

4. **Creating New Step Methods**:
   - Write XML comment to match the **exact** Gherkin step pattern word-for-word
   - Include all adverbs, adjectives, and qualifiers from the Gherkin step
   - Use regex patterns like `(.+)`, `{text}`, `{value}` for parameters only
   - Method name should be descriptive but doesn't need to include every word if clear from context

5. **Finding Existing Steps**:
   - Always search for methods by their XML comment pattern first
   - Use `list_code_definition_names` or `search_files` to find methods by XML comment
   - Don't create duplicate methods just because the name doesn't match exactly
   - **Do** create a new method if the XML comment doesn't match the Gherkin step exactly

### Examples

```csharp
// ✅ CORRECT: XML comment matches Gherkin, method name is concise
/// <summary>
/// Then: I should see an error message containing (.+)
/// </summary>
protected async Task ThenIShouldSeeAnErrorMessage(string errorMessage)
{
    // Implementation
}

// ❌ WRONG: Unnecessarily verbose method name
/// <summary>
/// Then: I should see an error message containing (.+)
/// </summary>
protected async Task ThenIShouldSeeAnErrorMessageContaining(string errorMessage)
{
    // This creates confusion and is redundant
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
