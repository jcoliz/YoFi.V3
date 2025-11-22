# Instructions to convert feature files to C# test files

This file provides detailed instructions to generate C# files out of Gherkin feature files.
They are intended for GitHub Copilot, but you can use them manually if you like!

## Steps

1. For each Feature file (ending in `*.feature`) in the [Features](./Features/) folder, we will create one Test file written in C# into the [Tests](./Tests/) folder.
2. The name of the Test file is the name of the Feature file, with the extension `.cs` appended.
3. To generate the C# Test file, follow the template as indicated by the `@template` tag. This file is a mustache file located in the [Features](./Features/) folder.
4. For each step in the Feature file, you can find the corresponding method to call from the `@baseclass` located in the [Steps](./Steps/) folder.
5. If you see a `@hook:before-first-then` notation on a feature, or individual scenario, this describes a special step to call before the first `Then` step in the resulting Test. Treat this as a special `Step`, with these properties: `{ "Keyword": "Hook", "Text": "Before first Then Step", "Args": "", "Method": "<method-from-tag>" }`.

## Mapping Feature Tags to Template Variables

- `@using` → `{{Using}}` (namespace for using statement)
- `@namespace` → `{{Namespace}}` (namespace for the test class)
- `@baseclass` → `{{BaseClass}}` (base class name)
- `@template` → Specifies which mustache template file to use
- `@hook:before-first-then` → Inserts a hook call before the first `Then` step

## Mapping Feature Content to Template Variables

- Feature name (without parenthetical prefix) → `{{FeatureName}}` (used in class name)
- Feature description (full text after "Feature:") → `{{FeatureDescription}}` (used in summary comment)
- Feature description lines (if multi-line) → `{{Description}}` array (used in remarks)
- Scenario name → `{{Name}}` (used in test method summary)
- Scenario name (camelCase, no spaces) → `{{Method}}` (used in test method name)

## Background Steps Handling

When a feature file contains a `Background:` section:

1. **Create Setup Method**: Generate a `[SetUp]` method called `SetupAsync()` that contains all Background steps
2. **Method Signature**: `[SetUp] public async Task SetupAsync()`
3. **Step Processing**: Process Background steps using the same step mapping rules as scenarios
4. **Execution Order**: Background steps execute before each individual test scenario

### Background Example

**Gherkin:**
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

## Data Table Handling

When a step is followed by a table (indicated by `|` characters), generate a `DataTable` object:

1. **Identify Table Steps**: Steps followed by lines starting with `|` contain data tables
2. **Create DataTable**: Generate code to create and populate a `DataTable` object from `YoFi.V3.Tests.Functional.Helpers`
3. **Pass to Method**: Pass the `DataTable` as a parameter to the step method
4. **Table Structure**: Each `| Field | Value |` row becomes `table.AddRow("Field", "Value")`

### Data Table Examples

**Gherkin:**
```gherkin
When I enter valid registration details:
    | Field            | Value                    |
    | Email            | newuser@example.com      |
    | Username         | newuser                 |
    | Password         | SecurePassword123!       |
    | Confirm Password | SecurePassword123!       |
```

**Generated C#:**
```csharp
// When I enter valid registration details:
var table = new DataTable();
table.AddRow("Email", "newuser@example.com");
table.AddRow("Username", "newuser");
table.AddRow("Password", "SecurePassword123!");
table.AddRow("Confirm Password", "SecurePassword123!");
await WhenIEnterValidRegistrationDetails(table);
```

**Another Example:**
```gherkin
Then I should see my account information:
    | Field    | Value                |
    | Email    | testuser@example.com |
    | Username | testuser             |
```

**Generated C#:**
```csharp
// Then I should see my account information:
var table = new DataTable();
table.AddRow("Email", "testuser@example.com");
table.AddRow("Username", "testuser");
await ThenIShouldSeeMyAccountInformation(table);
```

## Mapping Gherkin Steps to C# Method Calls

Each step in the feature file must be mapped to a corresponding method in the base class:

1. **Locate the method BY XML COMMENT**: Find the method in `@baseclass` file whose XML summary comment (e.g., `/// <summary>Given: I am on the home page</summary>`) matches the step text pattern. The XML comment format is `{Keyword}: {step text}`.
   - **CRITICAL**: The XML comment is the authoritative pattern matcher, NOT the method name
   - **CRITICAL**: Match against the XML comment text, which may use regex patterns like `(.+)` for capturing groups
   - The method name may differ from the step text (e.g., XML comment says "containing (.+)" but method is named `ThenIShouldSeeAnErrorMessage`)
   - When creating new step methods, ensure the XML comment matches the Gherkin step pattern exactly
2. **Handle `And` keyword correctly**: When a scenario step uses the `And` keyword, interpret this as the most recent non-`And` keyword encountered. For example:
   - If a `Then` step is followed by `And` steps, interpret those `And` steps as `Then` steps when searching for matching methods
   - If a `Given` step is followed by `And` steps, interpret those `And` steps as `Given` steps when searching for matching methods
   - When searching the base class, look for methods with XML comments like `/// <summary>Then: {step text}</summary>` even though the original step says `And`
3. **Use exact method names**: Use the exact method name as it appears in the `@baseclass` file. Do NOT modify or infer method names.
4. **Extract parameters**: Identify quoted strings (`"text"`) or placeholder values (`<variable>`) in the step text, matching capture groups in the XML comment pattern
5. **Handle data tables**: If the step is followed by a table, create a `DataTable` object and pass it as a parameter
6. **Generate method call**: Call the method with extracted parameters (including DataTable if present)
7. **Add step comments**: Before each method call, add a comment with the original step text (keeping `And` as written in the feature file)
8. **Add blank lines**: Add a blank line after each step's method call for readability
9. **Notify missing base class methods**: If the method cannot be found in the `@baseclass`, add a comment to point this out

### Step Mapping Examples

| Gherkin Step | XML Comment in Base Class | Base Class Method | Generated Code |
|--------------|---------------------------|-------------------|----------------|
| `Given user has launched site` | `/// <summary>Given: user has launched site</summary>` | `GivenLaunchedSite()` | `// Given user has launched site`<br>`await GivenLaunchedSite();`<br>(blank line) |
| `When user launches site` | `/// <summary>When: user launches site</summary>` | `WhenUserLaunchesSite()` | `// When user launches site`<br>`await WhenUserLaunchesSite();`<br>(blank line) |
| `When user selects option "Weather" in nav bar` | `/// <summary>When: user selects option {option} in nav bar</summary>` | `SelectOptionInNavbar(string option)` | `// When user selects option "Weather" in nav bar`<br>`await SelectOptionInNavbar("Weather");`<br>(blank line) |
| `Then page loaded ok` | `/// <summary>Then: page loaded ok</summary>` | `ThenPageLoadedOk()` | `// Then page loaded ok`<br>`await ThenPageLoadedOk();`<br>(blank line) |
| `And page heading is "Home"` (after a `Then` step) | `/// <summary>Then: page heading is {text}</summary>` | `PageHeadingIs(string text)` | `// And page heading is "Home"`<br>`await PageHeadingIs("Home");`<br>(blank line) |
| `Then I should see an error message containing "Invalid"` | `/// <summary>Then: I should see an error message containing (.+)</summary>` | `ThenIShouldSeeAnErrorMessage(string errorMessage)` | `// Then I should see an error message containing "Invalid"`<br>`await ThenIShouldSeeAnErrorMessage("Invalid");`<br>(blank line) |

**Note**: In the last example, the XML comment uses the pattern "containing (.+)" while the method name is `ThenIShouldSeeAnErrorMessage`. Always match based on the XML comment pattern, not the method name.

### Handling Scenario Outlines

- `Examples:` table rows become `[TestCase(...)]` attributes
- Placeholders like `<page>` become method parameters
- Each column in the Examples table maps to a test method parameter
- The test method signature includes parameters: `public async Task MethodName(string page)`

## Hook Handling Details

The `@hook:before-first-then` tag specifies a method to call before the first `Then` step:

- Parse the tag to extract the method name after the colon (e.g., `SaveScreenshot` from `@hook:before-first-then:SaveScreenshot`)
- Insert a step call `await {MethodName}Async();` immediately before the first step with keyword `Then`
- The hook appears in the generated code with comment: `// Hook Before first Then Step`
- Add a blank line after the hook call
- If the hook is on the Feature, apply it to all scenarios
- If the hook is on a specific Scenario, apply it only to that scenario

## Test Attribute Handling

- Add `[Test]` attribute to scenarios without Examples
- Add `[TestCase(...)]` attributes for each row in Examples table
- Add `[Explicit]` attribute if `@explicit` tag is present on the scenario
- All test methods should be `async Task` since all step methods are async

## Required Using Statements

Generated test files should include:
- `using YoFi.V3.Tests.Functional.Helpers;` (for DataTable class)
- Any additional using statements specified by `@using` tag
