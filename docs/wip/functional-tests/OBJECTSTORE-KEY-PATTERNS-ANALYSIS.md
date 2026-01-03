---
status: Complete
created: 2026-01-03
---

# ObjectStore Key Patterns Analysis

## Summary

After reviewing the functional test steps, I found **two distinct patterns** for accessing the ObjectStore:

1. **String literal keys** (direct access): `_context.ObjectStore.Get<string>("CurrentWorkspaceName")`
2. **Defined constants** (symbolic access): `_context.ObjectStore.Get<string>(KEY_CURRENT_WORKSPACE)`

## Pattern Distribution

### Files Using String Literals

1. **[`AuthSteps.cs`](tests/Functional/Steps/AuthSteps.cs)**
   - Lines 126, 176, 182: `"LoggedInAs"`
   - Lines 29, 44: `"CurrentWorkspaceName"`

2. **[`BankImportSteps.cs`](tests/Functional/Steps/BankImportSteps.cs)**
   - Line 29: `"CurrentWorkspaceName"`

3. **[`NavigationSteps.cs`](tests/Functional/Steps/NavigationSteps.cs)** - NO object store usage

4. **[`TransactionDataSteps.cs`](tests/Functional/Steps/Transaction/TransactionDataSteps.cs)**
   - Lines 39, 44: `"CurrentWorkspaceName"` and `"LoggedInAs"` (mixed - also uses KEY_CURRENT_WORKSPACE)
   - Lines 102, 107: Uses `KEY_CURRENT_WORKSPACE` constant
   - Lines 135-145: Uses string literals for transaction fields: `"TransactionPayee"`, `"TransactionAmount"`, `"TransactionCategory"`, `"TransactionMemo"`, `"TransactionSource"`, `"TransactionExternalId"`

5. **[`TransactionListSteps.cs`](tests/Functional/Steps/Transaction/TransactionListSteps.cs)**
   - Lines 40, 64, 99, 223, 250: Uses `KEY_CURRENT_WORKSPACE` constant consistently
   - Lines 122: Uses `KEY_HAS_WORKSPACE_ACCESS` constant
   - Lines 64, 223, 250: Uses `"TransactionPayee"` string literal

6. **[`TransactionCreateSteps.cs`](tests/Functional/Steps/Transaction/TransactionCreateSteps.cs)**
   - Lines 24-31: **Defines local constants** (`KEY_TRANSACTION_PAYEE`, `KEY_TRANSACTION_AMOUNT`, etc.)
   - Lines 51, 85-110: Uses these local constants

### Files Using Defined Constants

1. **[`TransactionStepsBase.cs`](tests/Functional/Steps/Transaction/TransactionStepsBase.cs)**
   - Lines 31-36: **Defines constants** for common keys
   - `KEY_LOGGED_IN_AS = "LoggedInAs"`
   - `KEY_PENDING_USER_CONTEXT = "PendingUserContext"`
   - `KEY_CURRENT_WORKSPACE = "CurrentWorkspaceName"`
   - `KEY_LAST_TRANSACTION_PAYEE = "LastTransactionPayee"`
   - `KEY_TRANSACTION_KEY = "TransactionKey"`
   - `KEY_HAS_WORKSPACE_ACCESS = "HasWorkspaceAccess"`

2. **[`WorkspaceStepsBase.cs`](tests/Functional/Steps/Workspace/WorkspaceStepsBase.cs)**
   - Lines 27-37: **Defines constants** for common keys
   - Same keys as TransactionStepsBase, plus:
   - `KEY_NEW_WORKSPACE_NAME = "NewWorkspaceName"`
   - `KEY_CAN_DELETE_WORKSPACE = "CanDeleteWorkspace"`
   - `KEY_CAN_MAKE_DESIRED_CHANGES = "CanMakeDesiredChanges"`

3. **[`WorkspaceDataSteps.cs`](tests/Functional/Steps/Workspace/WorkspaceDataSteps.cs)**
   - Uses inherited constants consistently throughout

4. **[`WorkspaceManagementSteps.cs`](tests/Functional/Steps/Workspace/WorkspaceManagementSteps.cs)**
   - Uses inherited constants consistently throughout

## Inconsistencies Found

### 1. Duplicate Key Definitions

**`TransactionStepsBase.cs`** and **`WorkspaceStepsBase.cs`** both define overlapping constants:
- `KEY_LOGGED_IN_AS`
- `KEY_PENDING_USER_CONTEXT`
- `KEY_CURRENT_WORKSPACE`
- `KEY_LAST_TRANSACTION_PAYEE`
- `KEY_TRANSACTION_KEY`
- `KEY_HAS_WORKSPACE_ACCESS`

This violates DRY (Don't Repeat Yourself) principle.

### 2. Mixed Usage

**`TransactionDataSteps.cs`** uses BOTH patterns:
- Line 39: `"CurrentWorkspaceName"` (string literal)
- Line 102: `KEY_CURRENT_WORKSPACE` (constant)
- Both refer to the same key!

### 3. String Literals in Multiple Files

The key `"LoggedInAs"` appears as a string literal in:
- `AuthSteps.cs` (lines 126, 176, 182)
- `TransactionDataSteps.cs` (lines 44, 107)

The key `"CurrentWorkspaceName"` appears as a string literal in:
- `AuthSteps.cs` (line 176)
- `BankImportSteps.cs` (line 29)
- `TransactionDataSteps.cs` (line 39)

### 4. Local Constants in Leaf Class

**`TransactionCreateSteps.cs`** defines its own local constants instead of inheriting from base or using string literals consistently.

## Recommendation: Use Defined Constants

### Why Constants Are Better

1. **Type Safety**: Compiler catches typos at compile time
   ```csharp
   // Typo - compiles but fails at runtime
   _context.ObjectStore.Get<string>("CurrentWorkspaceNam");

   // Typo - compiler error at compile time
   _context.ObjectStore.Get<string>(KEY_CURRENT_WORKSPAC);
   ```

2. **Refactoring Safety**: Change the key value in one place
   ```csharp
   // If we need to rename "CurrentWorkspaceName" to "ActiveWorkspace"
   // With constants: Change ONE line
   protected const string KEY_CURRENT_WORKSPACE = "ActiveWorkspace";

   // With string literals: Find and replace across 5+ files, hope you don't miss any
   ```

3. **Discoverability**: IntelliSense shows available keys
   - Type `KEY_` and see all available keys
   - With string literals, you must remember or search for them

4. **Consistency**: Forces same key name across all usages
   - Constants: `KEY_CURRENT_WORKSPACE` always maps to `"CurrentWorkspaceName"`
   - String literals: Risk of typos (`"CurrentWorkspaceName"` vs `"CurrentWorkspaceNam"`)

5. **Documentation**: Constants can have XML documentation
   ```csharp
   /// <summary>
   /// Object store key for the currently selected workspace name (with __TEST__ prefix).
   /// </summary>
   protected const string KEY_CURRENT_WORKSPACE = "CurrentWorkspaceName";
   ```

6. **Navigation**: "Go to Definition" works with constants
   - Right-click constant → Go to Definition → See where it's defined and documented
   - String literals: Must use "Find All References" which includes unrelated strings

### When String Literals Might Be Acceptable

1. **Truly one-off keys** used in a single method and never reused
2. **Keys that are specific to a single test scenario** and won't be shared

However, even these cases benefit from local constants (as `TransactionCreateSteps.cs` demonstrates).

## Proposed Solution

### 1. Create Central Key Registry

Create a new file [`tests/Functional/Infrastructure/ObjectStoreKeys.cs`](tests/Functional/Infrastructure/ObjectStoreKeys.cs):

```csharp
namespace YoFi.V3.Tests.Functional.Infrastructure;

/// <summary>
/// Central registry of all ObjectStore keys used across functional tests.
/// </summary>
/// <remarks>
/// This class provides a single source of truth for all object store keys,
/// preventing duplication and typos. All step classes should reference these
/// constants rather than using string literals.
/// </remarks>
public static class ObjectStoreKeys
{
    // User and Authentication Keys

    /// <summary>
    /// The full username (with __TEST__ prefix) of the currently logged-in user.
    /// </summary>
    public const string LoggedInAs = "LoggedInAs";

    /// <summary>
    /// Username set by pre-login entitlement steps before actual login occurs.
    /// </summary>
    public const string PendingUserContext = "PendingUserContext";

    // Workspace Keys

    /// <summary>
    /// The currently selected workspace name (with __TEST__ prefix).
    /// </summary>
    public const string CurrentWorkspace = "CurrentWorkspaceName";

    /// <summary>
    /// The new workspace name after a rename operation (with __TEST__ prefix).
    /// </summary>
    public const string NewWorkspaceName = "NewWorkspaceName";

    // Transaction Keys

    /// <summary>
    /// The payee name of the last created or referenced transaction.
    /// </summary>
    public const string TransactionPayee = "TransactionPayee";

    /// <summary>
    /// The amount of the last created or referenced transaction (as string).
    /// </summary>
    public const string TransactionAmount = "TransactionAmount";

    /// <summary>
    /// The category of the last created or referenced transaction.
    /// </summary>
    public const string TransactionCategory = "TransactionCategory";

    /// <summary>
    /// The memo of the last created or referenced transaction.
    /// </summary>
    public const string TransactionMemo = "TransactionMemo";

    /// <summary>
    /// The source of the last created or referenced transaction.
    /// </summary>
    public const string TransactionSource = "TransactionSource";

    /// <summary>
    /// The external ID of the last created or referenced transaction.
    /// </summary>
    public const string TransactionExternalId = "TransactionExternalId";

    /// <summary>
    /// The unique key (Guid as string) of the last created or referenced transaction.
    /// </summary>
    public const string TransactionKey = "TransactionKey";

    // Permission Check Keys

    /// <summary>
    /// Boolean flag indicating whether user has access to a workspace (for negative tests).
    /// </summary>
    public const string HasWorkspaceAccess = "HasWorkspaceAccess";

    /// <summary>
    /// Boolean flag indicating whether user can delete a workspace (for permission tests).
    /// </summary>
    public const string CanDeleteWorkspace = "CanDeleteWorkspace";

    /// <summary>
    /// Boolean flag indicating whether user can make desired changes (for permission tests).
    /// </summary>
    public const string CanMakeDesiredChanges = "CanMakeDesiredChanges";

    // Edit Mode Keys

    /// <summary>
    /// The current edit mode context (e.g., "CreateModal", "TransactionDetailsPage").
    /// </summary>
    public const string EditMode = "EditMode";
}
```

### 2. Update Base Classes

Remove duplicate constants from `TransactionStepsBase.cs` and `WorkspaceStepsBase.cs`, replace with:

```csharp
using YoFi.V3.Tests.Functional.Infrastructure;

public abstract class TransactionStepsBase(ITestContext context)
{
    protected readonly ITestContext _context = context;

    // No more local constants - use ObjectStoreKeys instead

    protected string GetLastTransactionPayee()
    {
        return _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee)
            ?? throw new InvalidOperationException($"'{ObjectStoreKeys.TransactionPayee}' not found in object store");
    }
}
```

### 3. Update All Step Classes

Replace string literals with constants:

```csharp
// Before
_context.ObjectStore.Get<string>("CurrentWorkspaceName")

// After
_context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
```

```csharp
// Before
_context.ObjectStore.Add("LoggedInAs", cred.Username);

// After
_context.ObjectStore.Add(ObjectStoreKeys.LoggedInAs, cred.Username);
```

## Migration Impact

### Files Requiring Updates

1. [`AuthSteps.cs`](tests/Functional/Steps/AuthSteps.cs) - 5 occurrences
2. [`BankImportSteps.cs`](tests/Functional/Steps/BankImportSteps.cs) - 1 occurrence
3. [`TransactionDataSteps.cs`](tests/Functional/Steps/Transaction/TransactionDataSteps.cs) - 11 occurrences
4. [`TransactionListSteps.cs`](tests/Functional/Steps/Transaction/TransactionListSteps.cs) - 3 occurrences (TransactionPayee)
5. [`TransactionCreateSteps.cs`](tests/Functional/Steps/Transaction/TransactionCreateSteps.cs) - Remove local constants, use central ones
6. [`TransactionStepsBase.cs`](tests/Functional/Steps/Transaction/TransactionStepsBase.cs) - Remove duplicates
7. [`WorkspaceStepsBase.cs`](tests/Functional/Steps/Workspace/WorkspaceStepsBase.cs) - Remove duplicates

### Testing Strategy

After migration:
1. Run all functional tests to ensure no keys were missed
2. Search codebase for remaining string literals that might be object store keys
3. Verify IntelliSense works properly with new constants

## Conclusion

**Use defined constants** - specifically, a central registry in [`ObjectStoreKeys.cs`](tests/Functional/Infrastructure/ObjectStoreKeys.cs).

This provides:
- ✅ Single source of truth
- ✅ Compile-time type safety
- ✅ Easy refactoring
- ✅ IntelliSense discoverability
- ✅ XML documentation for each key
- ✅ No duplication across base classes

The current mixed usage of string literals and duplicated constants across base classes creates maintenance burden and increases risk of bugs from typos.
