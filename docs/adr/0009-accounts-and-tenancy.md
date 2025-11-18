# 0009. Multi-tenancy and Workspace model

Date: 2025-11-13

## Status

Accepted

## Context

### Question

How should YoFi.V3 handle multiple users and financial data isolation? What is the relationship between users and financial "Workspaces"?

### Background

YoFi.V3 is a rewrite of the [YoFi personal finance application](https://github.com/jcoliz/yofi). YoFi is single-tenant. This is a constraint I would like to improveupon with this rewrite.

### Key Questions

1. **What is an "Workspace"?**
   - A financial institution account (bank Workspace, credit card)?
   - A logical grouping of financial data (household, business)?
   - A tenant boundary for multi-user access?

2. **User-to-Workspace Relationships:**
   - Can one user access multiple Workspaces?
   - Can multiple users access the same Workspace?
   - How are permissions managed?

3. **Data Isolation:**
   - How is financial data segregated?
   - What happens when a user is removed from an Workspace?

### Use Case Analysis

**Primary Use Cases:**
- **Personal Finance**: Individual manages their own financial data
- **Household Finance**: Family members share access to household financial data
- **Small Business**: Business owner + bookkeeper access business financial data
- **Multi-Workspace**: User manages both personal and business finances separately

**Secondary Use Cases:**
- **Financial Advisor**: Advisor has read-only access to client Workspaces
- **Shared Expenses**: Roommates track shared expenses
- **Family Financial Planning**: Parents and adult children coordinate finances

## Decision

### Workspace Model: **Logical Financial Boundary**

An "Workspace" in YoFi.V3 represents a **logical boundary for financial data** - not a bank Workspace, but a complete set of financial records (transactions, budgets, categories, etc.) managed as a unit.

Examples:
- "Smith Family Finances" (household Workspace)
- "John's Personal Finances" (individual Workspace)
- "ABC Consulting Business" (business Workspace)

As an example, I would like to have my own personal "Workspace" which only I can access, as well as an "Workspace" that my wife and I share (household Workspace).

"Workspaces" in YoFi have a one-to-many relationship with accounts at a bank. As it stands today, I download all transactions from multiple credit cards, and our savings accounts, and our checking account into a single YoFi "Workspace". Generally this works well, however, it would be useful to track which bank account  any transaction came from.

### Multi-Tenancy Model: **Multi-Workspace Users with Role-Based Access**

#### User-to-Workspace Relationships

**One User → Multiple Workspaces**: Users can have access to multiple Workspaces
- Personal Workspace + business Workspace
- Multiple business Workspaces
- Family Workspace + personal Workspace

**Multiple Users → One Workspace**: Workspaces can have multiple users with different roles
- Family members accessing household finances
- Business owner + bookkeeper
- Financial advisor with read-only access

#### Permission Model

**Three Role Levels:**
1. **Owner** - Full control (edit data, manage users, delete Workspace)
2. **Editor** - Edit financial data, view reports (cannot manage users)
3. **Viewer** - Read-only access to data and reports

**Workspace Management:**
- Workspace creation automatically makes creator the Owner
- Only Owners can invite/remove users
- Only Owners can change user roles
- Each Workspace must have at least one Owner

#### Data Isolation

**Complete Separation**: Financial data is completely isolated by Workspace
- Transactions, categories, budgets are Workspace-scoped
- No cross-Workspace data sharing
- User preferences are global (not Workspace-scoped)

#### Implementation vs. User Interface Terminology

**Implementation Layer**: Uses "Tenant" terminology throughout the codebase
- Database tables: `Tenants`, `UserTenantRoles`, etc.
- C# classes: `Tenant`, `TenantId`, `ITenantService`, etc.
- API routes: `/api/tenant/{tenantId}/transactions`

**User Interface Layer**: Uses "Workspace" terminology for end users
- UI components: Workspace selector, workspace switcher
- User-facing documentation and help text
- API documentation for frontend consumption

**Rationale**: This separation allows UI terminology to evolve based on user testing while keeping the implementation domain-agnostic and reusable for other multi-tenant applications.

### Database Schema Implications

```sql
-- Users (from ASP.NET Core Identity)
Users (Id, Email, UserName)

-- Tenant entity (logical tenant boundary for financial data)
-- Note: Called "Workspace" in UI, "Tenant" in implementation
Tenants (Id, Name, IsActive)
-- Future: CreatedBy, CreatedDate

-- User-to-Tenant relationship with roles
UserTenantRolesAssignments (Id, UserId, TenantId, Role)
-- Future: InvitedBy, JoinedDate

-- All financial data is tenant-scoped
Transactions (Id, TenantId, Date, Amount, Description, Source)
Categories (Id, TenantId, Name)
Budgets (Id, TenantId, Month, Amount)

-- User preferences (separate table for flexibility)
UserPreferences (Id, UserId, DefaultTenantId, Theme)
```

## Transaction Source Tracking

**Source Field**: Each transaction includes a `Source` field that identifies where the transaction originated. This provides the bank account tracking functionality in a simple, flexible way.

**Source Examples:**
- `"Chase Freedom Credit Card"` - Credit card transactions
- `"Wells Fargo Checking"` - Bank Workspace transactions  
- `"Cash"` - Cash transactions
- `"Venmo"` - Digital payment transactions
- `"Manual Entry"` - User-entered adjustments
- `"Transfer from Wells"` - Internal transfers

**Benefits of Source Approach:**
- **Simple Implementation**: No complex foreign key relationships initially
- **Natural Language**: Users immediately understand "where did this come from?"
- **Import Friendly**: Easy to set during transaction imports from banks
- **Flexible**: Can handle any transaction source without schema changes
- **Future Ready**: Can evolve to structured entities later if needed

**Future Evolution**: If more structured source management is needed, the Source field can later be supplemented with a formal `TransactionSources` table while maintaining backward compatibility.

### Implementation Details

#### JWT Claims Structure

```json
{
  "sub": "user123",
  "email": "john@example.com",
  "entitlements": "tenant1_guid:owner,tenant2_guid:editor,tenant3_guid:viewer"
}
```

#### API URL Structure

```
/api/tenant/{tenantId}/transactions
/api/tenant/{tenantId}/categories
/api/tenant/{tenantId}/budgets
/api/tenant/{tenantId}/reports
```

#### Authorization Policies

- **TenantView**: User must have Viewer, Editor, or Owner role for the tenant
- **TenantEdit**: User must have Editor or Owner role for the tenant  
- **TenantOwn**: User must have Owner role for the tenant

#### Code Organization

```
src/
  Entities/
    Models/
      Tenant.cs              // Core tenant entity
      ITenantScoped.cs       // Interface for tenant-scoped entities
  Application/
    Services/
      ITenantService.cs      // Tenant management
      TenantContext.cs       // Current tenant context
  Controllers/
    TenantController.cs      // Backend tenant operations
  Frontend/
    Components/
      WorkspaceSelector.vue  // UI uses "workspace" terminology
      WorkspaceSwitcher.vue
```

### Workspace Lifecycle

#### User Invitation
1. Owner sends invitation by email, choosing the invited role
2. Invited user accepts → gets specified role
3. Email notifications for Workspace activity

#### Workspace Creation
1. **First User/Admin**: Can register directly and gets a personal Workspace
2. **Subsequent Users**: Must be invited by existing Workspace owners
3. Upon registration → Auto-create personal Workspace for the new user
4. User logs in → Redirect to their default Workspace dashboard  
5. User can switch Workspaces via "Workspaces" page

#### User Removal
1. Only Owners can remove users
2. Owners cannot remove themselves if they're the last Owner
3. User data (preferences) remains, but Workspace access is revoked

#### Workspace Deletion
1. Only possible if user is the sole Owner
2. All financial data is permanently deleted
3. Other users lose access immediately

## Consequences

### What becomes easier:
- **Clear Data Boundaries**: Complete separation prevents data leaks
- **Flexible Use Cases**: Supports personal, household, and business scenarios
- **Scalable Authorization**: Role-based access scales to complex scenarios
- **Family-Friendly**: Multiple family members can collaborate
- **Business-Ready**: Proper permission model for business use
- **Domain Agnostic Code**: Implementation can be reused for other multi-tenant applications
- **UI Flexibility**: Can change user-facing terminology without code changes
- **Developer Clarity**: Standard multi-tenancy patterns recognizable to any developer

### What becomes more complex:
- **Database Queries**: All queries must be Workspace-scoped
- **User Experience**: Users need to select/switch between Workspaces
- **Invitation Flow**: Need email invitations and acceptance workflow
- **Error Handling**: Need to handle Workspace access denied scenarios
- **Workspace Management UI**: Need Workspace settings, user management pages

### Migration Impact:
- **From YoFi V1/V2**: When migrating existing data, user will identify which Workspace to migrate it into 
- **New Features**: All new features must be Workspace-aware from day one
- **API Design**: All endpoints must include Workspace context

### Technical Implications:
- **Application Layer**: All features must accept TenantId parameter
- **Controllers**: Tenant-scoped authorization on all endpoints  
- **Frontend**: Workspace selection/switching UI component (maps to tenant backend)
- **Database**: Tenant foreign keys on all business entities
- **API Layer**: Uses tenant terminology for implementation clarity
- **UI Layer**: Uses workspace terminology for user experience

## Implementation Phases

### Phase 1: Single-User Workspaces (MVP)
- Full database schema implemented
- User invitation UI disabled (Workspaces are single-user by default)
- Workspace creation during user registration
- All financial features Workspace-scoped

### Phase 2: Multi-User Workspaces
- User invitation system
- Role-based permissions
- Workspace management UI

### Phase 3: Advanced Features
- Workspace transfer/ownership change
- Workspace archival/soft delete
- Audit logging for Workspace access

## Related Decisions

- [ADR 0008: Identity System](0008-identity.md) - Provides the authentication foundation for this Workspace model
- [ADR 0005: Database Backend](0005-database-backend.md) - SQLite database will store Workspace-scoped data

## Migration from current YoFi

All existing YoFi data (transactions, categories, budgets, etc.) will be migrated to a single Workspace that will be identified during the migration process. This ensures existing users can continue using their historical data within the new multi-Workspace structure.

## Questions for Review

1. **Is the three-role model sufficient?** (Owner/Editor/Viewer) 
   ✅ **Yes, this is perfect.**

2. **Should user preferences be Workspace-scoped or global?** 
   ✅ **Start with global preferences** for simplicity. There isn't an obvious need for preferences right now. Just to have something here, we could save light/dark mode theme switch.

3. **How should Workspace switching work in the UI?** 
   ✅ **Workspace selection page approach**: User visits "Workspaces" page and selects current Workspace. This context applies to all subsequent UI actions. Consider adding Workspace switcher in navigation for quick switching.

4. **Should there be a default Workspace concept for new users?** 
   ✅ **Yes, auto-provision personal Workspace**: New users automatically get a personal Workspace they own, eliminating empty state.

5. **How do we handle users with no Workspace access (edge case)?** 
   ✅ **Enable Workspace creation**: On empty "Workspaces" page, allow user to create new Workspace.

## Assorted Technical Details

- **Current Tenant**: The tenant which user is currently interacting with (displayed as "Current Workspace" in UI)
- **Default Tenant**: Stored on the backend, this is the tenant which is loaded as the current tenant when user first logs in (displayed as "Default Workspace" in UI)
- **Terminology Mapping**: 
  - Backend: Tenant, TenantId, ITenantService
  - Frontend: Workspace, WorkspaceId, useWorkspace()
  - Database: Tenants table, TenantId columns
  - UI: "Workspace" in all user-facing text


