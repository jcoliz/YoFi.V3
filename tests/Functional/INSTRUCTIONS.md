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

## Mapping Gherkin Steps to C# Method Calls

Each step in the feature file must be mapped to a corresponding method in the base class:

1. **Locate the method**: Find the method in `@baseclass` file whose XML comment matches the step text pattern
2. **Extract parameters**: Identify quoted strings (`"text"`) or placeholder values (`<variable>`) in the step text
3. **Generate method call**: Call the method with extracted parameters

### Step Mapping Examples

| Gherkin Step | Base Class Method | Generated Call |
|--------------|-------------------|----------------|
| `Given user has launched site` | `GivenLaunchedSite()` | `await GivenLaunchedSite();` |
| `When user launches site` | `WhenUserLaunchesSite()` | `await WhenUserLaunchesSite();` |
| `When user selects option "Weather" in nav bar` | `SelectOptionInNavbar(string option)` | `await SelectOptionInNavbar("Weather");` |
| `Then page loaded ok` | `ThenPageLoadedOk()` | `await ThenPageLoadedOk();` |
| `Then page heading is "Home"` | `PageHeadingIs(string text)` | `await PageHeadingIs("Home");` |
| `Then page contains 5 forecasts` | `WeatherPageDisplaysForecasts(int expectedCount)` | `await WeatherPageDisplaysForecasts(5);` |

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
- If the hook is on the Feature, apply it to all scenarios
- If the hook is on a specific Scenario, apply it only to that scenario

## Test Attribute Handling

- Add `[Test]` attribute to scenarios without Examples
- Add `[TestCase(...)]` attributes for each row in Examples table
- Add `[Explicit]` attribute if `@explicit` tag is present on the scenario
