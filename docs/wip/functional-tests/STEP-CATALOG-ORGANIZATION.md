---
status: Draft
created: 2026-01-03
target_audience: Development Team
---

# Step Catalog Organization Proposal

## Problem Statement

The functional test step catalog has grown to **170 step methods** across 6 files, making it difficult to:
- Find the right step method when writing tests
- Know if a step already exists
- Avoid creating duplicate steps
- Understand what steps are available

**Current Distribution:**
- `AuthenticationSteps.cs` - 37 steps
- `BankImportSteps.cs` - 13 steps
- `TransactionRecordSteps.cs` - 55 steps (largest!)
- `WeatherSteps.cs` - 7 steps
- `WorkspaceTenancySteps.cs` - 53 steps
- `Common/` - 5 shared steps

**This is the hidden time sink** - navigating through hundreds of methods to find or verify step existence.

## Solution: Multi-Layered Discoverability

### 1. Step Catalog Index (Quick Reference) ✅ **Highest Impact**

Create a machine-readable and human-readable step catalog that can be searched quickly.

**File:** `tests/Functional/Steps/STEP-CATALOG.md`

**Format:**
```markdown
# Functional Test Step Catalog

Auto-generated from step definitions. Last updated: 2026-01-03

## Quick Search

Use Ctrl+F to find step patterns. Steps are organized by feature and type (Given/When/Then).

## Authentication Steps (37 total)

### GIVEN Steps (6)
| Pattern | Method | File | Line |
|---------|--------|------|------|
| `I am on the registration page` | `GivenIAmOnTheRegistrationPage` | AuthenticationSteps.cs | 25 |
| `I have an existing account with email {email}` | `GivenIHaveAnExistingAccountWithEmail` | AuthenticationSteps.cs | 41 |
| `I am on any page in the application` | `GivenIAmOnAnyPageInTheApplication` | AuthenticationSteps.cs | 57 |
| `I am viewing my workspace dashboard` | `GivenIAmViewingMyWorkspaceDashboard` | AuthenticationSteps.cs | 70 |
| `an account already exists with email {email}` | `GivenAnAccountAlreadyExistsWithEmail` | AuthenticationSteps.cs | 84 |
| `I am viewing my profile page` | `GivenIAmViewingMyProfilePage` | AuthenticationSteps.cs | 98 |

### WHEN Steps (11)
| Pattern | Method | File | Line |
|---------|--------|------|------|
| `I enter valid registration details` | `WhenIEnterValidRegistrationDetails` | AuthenticationSteps.cs | 118 |
| `I submit the registration form` | `WhenISubmitTheRegistrationForm` | AuthenticationSteps.cs | 135 |
| ... (truncated for example)

### THEN Steps (20)
| Pattern | Method | File | Line |
|---------|--------|------|------|
| `my registration request should be acknowledged` | `ThenMyRegistrationRequestShouldBeAcknowledged` | AuthenticationSteps.cs | 427 |
| ... (truncated for example)
```

**Benefits:**
- ✅ Ctrl+F to search for Gherkin text
- ✅ See all available steps in one place
- ✅ Quickly locate step in source file
- ✅ Know if a step already exists before creating

**Generation:**
Create PowerShell script to auto-generate from source:

```powershell
# scripts/Generate-StepCatalog.ps1
# Parses step attributes from *.cs files
# Generates STEP-CATALOG.md with searchable index
```

### 2. Visual Studio Code Search View Configuration ✅ **High Impact**

Configure VS Code's search to make step discovery faster.

**File:** `.vscode/settings.json` (project-specific)

```json
{
  "search.exclude": {
    "**/bin": true,
    "**/obj": true,
    "**/node_modules": true
  },
  "search.useIgnoreFiles": true,
  "files.associations": {
    "*.feature": "gherkin"
  }
}
```

**Workflow Enhancement:**
1. Press `Ctrl+Shift+F` (search all files)
2. Search for Gherkin pattern: `[Given("I am on the login page")]`
3. Finds exact step attribute immediately

### 3. Step Method Naming Conventions ✅ **Medium Impact**

**Problem:** Some step methods have cryptic names that don't match their Gherkin text

**Examples:**
- ✅ Good: `[When("I click the login button")]` → `WhenIClickTheLoginButton()` (clear match)
- ⚠️ Unclear: `[Then("page loaded ok")]` → `ThenPageLoadedOk()` (ok, but could be better)
- ❌ Poor: `[When("user visits the {option} page")]` → `VisitPage(string option)` (doesn't follow convention)

**Recommendation:**
- Keep method names descriptive but not verbatim
- Use consistent prefixes: `Given*`, `When*`, `Then*`
- Avoid abbreviations unless obvious

**Update Conventions Document:** Add to [`tests/Functional/Steps/README.md`](../../tests/Functional/Steps/README.md)

### 4. Reorganize Large Step Files ⚡ **High Impact for Large Files**

**Problem:** `TransactionRecordSteps.cs` has 55 steps and `WorkspaceTenancySteps.cs` has 53 steps. These are hard to navigate even within the file.

**Solution:** Split by functional area within feature

**Before:**
```
tests/Functional/Steps/
├── TransactionRecordSteps.cs (55 steps - too large!)
```

**After:**
```
tests/Functional/Steps/
├── TransactionRecordSteps.cs (base class, common transaction operations)
├── TransactionRecord/
│   ├── TransactionListSteps.cs (list view operations - ~15 steps)
│   ├── TransactionDetailsSteps.cs (details page operations - ~15 steps)
│   ├── TransactionEditSteps.cs (editing operations - ~15 steps)
│   └── TransactionCreateSteps.cs (creation operations - ~10 steps)
```

**Benefits:**
- Smaller files are easier to navigate
- Related steps grouped together
- Clear separation of concerns
- Each file is <200 lines

**Apply Same Pattern to WorkspaceTenancySteps:**

```
tests/Functional/Steps/
├── WorkspaceTenancySteps.cs (base class)
├── WorkspaceTenancy/
│   ├── WorkspaceAccessSteps.cs (access control steps - ~20 steps)
│   ├── WorkspaceManagementSteps.cs (CRUD operations - ~20 steps)
│   └── WorkspaceDataSteps.cs (data isolation steps - ~13 steps)
```

### 5. Step Method Regions for In-File Navigation ✅ **Already Done Well**

**Current Structure (Good):**
```csharp
#region Steps: GIVEN
// All Given steps
#endregion

#region Steps: WHEN
// All When steps
#endregion

#region Steps: THEN
// All Then steps
#endregion

#region Helpers
// Helper methods
#endregion
```

**Benefits:**
- ✅ Collapsible regions in IDE
- ✅ Clear separation by step type
- ✅ Easy to jump to section

**Keep this pattern** - it's working well!

### 6. Step Attribute Searchability Tool 🔧 **Medium Impact**

Create a simple grep-able tool for finding steps:

**File:** `scripts/Find-Step.ps1`

```powershell
<#
.SYNOPSIS
Finds step methods by Gherkin pattern.

.DESCRIPTION
Searches for step attributes matching the provided Gherkin text pattern.
Returns the file and line number of matching steps.

.PARAMETER Pattern
The Gherkin step text to search for (regex supported).

.EXAMPLE
.\scripts\Find-Step.ps1 -Pattern "I am on the login page"
Finds steps with that exact text.

.EXAMPLE
.\scripts\Find-Step.ps1 -Pattern "I (am|was) on the"
Finds steps matching the regex pattern.
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$Pattern
)

$stepsPath = Join-Path $PSScriptRoot ".." "tests" "Functional" "Steps"

Write-Host "Searching for step pattern: $Pattern" -ForegroundColor Cyan
Write-Host ""

Get-ChildItem -Path $stepsPath -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $content = Get-Content $file.FullName -Raw

    # Find step attributes matching pattern
    $regex = '\[(?:Given|When|Then)\("([^"]+)"\)\]'
    $matches = [regex]::Matches($content, $regex)

    foreach ($match in $matches) {
        $stepPattern = $match.Groups[1].Value
        if ($stepPattern -match $Pattern) {
            $lineNumber = ($content.Substring(0, $match.Index) -split "`n").Count
            $relativePath = $file.FullName.Replace($PSScriptRoot, "").TrimStart('\', '/')
            Write-Host "✓ Found: $stepPattern" -ForegroundColor Green
            Write-Host "  File: $relativePath" -ForegroundColor Gray
            Write-Host "  Line: $lineNumber" -ForegroundColor Gray
            Write-Host ""
        }
    }
}
```

**Usage:**
```powershell
# Find login-related steps
.\scripts\Find-Step.ps1 -Pattern "login"

# Find transaction editing steps
.\scripts\Find-Step.ps1 -Pattern "edit.*transaction"
```

### 7. Common Step Extraction ✅ **Low-Medium Impact**

**Current:** Common steps in `Common/` directory (5 steps)

**Recommendation:** Extract more common patterns

**Candidates for Common Steps:**
```csharp
// These appear in multiple files:
[Then("I should be redirected to {page} page")] // Used 3+ times
[When("I click the {buttonName} button")] // Generic button click
[Then("I should see {count} {itemType}")] // Generic count assertion
```

**Benefits:**
- Reduces duplication
- Single source of truth
- Easier maintenance

**Caution:** Don't over-extract. Feature-specific steps should stay in feature files.

### 8. Step Documentation Templates ✅ **Already Done Well**

Your XML documentation on steps is excellent:

```csharp
/// <summary>
/// Attempts to register with an email that already exists in the system.
/// </summary>
/// <remarks>
/// Retrieves existing user credentials from object store and attempts to register
/// a new account using the same email but different username. Used to test
/// duplicate email validation.
/// </remarks>
[When("I enter registration details with the existing email")]
protected async Task WhenIEnterRegistrationDetailsWithTheExistingEmail()
```

**Keep this quality!** It helps when navigating the catalog.

## Recommended Implementation Order

### Phase 1: Quick Wins (1-2 hours)

1. ✅ **Create Find-Step.ps1 script** (30 min)
   - Immediate discoverability improvement
   - No code changes required

2. ✅ **Generate STEP-CATALOG.md** (1 hour)
   - Build script to parse step attributes
   - Generate initial catalog
   - Add to git

### Phase 2: File Reorganization (4-6 hours)

3. ⚡ **Split TransactionRecordSteps.cs** (2-3 hours)
   - Create subdirectory structure
   - Move steps to new files
   - Update inheritance chain
   - Run tests to verify

4. ⚡ **Split WorkspaceTenancySteps.cs** (2-3 hours)
   - Same process as TransactionRecordSteps

### Phase 3: Ongoing Maintenance

5. ✅ **Update STEP-CATALOG.md on changes** (5 min per change)
   - Run generation script after adding steps
   - Commit updated catalog

6. ✅ **Extract common patterns as discovered** (ongoing)
   - When you notice duplication, extract to Common/

## Measuring Success

**Before:**
- ⏱️ 5-10 minutes to find if a step exists
- ⏱️ 10-15 minutes navigating large step files
- ❌ Frequently create duplicate steps by accident

**After:**
- ⏱️ 30 seconds to search STEP-CATALOG.md
- ⏱️ 1-2 minutes to locate step in smaller files
- ✅ Quickly verify step existence before creating

**Expected Time Savings:** 10-20 minutes per test feature (20-30% improvement)

## Alternative: VS Code Extension

**If step catalog grows beyond 200-300 steps**, consider:

**Custom VS Code Extension Features:**
- Autocomplete Gherkin steps from catalog
- "Go to Step Definition" from .feature files
- List all available steps in sidebar
- Highlight duplicate steps

**Effort:** 1-2 weeks to build
**ROI:** Only worth it if catalog grows significantly larger

## Comparison to Other Projects

### How Other BDD Projects Handle This

| Project | Step Count | Organization Strategy |
|---------|-----------|----------------------|
| SpecFlow (typical) | 50-100 | Single Steps directory, IDE autocomplete |
| Cucumber (Ruby) | 100-200 | Feature-based subdirectories |
| Behave (Python) | 50-150 | Context-based files (environment, api, ui) |
| **YoFi.V3** | **170** | **Feature-based files (needs improvement)** |

**Industry Practice:** Split into subdirectories when a single file exceeds 500-1000 lines or 30-50 steps.

**Your situation:** Two files (TransactionRecord, WorkspaceTenancy) are at the threshold. Splitting now will prevent future pain.

## Conclusion

The **step catalog navigation problem** is a real productivity drain that compounds over time. The good news is there are practical, incremental solutions:

**Immediate (This Week):**
- ✅ Create `Find-Step.ps1` script (30 min)
- ✅ Generate `STEP-CATALOG.md` (1 hour)

**Short-term (Next Sprint):**
- ⚡ Split `TransactionRecordSteps.cs` into subdirectories (2-3 hours)
- ⚡ Split `WorkspaceTenancySteps.cs` into subdirectories (2-3 hours)

**Long-term (Ongoing):**
- ✅ Keep catalog updated
- ✅ Extract common patterns as discovered

**Expected Impact:** 10-20 minutes saved per feature test (20-30% of current time)

This addresses the **real** time sink you identified while maintaining your excellent test quality.

## References

- [`tests/Functional/Steps/README.md`](../../tests/Functional/Steps/README.md) - Current step organization docs
- [`tests/Functional/Steps/AuthenticationSteps.cs`](../../tests/Functional/Steps/AuthenticationSteps.cs) - 37 steps (manageable)
- [`tests/Functional/Steps/TransactionRecordSteps.cs`](../../tests/Functional/Steps/TransactionRecordSteps.cs) - 55 steps (needs splitting)
- [`tests/Functional/Steps/WorkspaceTenancySteps.cs`](../../tests/Functional/Steps/WorkspaceTenancySteps.cs) - 53 steps (needs splitting)
