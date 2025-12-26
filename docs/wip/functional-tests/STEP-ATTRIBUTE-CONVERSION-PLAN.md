---
status: Draft
created: 2025-12-26
related_docs:
  - tests/Functional/INSTRUCTIONS.md
  - tests/Functional/Steps/README.md
---

# Step Attribute Conversion Plan

## Overview

Convert all functional test step definitions from XML comment-based pattern matching to custom attribute-based pattern matching. This improves semantic clarity, separates documentation from metadata, and provides a cleaner API for step discovery during test generation.

## Current State

Step methods currently use XML `<summary>` comments for pattern matching:

```csharp
/// <summary>
/// Given: I have an existing account with email {email}
/// </summary>
protected async Task GivenIHaveAnExistingAccountWithEmail(string email)
{
    // Implementation
}
```

**Issues with current approach:**
- XML comments are meant for human documentation, not machine-readable metadata
- Pattern matching logic must parse XML strings
- No separation between documentation (what it does) and metadata (how to match it)
- Cannot easily support multiple patterns per method

## Target State

Step methods will use custom attributes for pattern matching, with XML comments for documentation:

```csharp
/// <summary>
/// Verifies an existing user account exists for testing login scenarios.
/// </summary>
/// <param name="email">The email address of the existing account.</param>
[Given("I have an existing account with email {email}")]
protected async Task GivenIHaveAnExistingAccountWithEmail(string email)
{
    // Implementation
}
```

**Benefits:**
- ✅ **Semantic clarity** - Attributes are metadata, XML comments are documentation
- ✅ **Cleaner parsing** - Attributes have consistent C# syntax
- ✅ **Better IDE support** - Attributes show in tooltips, IntelliSense
- ✅ **Separation of concerns** - Documentation vs. pattern matching
- ✅ **Multiple patterns** - Can add multiple attributes per method
- ✅ **Familiar pattern** - Similar to SpecFlow/Cucumber (without runtime overhead)

## Architecture

### Custom Attribute Classes

Create three custom attribute classes in `tests/Functional/Attributes/`:

```csharp
namespace YoFi.V3.Tests.Functional.Attributes;

/// <summary>
/// Marks a step method as implementing a Given step with the specified pattern.
/// </summary>
/// <param name="pattern">
/// The Gherkin pattern to match, with placeholders in {curly} braces.
/// Example: "I have an existing account with email {email}"
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class GivenAttribute(string pattern) : Attribute
{
    public string Pattern { get; } = pattern;
}

/// <summary>
/// Marks a step method as implementing a When step with the specified pattern.
/// </summary>
/// <param name="pattern">
/// The Gherkin pattern to match, with placeholders in {curly} braces.
/// Example: "I click the {buttonName} button"
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class WhenAttribute(string pattern) : Attribute
{
    public string Pattern { get; } = pattern;
}

/// <summary>
/// Marks a step method as implementing a Then step with the specified pattern.
/// </summary>
/// <param name="pattern">
/// The Gherkin pattern to match, with placeholders in {curly} braces.
/// Example: "I should see {expectedText} on the page"
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ThenAttribute(string pattern) : Attribute
{
    public string Pattern { get; } = pattern;
}
```

**Key Design Decisions:**
- **AllowMultiple = true**: Allows a single method to handle multiple patterns (e.g., "I delete {name}" and "I remove {name}")
- **Primary constructor**: Modern C# 12 syntax for concise attribute definition
- **Pattern property**: Provides read access to the pattern for reflection-based discovery

### Pattern Matching Syntax

Patterns use placeholders in `{curly braces}` to indicate parameters:

| Pattern | Matches | Parameters |
|---------|---------|------------|
| `"I am logged in"` | `Given I am logged in` | None |
| `"I am logged in as {username}"` | `Given I am logged in as alice` | `username = "alice"` |
| `"I should see {count} transactions"` | `Then I should see 5 transactions` | `count = "5"` |
| `"{username} owns a workspace called {workspaceName}"` | `Given alice owns a workspace called Budget` | `username = "alice"`, `workspaceName = "Budget"` |

**Pattern Matching Rules:**
1. Placeholders match any text until the next literal text or end of string
2. Parameter names in `{brackets}` should match method parameter names
3. Patterns are case-sensitive (match Gherkin text exactly)
4. Whitespace is significant

### File Organization

```
tests/Functional/
├── Attributes/                    # NEW
│   ├── GivenAttribute.cs
│   ├── WhenAttribute.cs
│   └── ThenAttribute.cs
├── Steps/
│   ├── Common/
│   │   ├── CommonGivenSteps.cs   # MODIFY - Add attributes
│   │   ├── CommonWhenSteps.cs    # MODIFY - Add attributes
│   │   └── CommonThenSteps.cs    # MODIFY - Add attributes
│   ├── AuthenticationSteps.cs    # MODIFY - Add attributes
│   ├── WeatherSteps.cs           # MODIFY - Add attributes
│   ├── WorkspaceTenancySteps.cs  # MODIFY - Add attributes
│   └── TransactionRecordSteps.cs # MODIFY - Add attributes
└── INSTRUCTIONS.md               # MODIFY - Update to reference attributes
```

## Implementation Plan

### Phase 1: Create Attribute Classes
**Deliverable:** New attribute classes ready for use

1. Create `tests/Functional/Attributes/` directory
2. Create [`GivenAttribute.cs`](tests/Functional/Attributes/GivenAttribute.cs)
3. Create [`WhenAttribute.cs`](tests/Functional/Attributes/WhenAttribute.cs)
4. Create [`ThenAttribute.cs`](tests/Functional/Attributes/ThenAttribute.cs)
5. Add comprehensive XML documentation to attribute classes

### Phase 2: Convert Common Steps
**Deliverable:** Common step classes use attributes

Convert in dependency order (base → derived):

1. **[`CommonGivenSteps.cs`](tests/Functional/Steps/Common/CommonGivenSteps.cs)** (~7 step methods)
   - Add `using YoFi.V3.Tests.Functional.Attributes;`
   - Add attributes to all Given step methods
   - Enhance XML comments to describe what (not pattern matching)

2. **[`CommonWhenSteps.cs`](tests/Functional/Steps/Common/CommonWhenSteps.cs)** (~4 step methods)
   - Add attributes to all When step methods
   - Convert XML comments to proper documentation

3. **[`CommonThenSteps.cs`](tests/Functional/Steps/Common/CommonThenSteps.cs)** (~6 step methods)
   - Add attributes to all Then step methods
   - Update XML documentation

### Phase 3: Convert Feature-Specific Steps
**Deliverable:** All step classes use attributes

Convert feature step classes (can be done in parallel):

1. **[`AuthenticationSteps.cs`](tests/Functional/Steps/AuthenticationSteps.cs)** (~50 step methods)
   - Add attributes to ~15 Given steps
   - Add attributes to ~13 When steps
   - Add attributes to ~22 Then steps

2. **[`WeatherSteps.cs`](tests/Functional/Steps/WeatherSteps.cs)** (~9 step methods)
   - Add attributes to ~2 Given steps
   - Add attributes to ~1 When step
   - Add attributes to ~6 Then steps

3. **[`WorkspaceTenancySteps.cs`](tests/Functional/Steps/WorkspaceTenancySteps.cs)** (~70 step methods)
   - Add attributes to ~9 Given steps
   - Add attributes to ~14 When steps
   - Add attributes to ~47 Then steps

4. **[`TransactionRecordSteps.cs`](tests/Functional/Steps/TransactionRecordSteps.cs)** (~12 step methods)
   - Add attributes to ~2 Given steps
   - Add attributes to ~6 When steps
   - Add attributes to ~4 Then steps

### Phase 4: Update Documentation
**Deliverable:** Documentation reflects new attribute-based approach

1. Update [`tests/Functional/INSTRUCTIONS.md`](tests/Functional/INSTRUCTIONS.md)
   - Change "Locate the method BY XML COMMENT" to "Locate the method BY ATTRIBUTE"
   - Update pattern matching examples to show attributes
   - Keep XML comment references for human documentation only

2. Update [`tests/Functional/Steps/README.md`](tests/Functional/Steps/README.md)
   - Update "Writing New Steps" section to show attribute pattern
   - Add section on "Pattern Matching with Attributes"

### Phase 5: Validation
**Deliverable:** Confirmed all patterns work correctly

1. **Verify uniqueness** - Ensure no duplicate patterns across all step methods
2. **Verify coverage** - Ensure all Gherkin steps in feature files have matching attributes
3. **Verify generation** - Test that Roo can correctly generate tests using attributes
4. **Manual review** - Spot-check generated tests to verify correctness

## Pattern Examples by Step Type

### Given Steps - Common Patterns

```csharp
[Given("the application is running")]
[Given("I am not logged in")]
[Given("I have an existing account")]
[Given("I am on the login page")]
[Given("I am logged in")]
```

### Given Steps - Parameterized

```csharp
[Given("I am logged in as {username}")]
[Given("{username} owns a workspace called {workspaceName}")]
[Given("{username} owns {workspaceName}")]
[Given("{username} has access to {workspaceName}")]
[Given("{username} can edit data in {workspaceName}")]
[Given("{workspaceName} contains {transactionCount} transactions")]
```

### Given Steps - With DataTables

```csharp
[Given("these users exist")]
[Given("{username} has access to these workspaces")]
[Given("there are other workspaces in the system")]
[Given("I have a workspace with a transaction:")]
```

### When Steps - Common Patterns

```csharp
[When("User launches site")]
[When("I enter my credentials")]
[When("I click the login button")]
[When("I navigate to my profile page")]
[When("I click the logout button")]
```

### When Steps - Parameterized

```csharp
[When("user visits the {option} page")]
[When("I create a new workspace called {name} for {description}")]
[When("I rename it to {newName}")]
[When("I update the memo to {newMemo}")]
[When("I view transactions in {workspaceName}")]
```

### Then Steps - Common Patterns

```csharp
[Then("page loaded ok")]
[Then("I should be successfully logged in")]
[Then("I should be logged out")]
[Then("I should see the home page")]
[Then("the transaction should be saved successfully")]
```

### Then Steps - Parameterized

```csharp
[Then("page title contains {text}")]
[Then("page heading is {text}")]
[Then("I should see {workspaceName} in my workspace list")]
[Then("I should have {expectedCount} workspaces available")]
[Then("the memo should be updated to {expectedMemo}")]
```

## Special Cases to Handle

### 1. Methods with Multiple Patterns

Some steps may have aliases (e.g., "I delete" vs "I remove"):

```csharp
/// <summary>
/// Removes a workspace from the current user's workspace list.
/// </summary>
[When("I delete {workspaceName}")]
[When("I remove {workspaceName}")]
protected async Task WhenIDelete(string workspaceName)
{
    // Implementation
}
```

### 2. Regex-Style Patterns (Current State)

Some XML comments currently use regex patterns like `(.+)`:

```xml
/// <summary>
/// Then: I should see an error message containing (.+)
/// </summary>
```

**Convert to simple placeholder:**
```csharp
[Then("I should see an error message containing {errorMessage}")]
```

### 3. Method Name vs Pattern Mismatch

Some methods have names that don't match their pattern:

```csharp
/// <summary>
/// Then: page heading is {text}
/// </summary>
protected async Task PageHeadingIs(string text)
```

**Keep method name unchanged, just add attribute:**
```csharp
[Then("page heading is {text}")]
protected async Task PageHeadingIs(string text)
```

### 4. Abstract Methods in Base Classes

Some common step methods are declared abstract in base classes:

```csharp
protected abstract Task WhenIEnterMyCredentials();
```

**Add attribute to abstract declaration:**
```csharp
[When("I enter my credentials")]
protected abstract Task WhenIEnterMyCredentials();
```

### 5. Override Methods

When a derived class overrides a base method, the attribute is NOT inherited:

```csharp
// Base class
[When("I enter my credentials")]
protected virtual async Task WhenIEnterMyCredentials() { ... }

// Derived class - Attribute is NOT inherited automatically
protected override async Task WhenIEnterMyCredentials() { ... }
```

**For overridden methods:**
- If the pattern is the same, attribute is inherited (no action needed)
- If the pattern differs, add the new pattern to the override

## Consolidation Analysis

During conversion, we'll identify and consolidate pass-through methods:

### Pattern to Find

```csharp
// Wrapper method - CANDIDATE FOR REMOVAL
protected async Task MethodA()
{
    await MethodB(); // Only calls one other method
}
```

### Consolidation Process

1. **Identify wrapper:** Find methods that only call one other method
2. **Extract pattern:** Get the Gherkin pattern from wrapper's XML comment
3. **Add to target:** Add pattern as additional attribute on target method
4. **Remove wrapper:** Delete the wrapper method
5. **Update tests:** Generated tests will automatically use the consolidated method

### Examples from Current Code

**Example 1: Application Running**

**Before:**
```csharp
// In CommonGivenSteps.cs
protected async Task GivenLaunchedSite()
{
    await WhenUserLaunchesSite();
    await ThenPageLoadedOk();
}

protected async Task GivenTheApplicationIsRunning()
{
    await GivenLaunchedSite(); // Pass-through
}
```

**After:**
```csharp
// In CommonGivenSteps.cs
[Given("has user launched site")]
[Given("the application is running")]
protected async Task GivenLaunchedSite()
{
    await WhenUserLaunchesSite();
    await ThenPageLoadedOk();
}

// GivenTheApplicationIsRunning() method removed
```

**Example 2: Home Page Navigation**

**Before:**
```csharp
// In WeatherSteps.cs
protected async Task GivenIAmOnTheHomePage()
{
    await GivenLaunchedSite(); // Pass-through
}
```

**After:**
```csharp
// In CommonGivenSteps.cs - add additional pattern
[Given("has user launched site")]
[Given("the application is running")]
[Given("I am on the home page")]
protected async Task GivenLaunchedSite()
{
    await WhenUserLaunchesSite();
    await ThenPageLoadedOk();
}

// GivenIAmOnTheHomePage() removed from WeatherSteps.cs
```

### Consolidation Guidelines

**When to consolidate:**
- ✅ Method only calls one other method
- ✅ No additional logic or setup
- ✅ Parameters match exactly (or no parameters)
- ✅ Same return type

**When NOT to consolidate:**
- ❌ Method has additional logic before/after the call
- ❌ Method calls multiple other methods in sequence
- ❌ Method transforms parameters
- ❌ Method is abstract/virtual (may be overridden)

### Impact on Generated Tests

**Before consolidation:**
```csharp
// Generated test
[Test]
public async Task WeatherForecastDisplayTest()
{
    // Given I am on the home page
    await GivenIAmOnTheHomePage(); // Calls wrapper

    // When I navigate to view the weather forecast
    await WhenINavigateToViewTheWeatherForecast();

    // Then I should see upcoming weather predictions
    await ThenIShouldSeeUpcomingWeatherPredictions();
}
```

**After consolidation:**
```csharp
// Generated test - automatically uses consolidated method
[Test]
public async Task WeatherForecastDisplayTest()
{
    // Given I am on the home page
    await GivenLaunchedSite(); // Calls consolidated method directly

    // When I navigate to view the weather forecast
    await WhenINavigateToViewTheWeatherForecast();

    // Then I should see upcoming weather predictions
    await ThenIShouldSeeUpcomingWeatherPredictions();
}
```

**No breaking changes:** Pattern matching finds the right method via attribute.

## Migration Strategy

### Conversion Process per File

For each step class file:

1. **Add using statement** at top:
   ```csharp
   using YoFi.V3.Tests.Functional.Attributes;
   ```

2. **For each step method:**
   - Extract the Gherkin pattern from XML `<summary>` comment
   - Remove the "Given:", "When:", or "Then:" prefix
   - Convert regex patterns like `(.+)` to `{parameterName}`
   - Add appropriate attribute above the method
   - Rewrite XML `<summary>` to describe WHAT the method does (for humans)
   - Keep `<param>` and `<remarks>` tags as-is

3. **Example transformation:**

   **Before:**
   ```csharp
   /// <summary>
   /// Given: I have an existing account with email {email}
   /// </summary>
   protected async Task GivenIHaveAnExistingAccountWithEmail(string email)
   {
       // TODO: Implement account creation via API or database setup
       // For now, assume test accounts exist
       await Task.CompletedTask;
   }
   ```

   **After:**
   ```csharp
   /// <summary>
   /// Verifies an existing user account exists with the specified email address
   /// for testing login scenarios.
   /// </summary>
   /// <param name="email">The email address of the existing account.</param>
   [Given("I have an existing account with email {email}")]
   protected async Task GivenIHaveAnExistingAccountWithEmail(string email)
   {
       // TODO: Implement account creation via API or database setup
       // For now, assume test accounts exist
       await Task.CompletedTask;
   }
   ```

### Pattern Extraction Rules

| XML Comment Pattern | Attribute Pattern |
|---------------------|-------------------|
| `Given: I am logged in` | `[Given("I am logged in")]` |
| `Given: I am logged in as {username}` | `[Given("I am logged in as {username}")]` |
| `When: user visits the (\S+) page` | `[When("user visits the {page} page")]` |
| `Then: I should see (.+) in my list` | `[Then("I should see {item} in my list")]` |
| `Then: page title contains (\S+)` | `[Then("page title contains {text}")]` |

**Pattern Extraction Algorithm:**
1. Remove the `Given:`, `When:`, or `Then:` prefix
2. Replace regex patterns `(\S+)` or `(.+)` with `{parameterName}`
3. Parameter name should match method parameter name
4. Keep all other text exactly as-is (whitespace, punctuation)

## Testing Strategy

### Unit Testing Attributes (Optional)

While not strictly necessary, we could create simple tests to verify attributes are correctly applied:

```csharp
[Test]
public void AllStepMethodsHaveAttributes()
{
    var stepClasses = new[]
    {
        typeof(CommonGivenSteps),
        typeof(CommonWhenSteps),
        typeof(CommonThenSteps),
        typeof(AuthenticationSteps),
        typeof(WeatherSteps),
        typeof(WorkspaceTenancySteps),
        typeof(TransactionRecordSteps)
    };

    foreach (var stepClass in stepClasses)
    {
        var methods = stepClass.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Protected)
            .Where(m => m.Name.StartsWith("Given") || m.Name.StartsWith("When") || m.Name.StartsWith("Then"));

        foreach (var method in methods)
        {
            var hasAttribute =
                method.GetCustomAttribute<GivenAttribute>() != null ||
                method.GetCustomAttribute<WhenAttribute>() != null ||
                method.GetCustomAttribute<ThenAttribute>() != null;

            Assert.That(hasAttribute, Is.True,
                $"Method {stepClass.Name}.{method.Name} should have a step attribute");
        }
    }
}
```

### Integration Testing

After conversion:
1. Generate a test file from a feature file using Roo
2. Verify the generated test compiles
3. Run the generated test to verify it executes correctly
4. Spot-check that correct step methods are being called

### Validation Checklist

- [ ] All step methods have appropriate attributes
- [ ] No duplicate patterns exist (same pattern on multiple methods)
- [ ] All patterns correctly match their method parameters
- [ ] XML documentation describes WHAT, not pattern matching
- [ ] Pass-through methods identified and consolidated
- [ ] Consolidated methods have all relevant patterns as attributes
- [ ] Removed methods no longer referenced in code (except in generated tests)
- [ ] Test generation still works correctly
- [ ] Generated tests compile without errors
- [ ] Generated tests execute successfully

## Rollback Plan

If issues arise during conversion:

1. **Partial rollback:** Git revert specific commit(s) for problematic files
2. **Full rollback:** Git revert all commits related to this conversion
3. **Hybrid approach:** Use both XML comments and attributes temporarily during transition

Since Roo reads source files as text, both XML comments and attributes can coexist. Could implement a transitional phase where Roo checks for attributes first, falls back to XML comments if not found.

## Related Documentation Updates

After implementation, update:

1. **[`.roorules`](.roorules)** - Add pattern about step attributes
2. **Project README** - Mention step attribute pattern if relevant
3. **Testing documentation** - Update any references to step pattern matching

## Open Questions

### Q1: Should we keep XML comments with patterns for backward compatibility?

**Decision: No, remove pattern from XML comments**

Rationale:
- Cleaner separation of concerns
- Avoids confusion about which is authoritative
- Forces proper documentation in XML comments

### Q2: Should we support regex patterns in attributes?

**Decision: No, use simple placeholder syntax**

Rationale:
- Simpler to parse and understand
- Regex is overkill for our use cases
- Placeholder syntax `{name}` is more readable

### Q3: Should we validate attribute patterns at compile time?

**Decision: Not initially, but could add analyzer later**

Rationale:
- Would require custom Roslyn analyzer
- Manual validation during conversion is sufficient
- Could add in future if needed

## Success Criteria

This conversion is successful when:

1. ✅ All step methods have appropriate attributes (Given/When/Then)
2. ✅ All XML comments describe WHAT methods do (not pattern matching)
3. ✅ Test generation works correctly with new attribute-based approach
4. ✅ All functional tests pass after conversion
5. ✅ Documentation updated to reflect new pattern
6. ✅ No regression in test generation or execution

## Candidate Methods for Consolidation

After reviewing the code, here are confirmed pass-through methods to consolidate:

### In CommonGivenSteps.cs
```csharp
// CONSOLIDATE: GivenTheApplicationIsRunning() → GivenLaunchedSite()
[Given("has user launched site")]
[Given("the application is running")]
protected async Task GivenLaunchedSite()

// CONSOLIDATE: GivenIAmViewingWeatherForecasts() → WhenINavigateToViewTheWeatherForecast()
// (from WeatherSteps.cs)
```

### In WeatherSteps.cs
```csharp
// CONSOLIDATE: GivenIAmOnTheHomePage() → GivenLaunchedSite()
// Move pattern to CommonGivenSteps.cs, remove from WeatherSteps.cs

// CONSOLIDATE: GivenIAmViewingWeatherForecasts() → WhenINavigateToViewTheWeatherForecast()
// Add [Given("I am viewing weather forecasts")] to WhenINavigateToViewTheWeatherForecast()
```

### In WorkspaceTenancySteps.cs
```csharp
// CONSOLIDATE: ThenIShouldSeeAllWorkspaces() → ThenIShouldHaveWorkspacesAvailable()
// These two methods are already aliased at line 785-786
[Then("I should have {expectedCount} workspaces available")]
[Then("I should see all {expectedCount} workspaces")]
[Then("the workspace count should be {expectedCount}")]
protected async Task ThenIShouldHaveWorkspacesAvailable(int expectedCount)
```

**Note:** Need to do a comprehensive pass through all files to identify all pass-through methods during implementation.

## Timeline Estimate

**Total: ~10-14 hours of work** (increased due to consolidation analysis)

- Phase 1 (Attributes): 1 hour
- Phase 2 (Common steps): 1-2 hours
- **Phase 2.5 (Consolidation analysis): 1-2 hours** ← NEW
- Phase 3 (Feature steps): 4-6 hours
- Phase 4 (Documentation): 1-2 hours
- Phase 5 (Validation): 1-2 hours

Can be split across multiple work sessions. Phases 3 can be parallelized if multiple people work on it.
