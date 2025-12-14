# Tenancy Feature Implementation - Gap Analysis

**Date:** 2024-12-14
**Status:** Ready for Implementation
**Related Documents:**
- [`tests/Functional/Features/Tenancy.feature`](../../tests/Functional/Features/Tenancy.feature)
- [`docs/wip/TENANCY-FEATURE-IMPLEMENTATION-NOTES.md`](TENANCY-FEATURE-IMPLEMENTATION-NOTES.md)
- [`docs/wip/TEST-CONTROL-API-TENANCY-ENHANCEMENTS.md`](TEST-CONTROL-API-TENANCY-ENHANCEMENTS.md)

---

## Executive Summary

**CONCLUSION: ✅ READY TO IMPLEMENT**

The codebase is in excellent shape to proceed with implementing the Tenancy feature functional tests described in [`Tenancy.feature`](../../tests/Functional/Features/Tenancy.feature). All critical backend infrastructure, Test Control API enhancements, and frontend page objects are already in place.

**Key Findings:**
- ✅ **Backend API**: Fully implemented with complete CRUD operations
- ✅ **Test Control API**: All required endpoints implemented (6/6 complete)
- ✅ **Page Objects**: Comprehensive POMs with role-based permission checks
- ✅ **Infrastructure**: Object store, base patterns, and helpers ready
- 🟡 **Step Definitions**: Need to be created (expected - this is the implementation task)

**What's Needed:** Only step definitions that leverage existing infrastructure.

---

## Detailed Analysis

### ✅ 1. Backend API Implementation (Complete)

#### Tenant Controller API
**Location:** [`src/Controllers/Tenancy/Api/TenantController.cs`](../../src/Controllers/Tenancy/Api/TenantController.cs)

**Status:** ✅ Fully Implemented

All CRUD operations required by the feature tests are implemented:

| Endpoint | Method | Purpose | Status |
|----------|--------|---------|--------|
| `/api/tenant` | GET | List user's tenants with roles | ✅ |
| `/api/tenant/{key}` | GET | Get specific tenant | ✅ |
| `/api/tenant` | POST | Create new tenant | ✅ |
| `/api/tenant/{tenantKey}` | PUT | Update tenant (Owner only) | ✅ |
| `/api/tenant/{tenantKey}` | DELETE | Delete tenant (Owner only) | ✅ |

**Key Features:**
- Returns `IReadOnlyCollection<TenantRoleResultDto>` for list operations (matches pattern)
- Includes role-based authorization via `[RequireTenantRole(TenantRole.Owner)]`
- Proper error handling with exception mapping
- Comprehensive XML documentation

#### Application Layer Features
**Location:** [`src/Application/Tenancy/Features/TenantFeature.cs`](../../src/Application/Tenancy/Features/TenantFeature.cs)

**Status:** ✅ Fully Implemented

Provides both USER and ADMIN functionality:

**USER Methods** (for production use):
- `CreateTenantForUserAsync()` - Creates tenant with user as owner
- `GetTenantsForUserAsync()` - Lists user's tenants with roles
- `GetTenantForUserAsync()` - Gets single tenant with access check
- `UpdateTenantForUserAsync()` - Updates tenant with validation
- `DeleteTenantForUserAsync()` - Deletes tenant with validation

**ADMIN Methods** (for testing):
- `GetTenantByKeyAsync()` - Get tenant without access checks
- `AddUserTenantRoleAsync()` - Assign role to user
- `GetTenantsByNamePrefixAsync()` - Find tenants by name prefix
- `DeleteTenantsByKeysAsync()` - Bulk delete tenants
- `HasUserTenantRoleAsync()` - Check if user has role

**Critical for Testing:** The ADMIN methods support Test Control API operations.

---

### ✅ 2. Test Control API Implementation (Complete)

**Location:** [`src/Controllers/TestControlController.cs`](../../src/Controllers/TestControlController.cs)

**Status:** ✅ All 6 Required Endpoints Implemented

#### Implemented Endpoints

| Endpoint | Purpose | Status | Lines |
|----------|---------|--------|-------|
| `POST /TestControl/users/bulk` | Create multiple users with credentials | ✅ | 139-192 |
| `POST /TestControl/users/{username}/workspaces` | Create workspace for user | ✅ | 250-315 |
| `POST /TestControl/users/{username}/workspaces/{workspaceKey}/assign` | Assign user to workspace | ✅ | 328-399 |
| `POST /TestControl/users/{username}/workspaces/{workspaceKey}/transactions/seed` | Seed transactions | ✅ | 412-490 |
| `DELETE /TestControl/data` | Delete all test data | ✅ | 500-526 |
| `POST /TestControl/users/{username}/workspaces/bulk` | Bulk workspace setup | ✅ | 538-594 |

#### Security Validations (All Implemented)

All endpoints properly validate:
- ✅ Username has `__TEST__` prefix (403 if not)
- ✅ Workspace name has `__TEST__` prefix (403 if not)
- ✅ User has role on workspace for data operations (403 if not)
- ✅ Proper error responses (404 for not found, 409 for conflicts)

#### Key Implementation Details

**Bulk User Creation** (lines 139-192):
```csharp
// ✅ Returns TestUserCredentials with Id, Username, Email, Password
// ✅ Auto-approves users (EmailConfirmed = true)
// ✅ Generates secure random passwords
```

**Workspace Creation** (lines 250-315):
```csharp
// ✅ Validates __TEST__ prefix on workspace name
// ✅ Supports role specification (Owner, Editor, Viewer)
// ✅ Creates tenant via TenantFeature.CreateTenantForUserAsync()
// Note: Role update for non-Owner roles has TODO comment (line 309)
```

**🟡 Minor Issue Identified:** Line 309 has a TODO comment about updating roles for non-Owner scenarios. The current implementation creates all workspaces with Owner role, even if a different role is requested. This needs to be addressed but doesn't block implementation.

**Transaction Seeding** (lines 412-490):
```csharp
// ✅ Validates user has role on workspace
// ✅ Sets TenantContext for transaction creation
// ✅ Generates realistic test data (random amounts, distributed dates)
// ✅ Uses TransactionsFeature.AddTransactionAsync()
```

**Bulk Workspace Setup** (lines 538-594):
```csharp
// ✅ Creates multiple workspaces in single request
// ✅ Validates all workspace names before creating any
// ✅ Returns keys and roles for all created workspaces
```

---

### ✅ 3. Frontend Page Objects (Complete)

#### Workspaces Page
**Location:** [`tests/Functional/Pages/WorkspacesPage.cs`](../../tests/Functional/Pages/WorkspacesPage.cs)

**Status:** ✅ Comprehensive POM with 411 lines

**Key Capabilities:**
- ✅ Create, Edit, Delete operations
- ✅ Query methods: `GetWorkspaceCountAsync()`, `HasWorkspaceAsync()`
- ✅ Role-based permission checks (lines 377-410):
  - `IsCreateWorkspaceAvailableAsync()`
  - `IsEditAvailableAsync(workspaceName)`
  - `IsDeleteAvailableAsync(workspaceName)`
- ✅ Workspace card locators by name or key
- ✅ Loading state management
- ✅ Error display component integration

**Perfect for Feature Tests:** All scenarios in Tenancy.feature can be implemented using these methods.

#### Transactions Page
**Location:** [`tests/Functional/Pages/TransactionsPage.cs`](../../tests/Functional/Pages/TransactionsPage.cs)

**Status:** ✅ Comprehensive POM with 556 lines

**Key Capabilities:**
- ✅ Create, Edit, Delete transaction operations
- ✅ Query methods: `GetTransactionCountAsync()`, `HasTransactionAsync()`
- ✅ Role-based permission checks (lines 521-555):
  - `IsNewTransactionAvailableAsync()`
  - `IsEditAvailableAsync(payeeName)`
  - `IsDeleteAvailableAsync(payeeName)`
- ✅ Date range filtering
- ✅ Workspace selector integration
- ✅ Loading state management

**Perfect for Data Isolation Tests:** Scenarios testing transaction access across workspaces (lines 100-118 of Tenancy.feature) can be implemented easily.

#### Workspace Selector Component
**Location:** [`tests/Functional/Components/WorkspaceSelector.cs`](../../tests/Functional/Components/WorkspaceSelector.cs)

**Status:** ✅ Comprehensive component wrapper with 266 lines

**Key Capabilities:**
- ✅ `SelectWorkspaceAsync(workspaceName)` - Switch workspaces
- ✅ `GetCurrentWorkspaceNameAsync()` - Get active workspace
- ✅ `GetAvailableWorkspacesAsync()` - List all accessible workspaces
- ✅ `GetWorkspaceRoleAsync()` - Check user's role
- ✅ `ClickManageWorkspacesAsync()` - Navigate to workspaces page

**Perfect for Workspace Switching:** All scenarios requiring workspace context switching can use this.

---

### ✅ 4. Base Infrastructure (Complete)

#### FunctionalTest Base Class
**Location:** [`tests/Functional/Steps/FunctionalTest.cs`](../../tests/Functional/Steps/FunctionalTest.cs)

**Status:** ✅ Solid foundation with 529 lines

**Key Infrastructure:**
- ✅ ObjectStore for sharing data between steps (lines 499-529)
- ✅ `It<T>()` and `The<T>(key)` helper methods (lines 27-28)
- ✅ `testControlClient` property for API access (lines 35-51)
- ✅ Helper methods for page object creation (lines 462-486)
- ✅ Prerequisites checking (Playwright browsers, backend health)
- ✅ Screenshot capture support

**Perfect for Test Steps:** All infrastructure needed for step definitions is here.

#### BasePage with IsAvailableAsync
**Location:** [`tests/Functional/Pages/BasePage.cs`](../../tests/Functional/Pages/BasePage.cs) lines 84-89

**Status:** ✅ Already Implemented

The `IsAvailableAsync(ILocator locator)` method is already implemented in BasePage exactly as described in the implementation notes. This method abstracts permission checks by verifying both visibility and enabled state.

```csharp
public async Task<bool> IsAvailableAsync(ILocator locator)
{
    var isVisible = await locator.IsVisibleAsync();
    if (!isVisible) return false;
    return await locator.IsEnabledAsync();
}
```

---

### ✅ 5. No Infrastructure Gaps Remaining

All backend infrastructure is now complete! The only remaining task is creating the step definitions.

#### Implementation Detail: Role Assignment Fixed
**Location:** [`src/Controllers/TestControlController.cs`](../../src/Controllers/TestControlController.cs) lines 291-301

**What was fixed:** The `CreateWorkspaceForUser` endpoint now properly handles all roles (Owner, Editor, Viewer) by:
1. Creating tenant without any role assignments using new `TenantFeature.CreateTenantAsync()` method
2. Assigning the requested role to the user via `TenantFeature.AddUserTenantRoleAsync()`

**New ADMIN method added:** [`TenantFeature.CreateTenantAsync()`](../../src/Application/Tenancy/Features/TenantFeature.cs) at lines 169-191 - Creates tenant without assigning any roles, allowing flexible role assignment afterwards.

#### Only Remaining Task: Step Definitions
**Location:** Need to create `tests/Functional/Steps/WorkspaceTenancySteps.cs`

**Issue:** This is the expected gap - it's the implementation task.

**Impact:** Blocking - Cannot run tests without step definitions

**Solution:** Create step definitions file following patterns in implementation notes.

---

### ✅ 6. DTOs and Models (Complete)

All required DTOs are implemented:

| DTO | Location | Purpose | Status |
|-----|----------|---------|--------|
| `TenantEditDto` | `src/Application/Tenancy/Dto/` | Create/update tenant | ✅ |
| `TenantResultDto` | `src/Application/Tenancy/Dto/` | Tenant response | ✅ |
| `TenantRoleResultDto` | `src/Application/Tenancy/Dto/` | Tenant with user role | ✅ |
| `TestUserCredentials` | `src/Controllers/TestControlController.cs` | User credentials | ✅ |
| `WorkspaceCreateRequest` | `src/Controllers/TestControlController.cs` | Workspace creation | ✅ |
| `WorkspaceSetupRequest` | `src/Controllers/TestControlController.cs` | Bulk workspace setup | ✅ |
| `WorkspaceSetupResult` | `src/Controllers/TestControlController.cs` | Setup result | ✅ |
| `UserRoleAssignment` | `src/Controllers/TestControlController.cs` | Role assignment | ✅ |
| `TransactionSeedRequest` | `src/Controllers/TestControlController.cs` | Transaction seeding | ✅ |

---

## Scenario-by-Scenario Readiness

### Rule: Getting Started (Lines 19-26)
**Scenario:** New user automatically has a personal workspace

**Required Infrastructure:**
- ✅ User registration flow (exists in auth system)
- ✅ Tenant creation API (TenantController)
- ✅ WorkspacesPage to verify workspace exists

**Readiness:** ✅ Ready - All infrastructure present

---

### Rule: Creating Workspaces (Lines 28-42)
**Scenarios:** User creates workspaces for specific purposes

**Required Infrastructure:**
- ✅ WorkspacesPage.CreateWorkspaceAsync()
- ✅ WorkspacesPage.HasWorkspaceAsync()
- ✅ WorkspacesPage.GetWorkspaceCountAsync()
- ✅ Tenant creation API

**Readiness:** ✅ Ready - All page object methods exist

---

### Rule: Viewing Workspaces (Lines 44-63)
**Scenarios:** User views all their workspaces and details

**Required Infrastructure:**
- ✅ Test Control API for bulk workspace setup
- ✅ WorkspacesPage.GetWorkspaceCountAsync()
- ✅ WorkspacesPage.GetWorkspaceRoleAsync()
- ✅ WorkspacesPage for workspace details

**Readiness:** ✅ Ready - Test Control API supports bulk setup

---

### Rule: Managing Workspace Settings (Lines 65-80)
**Scenarios:** Update and restrict workspace modifications

**Required Infrastructure:**
- ✅ WorkspacesPage.UpdateWorkspaceAsync()
- ✅ WorkspacesPage.IsEditAvailableAsync()
- ✅ Tenant update API with role restrictions

**Readiness:** ✅ Ready - Role-based authorization implemented

---

### Rule: Removing Workspaces (Lines 82-95)
**Scenarios:** Delete workspaces with role restrictions

**Required Infrastructure:**
- ✅ WorkspacesPage.DeleteWorkspaceAsync()
- ✅ WorkspacesPage.IsDeleteAvailableAsync()
- ✅ Tenant delete API with Owner role requirement

**Readiness:** ✅ Ready - Delete operations and role checks exist

---

### Rule: Data Isolation Between Workspaces (Lines 97-118)
**Scenarios:** Transactions isolated per workspace

**Required Infrastructure:**
- ✅ Test Control API transaction seeding
- ✅ TransactionsPage.GetTransactionCountAsync()
- ✅ WorkspaceSelector.SelectWorkspaceAsync()
- ✅ Tenant context middleware

**Readiness:** ✅ Ready - All seeding and isolation infrastructure exists

---

### Rule: Permission Levels (Lines 120-146)
**Scenarios:** Viewer, Editor, Owner role functionality

**Required Infrastructure:**
- ✅ TransactionsPage.IsNewTransactionAvailableAsync()
- ✅ TransactionsPage.IsEditAvailableAsync()
- ✅ TransactionsPage.IsDeleteAvailableAsync()
- ✅ WorkspacesPage role-based permission methods
- ✅ Test Control API for assigning roles

**Readiness:** ✅ Ready - Comprehensive permission checking in POMs

---

### Rule: Privacy and Security (Lines 148-162)
**Scenarios:** Users only see workspaces they have access to

**Required Infrastructure:**
- ✅ Test Control API for creating isolated workspaces
- ✅ WorkspacesPage.GetWorkspaceCountAsync()
- ✅ WorkspacesPage.HasWorkspaceAsync()
- ✅ Tenant access control in backend

**Readiness:** ✅ Ready - Access control enforced at API level

---

## Implementation Roadmap

### Phase 1: Create Step Definitions File
**Estimated Effort:** 4-6 hours

Create `tests/Functional/Steps/WorkspaceTenancySteps.cs` with:
1. Background steps (bulk user creation)
2. Workspace creation/management steps
3. Transaction seeding steps
4. Permission verification steps
5. Security isolation steps

**Pattern to Follow:**
```csharp
using YoFi.V3.Tests.Functional.Helpers;

public partial class WorkspaceTenancySteps : FunctionalTest
{
    // Given: these users exist
    protected async Task GivenTheseUsersExist(DataTable users)
    {
        // Use extension method for cleaner single-column table access
        var usernames = users.ToSingleColumnList();
        var credentials = await testControlClient.CreateBulkUsersAsync(usernames);
        foreach (var cred in credentials)
        {
            _objectStore.Add($"User_{cred.Username.Replace("__TEST__", "")}", cred);
        }
    }

    // More steps...
}
```

### Phase 3: Run and Refine
**Estimated Effort:** 2-4 hours

1. Run tests against local development environment
2. Fix any selector issues or timing problems
3. Refine step implementations based on actual UI behavior
4. Add screenshot captures at key points

---

## Risk Assessment

### Low Risk ✅
- Backend API is stable and tested
- Page objects are comprehensive
- Test Control API is fully implemented
- Infrastructure patterns are proven
- All role assignment scenarios now work correctly

### Medium Risk 🟡
- Some scenarios might need selector adjustments
- Timing issues may require `WaitFor` additions

### High Risk ❌
- None identified

---

## Recommendations

### 1. Proceed with Implementation ✅
The codebase is in excellent condition to implement the Tenancy feature tests. All critical infrastructure is in place, including the role assignment fix that was just completed.

### 3. Use Existing Patterns
The implementation notes provide excellent patterns. Follow them closely:
- Use Test Control API for data setup
- Store context in ObjectStore
- Use page object methods for UI interactions
- Leverage role-based permission check methods

### 4. Implement in Phases
1. Start with simple scenarios (Creating Workspaces)
2. Add data isolation scenarios
3. Complete with complex security scenarios
4. Refine based on test execution results

### 5. Testing Strategy
- Run against local development first
- Use `Start-LocalDev.ps1` for backend
- Verify Test Control API endpoints manually if needed
- Add screenshots at key assertion points

---

## Conclusion

**Status: ✅ READY TO IMPLEMENT**

The implementation of Tenancy feature functional tests can proceed immediately. The codebase has:
- ✅ Complete backend API with proper authorization
- ✅ All 6 required Test Control API endpoints
- ✅ Comprehensive page objects with role-based checks
- ✅ Solid base infrastructure and patterns
- ✅ All role assignment scenarios working correctly (just fixed!)
- ✅ Zero infrastructure gaps remaining

**Next Step:** Create `WorkspaceTenancySteps.cs` following the patterns in the implementation notes.

**Estimated Time to Complete:** 6-10 hours total (including testing and refinement)
