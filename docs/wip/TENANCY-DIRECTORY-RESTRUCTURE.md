# Tenancy Directory Restructure Plan

## Current State Analysis

The `src/Controllers/Tenancy` directory currently contains 9 files at the root level with mixed concerns:

### Current Structure
```
src/Controllers/Tenancy/
├── RequireTenantRoleAttribute.cs        [Authorization]
├── ServiceCollectionExtensions.cs       [Configuration]
├── TenantContext.cs                     [Context Management]
├── TenantContextMiddleware.cs           [Middleware]
├── TenantController.cs                  [API/Controller]
├── TenantFeature.cs                     [Business Logic + DTOs]
├── TenantRoleHandler.cs                 [Authorization]
├── TenantRoleRequirement.cs             [Authorization]
├── TenantUserClaimsService.cs           [Authorization/Claims]
└── Exceptions/
    └── TenancyExceptionHandler.cs       [Exception Handling]
```

### Identified Concerns
1. **API Layer** - Controllers and DTOs
2. **Authorization** - Role-based access control components
3. **Context Management** - Tenant context and middleware
4. **Configuration** - Dependency injection setup
5. **Exception Handling** - Error mapping and responses

## Proposed Restructure

### New Directory Structure

```
src/Controllers/Tenancy/
├── Api/
│   ├── TenantController.cs              [API endpoints for tenant management]
│   └── Dto/
│       ├── TenantEditDto.cs             [Input DTO for create/update]
│       ├── TenantResultDto.cs           [Output DTO for tenant data]
│       └── TenantRoleResultDto.cs       [Output DTO with role info]
│
├── Authorization/
│   ├── RequireTenantRoleAttribute.cs    [Authorization attribute]
│   ├── TenantRoleHandler.cs             [Authorization handler logic]
│   ├── TenantRoleRequirement.cs         [Authorization requirement]
│   └── TenantUserClaimsService.cs       [Claims provider for tokens]
│
├── Context/
│   ├── TenantContext.cs                 [Tenant context state management]
│   └── TenantContextMiddleware.cs       [Middleware to set context]
│
├── Features/
│   └── TenantFeature.cs                 [Business logic for tenant operations]
│
├── Exceptions/
│   └── TenancyExceptionHandler.cs       [Maps exceptions to HTTP responses]
│
└── ServiceCollectionExtensions.cs       [DI configuration - stays at root]
```

## Rationale

### 1. **Api/** - API Layer Concerns
- **Purpose**: Contains all HTTP/controller-related code and DTOs
- **Contents**:
  - [`TenantController.cs`](../../src/Controllers/Tenancy/TenantController.cs) - RESTful endpoints
  - **Dto/** subdirectory - All data transfer objects used in API contracts
- **Benefits**:
  - Clear separation of API concerns
  - DTOs grouped with controller that uses them
  - Easy to find all API-related code

### 2. **Authorization/** - Authorization Concerns
- **Purpose**: All components related to role-based tenant authorization
- **Contents**:
  - [`RequireTenantRoleAttribute.cs`](../../src/Controllers/Tenancy/RequireTenantRoleAttribute.cs) - Declarative authorization
  - [`TenantRoleHandler.cs`](../../src/Controllers/Tenancy/TenantRoleHandler.cs) - Authorization logic
  - [`TenantRoleRequirement.cs`](../../src/Controllers/Tenancy/TenantRoleRequirement.cs) - Requirement definition
  - [`TenantUserClaimsService.cs`](../../src/Controllers/Tenancy/TenantUserClaimsService.cs) - Claims transformation
- **Benefits**:
  - All authorization components together
  - Clear understanding of authorization flow
  - Easier to maintain authorization logic

### 3. **Context/** - Request Context Management
- **Purpose**: Manages tenant context per HTTP request
- **Contents**:
  - [`TenantContext.cs`](../../src/Controllers/Tenancy/TenantContext.cs) - Context state holder
  - [`TenantContextMiddleware.cs`](../../src/Controllers/Tenancy/TenantContextMiddleware.cs) - Context initialization
- **Benefits**:
  - Tight coupling between context and middleware is visible
  - Clear pipeline responsibility
  - Isolated from other concerns

### 4. **Features/** - Business Logic Layer
- **Purpose**: Application/business logic that controllers delegate to
- **Contents**:
  - [`TenantFeature.cs`](../../src/Controllers/Tenancy/TenantFeature.cs) - Orchestrates tenant operations
- **Benefits**:
  - Separates business logic from controllers
  - Follows feature-based organization pattern
  - Room to grow if more features are added

### 5. **Exceptions/** - Exception Handling (Existing)
- **Purpose**: HTTP exception mapping
- **Contents**:
  - [`TenancyExceptionHandler.cs`](../../src/Controllers/Tenancy/Exceptions/TenancyExceptionHandler.cs)
- **Benefits**: Already well-organized, no change needed

### 6. **ServiceCollectionExtensions.cs** - Configuration (Root)
- **Purpose**: Dependency injection and middleware configuration
- **Rationale for staying at root**:
  - Natural entry point when integrating tenancy
  - Common pattern in ASP.NET Core projects
  - Immediately visible at directory level

## Benefits of This Structure

### Clear Separation of Concerns
- Each directory has a single, well-defined responsibility
- Files are grouped with related functionality
- Easy to understand what each part does

### Improved Discoverability
- Developers can quickly find authorization, API, or context code
- Related files are co-located
- Logical grouping matches mental model

### Better Scalability
- Each concern can grow independently
- Adding new authorization handlers goes in Authorization/
- Adding new API endpoints goes in Api/
- Clear pattern for future additions

### Maintenance Benefits
- Changes to authorization don't touch other concerns
- API changes isolated from middleware
- Easier to review and understand changes
- Reduced merge conflicts

## Migration Impact

### Files to Move (8 files)
1. `TenantController.cs` → `Api/TenantController.cs`
2. DTOs from `TenantFeature.cs` → `Api/Dto/*.cs` (extract to separate files)
3. `TenantFeature.cs` → `Features/TenantFeature.cs`
4. `RequireTenantRoleAttribute.cs` → `Authorization/RequireTenantRoleAttribute.cs`
5. `TenantRoleHandler.cs` → `Authorization/TenantRoleHandler.cs`
6. `TenantRoleRequirement.cs` → `Authorization/TenantRoleRequirement.cs`
7. `TenantUserClaimsService.cs` → `Authorization/TenantUserClaimsService.cs`
8. `TenantContext.cs` → `Context/TenantContext.cs`
9. `TenantContextMiddleware.cs` → `Context/TenantContextMiddleware.cs`

### Namespace Changes Required
All moved files will need namespace updates to reflect new paths:
- `YoFi.V3.Controllers.Tenancy.Api`
- `YoFi.V3.Controllers.Tenancy.Api.Dto`
- `YoFi.V3.Controllers.Tenancy.Authorization`
- `YoFi.V3.Controllers.Tenancy.Context`
- `YoFi.V3.Controllers.Tenancy.Features`

### Additional Refactoring: Extract DTOs
Currently, DTOs are defined inline in [`TenantFeature.cs`](../../src/Controllers/Tenancy/TenantFeature.cs) (lines 161-199). These should be extracted to separate files in `Api/Dto/`:
- `TenantEditDto.cs` (lines 166-169)
- `TenantResultDto.cs` (lines 178-183)
- `TenantRoleResultDto.cs` (lines 193-199)

### Files That Reference Moved Code
Need to update imports in:
- [`src/BackEnd/Program.cs`](../../src/BackEnd/Program.cs) - Uses ServiceCollectionExtensions
- [`tests/Integration.Controller/TenantControllerTests.cs`](../../tests/Integration.Controller/TenantControllerTests.cs)
- [`tests/Integration.Controller/TenantContextMiddlewareTests.cs`](../../tests/Integration.Controller/TenantContextMiddlewareTests.cs)
- Any files in `src/Controllers/` that reference tenancy components

## Testing Impact

### Tests Requiring Updates
1. **TenantControllerTests.cs** - Update controller namespace import
2. **TenantContextMiddlewareTests.cs** - Update middleware namespace import
3. All tests will continue to work after namespace updates

### Validation Approach
1. Run integration tests after each file move
2. Verify all namespaces compile
3. Run full test suite before completion

## Implementation Approach

### Phase 1: Create Directory Structure
- Create new directories: `Api/`, `Api/Dto/`, `Authorization/`, `Context/`, `Features/`
- Keep existing files in place initially

### Phase 2: Extract DTOs
- Extract DTOs from `TenantFeature.cs` to separate files in `Api/Dto/`
- Update `TenantFeature.cs` to reference new DTO files
- Run tests to verify

### Phase 3: Move Files by Concern
- Move authorization files → `Authorization/`
- Move context files → `Context/`
- Move feature files → `Features/`
- Move controller file → `Api/`
- Update all namespaces

### Phase 4: Update References
- Update `using` statements in all consuming code
- Update test files
- Run full test suite

### Phase 5: Clean Up
- Remove empty Tenancy root if only ServiceCollectionExtensions remains
- Update documentation
- Verify all tests pass

## Alternative Considered: Flat Structure with Prefixes

An alternative approach would be to keep files flat but use naming prefixes:
```
TenantApi_Controller.cs
TenantApi_EditDto.cs
TenantAuth_RoleAttribute.cs
TenantAuth_RoleHandler.cs
TenantContext_Context.cs
TenantContext_Middleware.cs
```

**Why the proposed structure is better:**
- IDE folder navigation is more intuitive than prefix-based grouping
- Namespaces provide clear architectural boundaries
- Standard ASP.NET Core pattern
- Better tooling support (e.g., "Add to folder")
- Easier to apply folder-level rules or conventions

## Open Questions

1. **Should `TenantFeature` remain in Controllers project?**
   - Pro: It's controller-specific business logic
   - Con: Could move to Application layer if it becomes more complex
   - **Recommendation**: Keep in Controllers for now, matches current architecture

2. **Should DTOs be in separate files or inline?**
   - **Recommendation**: Separate files for better organization and reusability

3. **Should we create a `Middleware/` directory instead of `Context/`?**
   - **Recommendation**: `Context/` better reflects the concern (context management via middleware)

## Success Criteria

- ✅ All files organized by concern
- ✅ Clear directory structure matches mental model
- ✅ All tests pass after reorganization
- ✅ No breaking changes to external consumers
- ✅ Namespace changes are consistent
- ✅ Documentation updated to reflect new structure
