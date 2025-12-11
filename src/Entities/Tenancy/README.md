# Tenancy Infrastructure

This directory contains the core tenancy infrastructure for the YoFi.V3 application, providing multi-tenant data isolation and role-based access control.

## Directory Structure

```
src/Entities/Tenancy/
├── Models/
│   ├── Tenant.cs                      # Tenant entity
│   ├── UserTenantRoleAssignment.cs    # User-tenant-role mapping
│   └── ITenantModel.cs                # Interface for tenant-scoped entities
│
├── Providers/
│   ├── ITenantProvider.cs             # Current tenant provider interface
│   └── ITenantRepository.cs           # Tenant data operations interface
│
├── Exceptions/
│   ├── TenancyException.cs            # Base exception
│   ├── TenancyAccessDeniedException.cs
│   ├── TenancyResourceNotFoundException.cs
│   ├── TenantAccessDeniedException.cs
│   ├── TenantContextNotSetException.cs
│   ├── TenantNotFoundException.cs
│   ├── DuplicateUserTenantRoleException.cs
│   └── UserTenantRoleNotFoundException.cs
│
└── README.md (this file)
```

## Purpose

This tenancy infrastructure provides:

1. **Multi-Tenant Data Isolation** - Each tenant's data is isolated from other tenants
2. **Role-Based Access Control** - Users can have different roles (Owner, Editor, Viewer) per tenant
3. **Tenant Context Management** - Track which tenant is active for each request
4. **Tenant-Scoped Entities** - Base types for entities that belong to a tenant

## Key Components

### Models/

**[`Tenant.cs`](Models/Tenant.cs)** - Represents a tenant (organization, workspace, etc.)
- Contains tenant metadata (Name, Description, CreatedAt)
- Tracks user role assignments via `RoleAssignments` navigation property

**[`UserTenantRoleAssignment.cs`](Models/UserTenantRoleAssignment.cs)** - Maps users to tenants with specific roles
- Defines `TenantRole` enum (Owner, Editor, Viewer) with hierarchical permissions
- Each user can have one role per tenant

**[`ITenantModel.cs`](Models/ITenantModel.cs)** - Interface for entities that belong to a tenant
- Defines `TenantId` property for tenant-scoped data
- Implemented by tenant-aware entities (e.g., Transaction)

### Providers/

**[`ITenantProvider.cs`](Providers/ITenantProvider.cs)** - Provides access to the current tenant
- Used by application features to get the active tenant context
- Implemented by `TenantContext` in the Controllers layer

**[`ITenantRepository.cs`](Providers/ITenantRepository.cs)** - Data access interface for tenants
- CRUD operations for tenants
- User-tenant role assignment management
- Implemented by `ApplicationDbContext` in the Data layer

### Exceptions/

Custom exceptions for tenant-related error conditions:
- `TenantNotFoundException` - Tenant doesn't exist
- `TenantAccessDeniedException` - User lacks access to tenant
- `DuplicateUserTenantRoleException` - Role assignment already exists
- `UserTenantRoleNotFoundException` - Role assignment not found
- And more...

## Usage Example

```csharp
// Tenant-scoped entity
public class Transaction : BaseTenantModel
{
    public string Payee { get; set; }
    public decimal Amount { get; set; }
    // TenantId inherited from BaseTenantModel
}

// Getting current tenant in a feature
public class TransactionsFeature(ITenantProvider tenantProvider)
{
    private readonly Tenant _currentTenant = tenantProvider.CurrentTenant;

    public async Task<IEnumerable<Transaction>> GetAllAsync()
    {
        // Automatically filtered by TenantId
        return await _dataProvider
            .Get<Transaction>()
            .Where(t => t.TenantId == _currentTenant.Id)
            .ToListAsync();
    }
}
```

## Future Plans

**⚠️ Note:** This tenancy infrastructure is being prepared for extraction into a separate, reusable NuGet package.

The goal is to create a standalone multi-tenancy library that can be used across multiple projects. This restructuring organizes the code by concern to make extraction easier:

- **Models/** - Core domain models (will be in separate package)
- **Providers/** - Abstraction interfaces (will be in separate package)
- **Exceptions/** - Domain exceptions (will be in separate package)

When extracted, applications will:
1. Reference the tenancy package
2. Implement the provider interfaces in their data layer
3. Configure tenancy in their web layer
4. Use the tenancy models and exceptions

## Related Documentation

- [Tenancy Documentation](../../../docs/TENANCY.md) - High-level tenancy concepts
- [ADR-0009: Accounts and Tenancy](../../../docs/adr/0009-accounts-and-tenancy.md) - Architecture decision record
- [Controllers Tenancy README](../../Controllers/Tenancy/README.md) - Web layer tenancy implementation

## Design Principles

1. **Separation of Concerns** - Models, providers, and exceptions are clearly separated
2. **Interface-Based** - Depends on abstractions (ITenantProvider, ITenantRepository) not concrete implementations
3. **Domain-Driven** - Entities and exceptions model the tenancy domain
4. **Reusable** - Designed to be extracted and reused in other projects
5. **Type-Safe** - Strong typing for tenant IDs and role enums
