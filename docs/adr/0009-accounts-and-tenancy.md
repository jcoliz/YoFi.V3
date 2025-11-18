# 0009. Multi-tenancy and Workspace model

Date: 2025-11-13

## Status

Accepted

## Context

### Question

How should YoFi.V3 handle multiple users and financial data isolation? What is the relationship between users and their financial data boundaries?

### Background

YoFi.V3 is a rewrite of the [YoFi personal finance application](https://github.com/jcoliz/yofi). YoFi is single-tenant. This is a constraint I would like to improve upon with this rewrite.

### Use Case Analysis

**Primary Use Cases:**
- **Personal Finance**: Individual manages their own financial data
- **Household Finance**: Family members share access to household financial data  
- **Small Business**: Business owner + bookkeeper access business financial data
- **Multi-Workspace**: User manages both personal and business finances separately

## Decision

### Terminology: Implementation vs. User Interface

**Implementation Layer**: Uses "Tenant" terminology throughout the codebase
- Database: `Tenants`, `UserTenantRoles`, `TenantId` columns
- Code: `Tenant`, `ITenantService`, `TenantContext` classes
- APIs: `/api/tenant/{tenantId}/transactions`

**User Interface Layer**: Uses "Workspace" terminology for end users
- UI components: Workspace selector, workspace switcher
- All user-facing text and documentation

**Rationale**: Separates domain-agnostic implementation from user experience, allowing UI terminology to evolve based on user testing while keeping code reusable.

### Tenant Model: Logical Financial Boundary

A "Workspace" (UI) / "Tenant" (implementation) represents a complete set of financial records (transactions, budgets, categories, etc.) managed as a unit.

Examples:
- "Smith Family Finances" (household)
- "John's Personal Finances" (individual)  
- "ABC Consulting Business" (business)

### Multi-Tenancy: Role-Based Access Control

**User-to-Tenant Relationships:**
- **One User → Multiple Tenants**: Personal + business finances
- **Multiple Users → One Tenant**: Family members, business owner + bookkeeper

**Role Levels:**
1. **Owner** - Full control (edit data, manage users, delete tenant)
2. **Editor** - Edit financial data, view reports (cannot manage users)
3. **Viewer** - Read-only access to data and reports

**Rules:**
- Tenant creation makes creator the Owner
- Only Owners can invite/remove users and change roles
- Each tenant must have at least one Owner
- Financial data is completely isolated by tenant

### Database Schema

```sql
-- Users (from ASP.NET Core Identity)
Users (Id, Email, UserName)

-- Tenant entity (called "Workspace" in UI)
Tenants (Id, Name, IsActive)

-- User-to-Tenant relationship with roles
UserTenantRoles (Id, UserId, TenantId, Role)

-- All financial data is tenant-scoped
Transactions (Id, TenantId, Date, Amount, Description, Source)
Categories (Id, TenantId, Name)
Budgets (Id, TenantId, Month, Amount)

-- User preferences (global, not tenant-scoped)
UserPreferences (Id, UserId, DefaultTenantId, Theme)
```

### Authorization & APIs

**JWT Claims:**
```json
{
  "sub": "user123",
  "email": "john@example.com", 
  "entitlements": "tenant1_guid:owner,tenant2_guid:editor"
}
```

**API Structure:**
```
/api/tenant/{tenantId}/transactions
/api/tenant/{tenantId}/categories
/api/tenant/{tenantId}/budgets
```

**Policies:**
- **TenantView**: Viewer, Editor, or Owner role required
- **TenantEdit**: Editor or Owner role required
- **TenantOwn**: Owner role required

### Transaction Source Tracking

Each transaction includes a `Source` field identifying origin:
- `"Chase Freedom Credit Card"`
- `"Wells Fargo Checking"`
- `"Cash"`, `"Venmo"`, `"Manual Entry"`

Benefits: Simple implementation, natural language, import-friendly, flexible.

## Implementation Phases

**Phase 1 (MVP)**: Single-user tenants, full schema, all features tenant-scoped
**Phase 2**: Multi-user tenants, invitation system, role management UI  
**Phase 3**: Advanced features (transfer ownership, audit logging)

## Consequences

### Easier:
- Clear data boundaries prevent leaks
- Supports personal, household, and business scenarios
- Role-based access scales to complex use cases
- Domain-agnostic code reusable for other applications
- UI terminology can evolve without code changes

### More Complex:
- All queries must be tenant-scoped
- Users need tenant selection/switching UI
- Email invitation and acceptance workflow required
- Tenant access denied error handling

## Related Decisions

- [ADR 0008: Identity System](0008-identity.md) - Authentication foundation
- [ADR 0005: Database Backend](0005-database-backend.md) - SQLite storage

## Technical Details

- **Current Tenant**: Active tenant user is interacting with (UI: "Current Workspace")
- **Default Tenant**: Loaded on login, updated on switch (UI: "Default Workspace")  
- **New User Flow**: Auto-create personal tenant → redirect to dashboard
- **Migration**: Existing YoFi data migrates to single identified tenant