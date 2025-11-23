# Tenancy Design Review

**Date**: 2025-11-23
**Reviewer**: Architecture Review
**Document Under Review**: [TENANCY-DESIGN.md](TENANCY-DESIGN.md)

## Executive Summary

This review addresses three key areas:
1. Missing questions and considerations
2. Evaluation of existing answers
3. Suggestions for open questions

Overall, the design is **solid and well-thought-out**. The separation of concerns, use of ASP.NET Core Identity integration, and tenant isolation strategy are appropriate. However, there are important areas that need addressing before implementation.

---

## 1. Missing Questions & Considerations

### 1.1 Migration and Onboarding

**Question**: How will existing single-user YoFi data migrate to the multi-tenant model?

**Consideration**: The ADR mentions "existing YoFi data migrates to single identified tenant" but the design document doesn't address:
- Migration script requirements
- How to identify which user should own migrated data
- What happens to anonymous/unowned data
- Migration rollback strategy

**Recommendation**: Add a "Data Migration" section covering:
```csharp
public class TenantMigration
{
    // How do we assign existing WeatherForecasts to a tenant?
    // Do we create a default tenant for each existing user?
    // Or a single "migrated data" tenant?
}
```

### 1.2 Tenant Creation Workflow

**Question**: What is the complete user journey for tenant creation?

**Missing details**:
- UI/API endpoint for creating tenants
- Validation rules (name uniqueness? character limits?)
- What happens on first login if user has no tenants?
- Auto-creation of "personal" tenant vs. manual creation

**Recommendation**: Document the user onboarding flow:
```
New User Registration → Auto-create Personal Tenant → Set as Default → Redirect to Dashboard
```

### 1.3 Claim Structure Format

**Question**: What is the exact structure of tenant role claims?

**Current state**: ADR shows `"entitlements": "tenant1_guid:owner,tenant2_guid:editor"` but doesn't specify:
- Claim type name (e.g., `"tenant_role"`, `"workspace"`, `"entitlement"`?)
- Single claim with comma-separated values vs. multiple claims?
- How to handle tenant GUID formatting in claims

**Recommendation**: Define explicit claim structure:
```csharp
// Option A: Multiple claims
{ "tenant_role": "00000000-0000-0000-0000-000000000001:Owner" }
{ "tenant_role": "00000000-0000-0000-0000-000000000002:Editor" }

// Option B: Single claim with delimiter
{ "tenant_roles": "00000000-0000-0000-0000-000000000001:Owner,00000000-0000-0000-0000-000000000002:Editor" }
```

### 1.4 Performance and Scalability

**Question**: How will tenant queries perform at scale?

**Missing considerations**:
- Database indexing strategy (covered partially with unique constraint)
- Query performance with many users per tenant
- Caching strategy for tenant role lookups
- Connection pooling with tenant-scoped DbContext

**Recommendation**: Add performance section addressing:
- Index on `UserTenantRoleAssignment.UserId` and `TenantId`
- Caching `IUserClaimsProvider` results
- Query optimization for tenant data filtering

### 1.5 Tenant Deletion and Soft Deletes

**Question**: Should tenants support soft deletes?

**Current state**: [`Tenant.IsActive`](TENANCY-DESIGN.md:26) property exists but isn't used in the design.

**Considerations**:
- Hard delete with `CASCADE` removes all data immediately
- Soft delete allows recovery period
- Compliance requirements (GDPR, data retention policies)
- Audit trail preservation

**Recommendation**: Clarify soft vs. hard delete strategy:
```csharp
public class Tenant
{
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedDate { get; set; }
    public string? DeletedByUserId { get; set; }
}
```

### 1.6 Invitation System Details

**Question**: How does the invitation system work technically?

**Referenced but not designed**: [Tenancy.feature](../tests/Functional/Features/Tenancy.feature) scenarios mention invitations, but design lacks:
- Invitation token generation/storage
- Email delivery mechanism
- Invitation acceptance flow
- Link format and security

**Recommendation**: Add "Invitation System" section with:
```csharp
public class TenantInvitation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string InvitedEmail { get; set; }
    public TenantRole ProposedRole { get; set; }
    public string InvitedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ExpiresDate { get; set; }
    public bool IsAccepted { get; set; }
    public string? AcceptedByUserId { get; set; }
}
```

### 1.7 Concurrency and Race Conditions

**Question**: How do we handle concurrent tenant operations?

**Missing considerations**:
- Two owners simultaneously removing each other
- Last owner attempting to leave/delete tenant
- Concurrent role changes
- Optimistic concurrency control

**Recommendation**: Add concurrency handling:
```csharp
public class Tenant
{
    [Timestamp]
    public byte[] RowVersion { get; set; }
}
```

### 1.8 Error Handling and User Feedback

**Question**: What error messages should users see for tenant violations?

**Missing details**:
- 403 vs 404 for unauthorized tenant access (security consideration)
- Error response format
- Client-side error handling guidance

**Recommendation**: Define error response schema:
```json
{
  "error": "TenantAccessDenied",
  "message": "You don't have permission to access this workspace",
  "tenantId": "guid",
  "requiredRole": "Editor"
}
```

### 1.9 Testing Strategy

**Question**: How will tenant isolation be tested?

**Consideration**: While functional tests exist, the design should specify:
- Unit testing tenant-scoped data providers
- Integration testing cross-tenant isolation
- Security testing for tenant bypass attempts

### 1.10 Tenant Metadata and Customization

**Question**: What additional tenant properties might be needed?

**Current minimal design** might need:
- Tenant description/notes
- Created by user ID
- Tenant settings/preferences
- Usage metrics (transaction count, storage size)
- Billing/subscription tier (if applicable)

---

## 2. Evaluation of Existing Answers

### ✅ EXCELLENT: Database Storage Model

**Answer**: Separate [`Tenant`](TENANCY-DESIGN.md:21) and [`UserTenantRoleAssignment`](TENANCY-DESIGN.md:32) entities

**Evaluation**: This is the correct approach. Clean separation of concerns, follows database normalization, and allows flexible many-to-many relationships.

**Strengths**:
- Guid IDs prevent enumeration attacks
- Role as enum with string conversion is type-safe yet readable
- Navigation properties support EF Core lazy loading

**Minor suggestion**: Consider adding audit fields:
```csharp
public class Tenant
{
    // ... existing properties
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? LastModifiedDate { get; set; }
}
```

### ✅ EXCELLENT: No Default Tenant in Backend

**Answer**: Default tenant is UI-only construct managed as user preference

**Evaluation**: Absolutely correct. This maintains backend statelessness and allows flexible client-side implementations.

**Reasoning**:
- Backend doesn't need to know/care about user preferences
- Supports multiple client types (web, mobile, API)
- Simplifies API design (explicit tenant in every request)

### ✅ GOOD: OnModelCreating Configuration

**Answer**: Standard EF Core fluent configuration

**Evaluation**: Generally good, but has **bugs that need fixing**:

**Bug 1** - Line 88: Property name mismatch
```csharp
// CURRENT (WRONG):
.HasForeignKey(uaa => uaa.AccountId)  // AccountId doesn't exist!

// SHOULD BE:
.HasForeignKey(uaa => uaa.TenantId)
```

**Bug 2** - Line 29: Navigation property mismatch
```csharp
// CURRENT (WRONG):
public virtual ICollection<UserAccountAccess> UserAccess { get; set; }

// SHOULD BE:
public virtual ICollection<UserTenantRoleAssignment> UserAccess { get; set; }
```

**Bug 3** - Line 92: Index references non-existent property
```csharp
// CURRENT (WRONG):
.HasIndex(uaa => new { uaa.UserId, uaa.AccountId })

// SHOULD BE:
.HasIndex(uaa => new { uaa.UserId, uaa.TenantId })
```

**Enhancement**: Add required field validation:
```csharp
entity.Property(a => a.CreatedDate)
    .IsRequired();

entity.Property(uaa => uaa.UserId)
    .IsRequired()
    .HasMaxLength(450); // Standard Identity user ID length
```

### ⚠️ NEEDS CLARIFICATION: ApplicationUser Navigation Properties

**Question**: Should [`ApplicationUser`](TENANCY-DESIGN.md:106) have navigation to tenant role assignments?

**Current answer**: ⚠️ Open question

**My recommendation**: **YES, add the navigation property**

**Rationale**:
1. **EF Core best practice**: Bidirectional navigation helps with lazy loading and queries
2. **Useful queries**: `user.TenantRoleAssignments.Where(t => t.Role == TenantRole.Owner)`
3. **Cascade behavior**: Makes EF Core properly handle cascade deletes
4. **No downside**: Even if not used directly, it doesn't hurt

**Implementation**:
```csharp
// Create custom ApplicationUser
public class ApplicationUser : IdentityUser
{
    public virtual ICollection<UserTenantRoleAssignment> TenantRoleAssignments { get; set; }
        = new List<UserTenantRoleAssignment>();
}

// Update DbContext
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>

// Update OnModelCreating
entity.HasOne(uaa => uaa.User)
    .WithMany(u => u.TenantRoleAssignments)
    .HasForeignKey(uaa => uaa.UserId)
    .OnDelete(DeleteBehavior.Cascade);
```

### ✅ EXCELLENT: Correlating Application Data Types

**Answer**: Each entity has `TenantId` foreign key

**Evaluation**: Perfect approach for tenant isolation. Simple, explicit, and efficient.

**Strengths**:
- Clear ownership model
- Easy to query and filter
- Supports CASCADE delete for data cleanup
- Index-friendly for performance

**Enhancement**: Consider base class:
```csharp
public abstract class TenantScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // Navigation property
    public virtual Tenant Tenant { get; set; } = null!;
}

public class Transaction : TenantScopedEntity
{
    // Just business properties
}
```

### ⚠️ NEEDS CLARIFICATION: Derived Tenant with Navigation Properties

**Question**: Should application code derive from [`Tenant`](TENANCY-DESIGN.md:150)?

**Current answer**: ⚠️ Open question

**My recommendation**: **NO, don't derive. Use composition instead.**

**Rationale**:
1. **Single Responsibility**: Core `Tenant` entity should be domain-agnostic
2. **Reusability**: Keeps tenancy system portable across projects
3. **Separation of Concerns**: Application-specific navigation properties belong in application-specific entities

**Better approach**:
```csharp
// Keep Tenant generic
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // ... generic properties only
}

// Application entities reference Tenant
public class Transaction
{
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

// If you need app-specific tenant data, create separate entity
public class TenantSettings
{
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public string? DefaultCurrency { get; set; }
    public int FiscalYearStartMonth { get; set; }
}
```

### ✅ EXCELLENT: Communication to Client via Claims

**Answer**: Implement [`IUserClaimsProvider<TUser>`](../submodules/NuxtIdentity/src/Core/Abstractions/IUserClaimsProvider.cs) for tenant roles

**Evaluation**: Perfect integration with NuxtIdentity architecture.

**Strengths**:
- Leverages existing JWT infrastructure
- Claims available both in token and user endpoint
- Type-safe with interface contract
- Testable with dependency injection

**Implementation guidance**:
```csharp
public class TenantClaimsProvider : IUserClaimsProvider<ApplicationUser>
{
    private readonly ApplicationDbContext _context;

    public async Task<IEnumerable<Claim>> GetClaimsAsync(ApplicationUser user)
    {
        var assignments = await _context.UserTenantRoleAssignments
            .Where(utra => utra.UserId == user.Id)
            .Include(utra => utra.Tenant)
            .Where(utra => utra.Tenant.IsActive)
            .ToListAsync();

        return assignments.Select(a =>
            new Claim("tenant_role", $"{a.TenantId}:{a.Role}"));
    }
}
```

### ✅ EXCELLENT: ASP.NET Authorization Policies

**Answer**: Use policy-based authorization for tenant role enforcement

**Evaluation**: Correct approach, follows ASP.NET Core best practices.

**Strengths**:
- Declarative authorization
- Centralized security logic
- Testable and maintainable

### ⚠️ NEEDS CLARIFICATION: Policy Attributes Implementation

**Question**: How exactly are policy attributes written?

**Current answer**: ⚠️ Open question

**My recommendation**: Use **custom authorization requirement and handler**

**Implementation**:
```csharp
// 1. Define the requirement
public class TenantRoleRequirement : IAuthorizationRequirement
{
    public TenantRole MinimumRole { get; }

    public TenantRoleRequirement(TenantRole minimumRole)
    {
        MinimumRole = minimumRole;
    }
}

// 2. Define the handler
public class TenantRoleHandler : AuthorizationHandler<TenantRoleRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        // Extract tenant ID from route
        var httpContext = _httpContextAccessor.HttpContext;
        var tenantId = httpContext?.Request.RouteValues["tenantId"]?.ToString();

        if (string.IsNullOrEmpty(tenantId))
        {
            return Task.CompletedTask; // Fail - no tenant in route
        }

        // Check user's claims for this tenant
        var tenantRoleClaim = context.User.FindFirst(c =>
            c.Type == "tenant_role" &&
            c.Value.StartsWith($"{tenantId}:"));

        if (tenantRoleClaim == null)
        {
            return Task.CompletedTask; // Fail - no access to this tenant
        }

        // Parse role and check against requirement
        var parts = tenantRoleClaim.Value.Split(':');
        if (parts.Length == 2 && Enum.TryParse<TenantRole>(parts[1], out var userRole))
        {
            if (userRole >= requirement.MinimumRole)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}

// 3. Create attribute for easy use
public class RequireTenantRoleAttribute : AuthorizeAttribute
{
    public RequireTenantRoleAttribute(TenantRole minimumRole)
    {
        Policy = $"TenantRole_{minimumRole}";
    }
}

// 4. Register in DI
services.AddAuthorization(options =>
{
    foreach (TenantRole role in Enum.GetValues<TenantRole>())
    {
        options.AddPolicy($"TenantRole_{role}", policy =>
            policy.Requirements.Add(new TenantRoleRequirement(role)));
    }
});

services.AddSingleton<IAuthorizationHandler, TenantRoleHandler>();
```

### ⚠️ NEEDS IMPLEMENTATION DETAIL: AddTenantPolicies Service Extension

**Question**: What does [`AddTenantPolicies`](TENANCY-DESIGN.md:213) look like?

**Current answer**: ⚠️ Open question

**My recommendation**:
```csharp
public static class TenantPolicyServiceExtensions
{
    public static IServiceCollection AddTenantPolicies(this IServiceCollection services)
    {
        // Register authorization handler
        services.AddSingleton<IAuthorizationHandler, TenantRoleHandler>();

        // Register HTTP context accessor (needed for route value access)
        services.AddHttpContextAccessor();

        // Configure authorization options
        services.AddAuthorizationBuilder()
            .AddPolicy("TenantViewer", policy =>
                policy.Requirements.Add(new TenantRoleRequirement(TenantRole.Viewer)))
            .AddPolicy("TenantEditor", policy =>
                policy.Requirements.Add(new TenantRoleRequirement(TenantRole.Editor)))
            .AddPolicy("TenantOwner", policy =>
                policy.Requirements.Add(new TenantRoleRequirement(TenantRole.Owner)));

        return services;
    }
}

// Usage in Program.cs
builder.Services.AddTenantPolicies();
```

### ✅ EXCELLENT: Tenant ID in HTTP Request Route

**Answer**: Tenant-scoped resources at `/api/tenant/{tenantId}/...`

**Evaluation**: Perfect. Makes tenant context explicit and supports RESTful API design.

**Strengths**:
- Self-documenting URLs
- Easy to extract in middleware/handlers
- Supports API versioning
- Clear tenant boundaries

**Enhancement**: Consider route constraint:
```csharp
[Route("api/tenant/{tenantId:guid}/[controller]")]
```

### ⚠️ CRITICAL: Data Provider Tenant Scope

**Answer**: Extend [`IDataProvider`](../src/Entities/Providers/IDataProvider.cs) with tenant-scoped version

**Evaluation**: **This is the most complex part of the design and needs careful consideration.**

**Concerns with current approach**:
1. When to create the scoped provider?
2. How to pass it to application features?
3. How to prevent bypassing the scoping?

**My recommendation**: **Use ASP.NET Core scoped services with ambient context**

**Better approach**:
```csharp
// 1. Define tenant context service
public interface ITenantContext
{
    Guid TenantId { get; }
    TenantRole Role { get; }
}

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; set; }
    public TenantRole Role { get; set; }
}

// 2. Create middleware to set context
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Extract from route (already validated by authorization)
        if (context.Request.RouteValues.TryGetValue("tenantId", out var tenantIdValue) &&
            Guid.TryParse(tenantIdValue?.ToString(), out var tenantId))
        {
            var tenantContextImpl = (TenantContext)tenantContext;
            tenantContextImpl.TenantId = tenantId;

            // Extract role from claims
            var roleClaim = context.User.FindFirst(c =>
                c.Type == "tenant_role" &&
                c.Value.StartsWith($"{tenantId}:"));

            if (roleClaim != null)
            {
                var parts = roleClaim.Value.Split(':');
                if (parts.Length == 2 && Enum.TryParse<TenantRole>(parts[1], out var role))
                {
                    tenantContextImpl.Role = role;
                }
            }
        }

        await _next(context);
    }
}

// 3. Create tenant-aware data provider
public class TenantDataProvider : IDataProvider
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public TenantDataProvider(ApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel
    {
        var query = _context.Set<TEntity>();

        // If entity is tenant-scoped, filter automatically
        if (typeof(TEntity).GetProperty("TenantId") != null)
        {
            // Use EF.Property for dynamic filtering
            query = query.Where(e =>
                EF.Property<Guid>(e, "TenantId") == _tenantContext.TenantId);
        }

        return query;
    }

    public void Add(IModel item)
    {
        // Auto-set tenant ID on new items
        if (item.GetType().GetProperty("TenantId") != null)
        {
            item.GetType().GetProperty("TenantId")!.SetValue(item, _tenantContext.TenantId);
        }
        _context.Add(item);
    }

    // ... other methods
}

// 4. Register in DI
services.AddScoped<ITenantContext, TenantContext>();
services.AddScoped<IDataProvider, TenantDataProvider>();

// 5. Add middleware AFTER authorization
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
```

**Benefits**:
- Automatic tenant filtering on all queries
- Automatic tenant ID assignment on creates
- Impossible to forget tenant filtering
- Testable with mock ITenantContext
- Clean separation of concerns

---

## 3. Suggestions for Open Questions

### 3.1 User Navigation Properties → YES

**Recommendation**: Add `ApplicationUser` with navigation properties

See detailed implementation in section 2 under "ApplicationUser Navigation Properties"

### 3.2 Derived Tenant → NO

**Recommendation**: Keep `Tenant` generic, use composition for app-specific data

See detailed rationale in section 2 under "Derived Tenant with Navigation Properties"

### 3.3 Policy Attributes → Custom Authorization Handler

**Recommendation**: Implement `IAuthorizationRequirement` and handler

See complete implementation in section 2 under "Policy Attributes Implementation"

### 3.4 AddTenantPolicies → Service Collection Extension

**Recommendation**: Create extension method that registers all tenant services

```csharp
public static IServiceCollection AddTenantServices(this IServiceCollection services)
{
    // 1. Register entities and context (already done elsewhere)

    // 2. Register tenant context
    services.AddScoped<ITenantContext, TenantContext>();

    // 3. Register tenant-aware data provider
    services.AddScoped<IDataProvider, TenantDataProvider>();

    // 4. Register claims provider
    services.AddScoped<IUserClaimsProvider<ApplicationUser>, TenantClaimsProvider>();

    // 5. Register authorization
    services.AddSingleton<IAuthorizationHandler, TenantRoleHandler>();
    services.AddHttpContextAccessor();

    services.AddAuthorizationBuilder()
        .AddPolicy("TenantViewer", policy =>
            policy.Requirements.Add(new TenantRoleRequirement(TenantRole.Viewer)))
        .AddPolicy("TenantEditor", policy =>
            policy.Requirements.Add(new TenantRoleRequirement(TenantRole.Editor)))
        .AddPolicy("TenantOwner", policy =>
            policy.Requirements.Add(new TenantRoleRequirement(TenantRole.Owner)));

    return services;
}
```

### 3.5 Tenant Data Provider Creation → Scoped Service with Middleware

**Recommendation**: Use scoped DI + middleware approach

The key insight: **Don't create the provider in middleware. Let DI handle it.**

**Architecture**:
```
Request → Authentication → Authorization → TenantContextMiddleware → Controller → Feature
                                              ↓
                                        Sets ITenantContext
                                              ↓
                                        TenantDataProvider (scoped)
                                              ↓
                                        Auto-filters by TenantId
```

**Controller usage** (no changes needed!):
```csharp
[Route("api/tenant/{tenantId:guid}/[controller]")]
[RequireTenantRole(TenantRole.Editor)]
public class TransactionController : ControllerBase
{
    private readonly TransactionFeature _feature;

    // IDataProvider is already tenant-scoped via DI
    public TransactionController(TransactionFeature feature)
    {
        _feature = feature;
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions()
    {
        // Feature uses IDataProvider, which is automatically tenant-scoped
        var transactions = await _feature.GetAllTransactions();
        return Ok(transactions);
    }
}
```

**Feature usage** (minimal changes):
```csharp
public class TransactionFeature
{
    private readonly IDataProvider _dataProvider; // Already tenant-scoped!

    public TransactionFeature(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public async Task<List<Transaction>> GetAllTransactions()
    {
        // This query is automatically filtered to current tenant
        var query = _dataProvider.Get<Transaction>()
            .OrderByDescending(t => t.Date);

        return await _dataProvider.ToListAsync(query);
    }
}
```

---

## 4. Additional Architectural Recommendations

### 4.1 Consider Global Query Filters

EF Core's global query filters can provide an additional safety layer:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // This would require ITenantContext injection into DbContext
    modelBuilder.Entity<Transaction>()
        .HasQueryFilter(t => t.TenantId == _tenantContext.TenantId);
}
```

**Pros**: Impossible to forget filtering
**Cons**: Harder to bypass when legitimately needed (e.g., admin operations)

### 4.2 Audit Logging for Tenant Operations

Consider logging all tenant management operations:

```csharp
public class TenantAuditLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Action { get; set; } = string.Empty; // "RoleChanged", "UserAdded", "UserRemoved"
    public string ActorUserId { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### 4.3 Health Checks for Tenant Isolation

Add health check to verify tenant isolation:

```csharp
public class TenantIsolationHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        // Verify no cross-tenant data leaks
        // Check that queries are properly filtered
        // Validate foreign key constraints
    }
}
```

---

## 5. Summary and Next Steps

### Overall Assessment: ✅ STRONG FOUNDATION

The design demonstrates solid understanding of multi-tenancy patterns and ASP.NET Core best practices. Key strengths:
- Clean entity model
- Proper use of EF Core relationships
- Integration with existing NuxtIdentity infrastructure
- Security-first approach with authorization policies

### Critical Issues to Address:

1. **Fix bugs** in [`OnModelCreating`](TENANCY-DESIGN.md:66) (AccountId → TenantId)
2. **Implement ITenantContext** scoped service pattern for data provider
3. **Define claim structure** explicitly
4. **Add invitation system** design
5. **Document migration** strategy

### Recommended Implementation Order:

1. **Phase 1: Core Infrastructure**
   - [ ] Fix entity model bugs
   - [ ] Create `ApplicationUser` with navigation properties
   - [ ] Implement `ITenantContext` service
   - [ ] Create `TenantDataProvider` with automatic filtering
   - [ ] Add `TenantContextMiddleware`

2. **Phase 2: Authorization**
   - [ ] Implement `TenantClaimsProvider`
   - [ ] Create authorization requirement and handler
   - [ ] Add `RequireTenantRole` attribute
   - [ ] Create `AddTenantServices` extension

3. **Phase 3: User Features**
   - [ ] Tenant CRUD operations
   - [ ] User invitation system
   - [ ] Role management
   - [ ] Default tenant preferences

4. **Phase 4: Testing & Polish**
   - [ ] Tenant isolation tests
   - [ ] Cross-tenant security tests
   - [ ] Migration scripts
   - [ ] Documentation

---

## 6. Conclusion

This is a well-conceived design that will successfully implement multi-tenancy for YoFi.V3. The main work ahead is:

1. **Resolving the open questions** (this review provides specific recommendations)
2. **Fixing the identified bugs** in the entity configuration
3. **Implementing the scoped data provider pattern** using DI and middleware
4. **Adding the invitation system** design

The suggested `ITenantContext` + `TenantDataProvider` + middleware approach solves the most complex part of the design (tenant-scoped data access) in a clean, testable, and maintainable way.

**Recommendation**: Proceed with implementation using the guidance in this review. The foundation is solid; the details just need to be filled in.
