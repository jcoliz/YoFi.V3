# Multi-tenancy database design

**Abstract**: This document describes the design in progress for the Tenancy feature in YoFi.V3

**Goal**: Design a domain-independent system where user data can be aggregated into "Tenants", with separate user role assignment. Would like to be able to resuse this in other applications.

**Reference**: This is designed to implement [ADR 0009](./adr/0009-accounts-and-tenancy.md). In case of conflict, assume the ADR is out of data, or needlessly specific. This design is the latest thinking.

**Dependencies**: .NET 10, Entity Framework Core, ASP.NET Core Identity, and [NuxtIdentity](https://www.github.com/jcoliz/NuxtIdentity)

## Entity Models

### Database storage

**Question**: What data is stored in the database?

- Tenant: Description of the tenant itself
- UserTenantRoleAssignment: Assignments of roles to users on a certain tenant

For both of these models, we are storing a minimum of data to get started. As the application evolves, we can add additional functionality to meet future needs.

```csharp
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime DeactivatedDate { get; set; }
    public string? DeactivatedByUserId { get; set; }

    // Navigation properties
    public virtual ICollection<UserTenantRoleAssignment> UserAccess { get; set; } = new List<UserAccountAccess>();
}

public class UserTenantRoleAssignment
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public TenantRole Role { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}

public enum TenantRole
{
    Viewer = 1,
    Editor = 2,
    Owner = 3
}
```

### No default tenant

**Question**: What about default tenant?

We will manage default tenant as a user preference. Default tenant is only a UI construct. It has no
meaning on the backend.

## Database Configuration

### OnModelCreating setup

**Question**: What setup must be done on the database side?

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Tenant entity configuration
    modelBuilder.Entity<Tenant>(entity =>
    {
        entity.HasKey(a => a.Id);

        entity.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(a => a.CreatedDate)
            .IsRequired();

    // UserTenantRoleAssignment entity configuration
    modelBuilder.Entity<UserTenantRoleAssignment>(entity =>
    {
        entity.HasKey(uaa => uaa.Id);

        entity.Property(uaa => uaa.UserId)
            .IsRequired()
            .HasMaxLength(450); // Standard Identity user ID length
            });

        // Tenant relationship
        entity.HasOne(uaa => uaa.Tenant)
            .WithMany(a => a.UserAccess)
            .HasForeignKey(uaa => uaa.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one user can have only one role per account
        entity.HasIndex(uaa => new { uaa.UserId, uaa.TenantId })
            .IsUnique();

        // Convert enum to string in database
        entity.Property(uaa => uaa.Role)
            .HasConversion<string>();
    });
```

### User navigation properties

**Question**: Is an application specific ApplicationUser necessary with navigation property to the user's tenant roles?

Yes, we will create an application user to add helpful navigation properties. This is always handled at the application level, so doesnt interfere with anything the libary is doing.

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

### Correlating application data types

**Question**: How will we correlate to Tenants in application code?

Each domain-specific tenant-constrained data type will have a `TenantId` property. For example, consider a `Transaction` entity:

```csharp
public interface ITenantModel
{
    public Guid TenantId { get; set; }
}

public class Transaction: ITenantModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    // ... Other application specific properties

}

// Financial entities (account-scoped)
modelBuilder.Entity<Transaction>(entity =>
{
    entity.HasOne<Tenant>()
        .WithMany()
        .HasForeignKey(t => t.TenantId)
        .OnDelete(DeleteBehavior.Cascade);
});
}
```

### No tenant navigation properties

**Question**: In application code, should I derive from Tenant, and add navigation properties to all

No. In order to add navigation properties to the tenant entity, it would couple `Tenant` to the specific application domain.

**Rationale**:
1. **Single Responsibility**: Core `Tenant` entity should be domain-agnostic
2. **Reusability**: Keeps tenancy system portable across projects
3. **Separation of Concerns**: Application-specific navigation properties belong in application-specific entities

## Tenant management

There are a number of user stories affecting tenants and tenant role assignments. These will all be handled the same way I handle application data: with a Tenant Feature, exposed to API via Tenant Controller.

These endpoints generally take the form: `/tenant/{TenantId:guid}/user/{UserId}/role/{role}`

In normal circumstances, all users are provisioned with a tenant they own when their user account is approved in the system. If user logs on and has no tenant, they'll need to contact a site administrator to solve this.

```
New User creates account → Verifies email → Approved by administrator → Auto-create Personal Tenant → Set as Default (in UI) → Redirect to Dashboard
```

Regular User can:
- Add a role assignment at any level for another user on a tenant they own.
- Remove a role assignment (except owner) for another user on a tenant they own
- Remove a role assignment for themselves on any tenant.
- Deactivate a tenant they own, as long as they're the only owner
- Activate a tenant they own

Power user can: (This is a site-wide user account role)
- Create a new tenant, which they now own.

Administator can: (This is a site-wide user account role)
- Approve a new user
- Deactivate/reactivate a user
- Set any user account role on any user
- Set any user any role on any tenant
- Deactivate any tenant
- Hard (permanantly) delete a tenant and its data. To avoid mishaps, we will require that it has been disabled for at least 1 week first.

## Communication to client

### Methodology

**Question**: How do available tenants & role assignments get to the client?

NuxtIdentity returns claims both in the JWT access token, and in the user data structure. We will implement a tenant manager.
It can implement `IUserClaimsProvider<TUser>`, providing tenant role assignments for the current user as claims.

### Format

**Question**: What is the exact structure of tenant role claims?

For initial simplicity, each UserTenantRoleAssignment will be add as a single claim to both the front-end and JWT user tokwn.

```csharp
new Claim("tenant_role", "00000000-0000-0000-0000-000000000001:Owner")
```

## Authentication

**Question**: How does the API endpoint enforce tenant role assignment?

We'll use ASP.NET authorization policies

### Controller decoration

**Question** How does a controller declare a tenant role requirement

We will implement policy attributes which can be added to a controller, e.g.

```csharp
    [HttpGet]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(WeatherForecast[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWeatherForecasts() {}
```

The middleware will extract the requested tenant from the HTTP request, and then validate that the claims principal
for the request has at least the indicated role.

### TenantId in HTTP request

**Question**: Where will the middleware look to find the requested tenant?

Tenant-scoped resources are served on `/api/tenant/` endpoints, e.g.

```csharp
[Route("api/tenant/{tenantId:guid}/[controller]")]
```

### Tenant permission errors

**Question**: What error messages should users see for tenant violations?

1. **No tenant access** → **404 Not Found**
   - Hides tenant existence
   - Prevents enumeration

2. **Has access, insufficient role** → **403 Forbidden** with detailed message
   - User already knows tenant exists
   - Clear feedback helps legitimate users

3. **Invalid tenant ID format** → **400 Bad Request**
   - Not a security issue
   - Invalid GUID format

### Policy attributes

**Question**: How exactly is the `RequireTenantRole` attribute written?

The additional document [TENANCY-POLICY-HOWTO](./TENANCY-POLICY-HOWTO.md) and [TENANCY-POLICY-CLARIFICATION](./TENANCY-POLICY-CLARIFICATION.md) go into great detail on this.

We will create a specialized `AuthorizeAttribute`, which sets an authorization policy.

```csharp
public class RequireTenantRoleAttribute : AuthorizeAttribute
{
    public RequireTenantRoleAttribute(TenantRole minimumRole)
    {
        // THIS IS THE KEY LINE - Sets the policy name string
        Policy = $"TenantRole_{minimumRole}";
    }
}
```

The custom attribute is just syntactic sugar. The following two forms are the same. The first version, which we've enabled by including the above `AuthorizeAttribute`, allows for more expressive and type-safe code.

```csharp
[RequireTenantRole(TenantRole.Editor)]  // Internally does: Policy = "TenantRole_Editor"
public async Task<IActionResult> GetTransactions() { }

[Authorize(Policy = "TenantRole_Editor")]
public async Task<IActionResult> GetTransactions() { }

```

### Authorization policies

**Question**: How are the policies defined

The `RequireTenantRole` attribute will set the policy, but we still need to define the policy!

```csharp
public class TenantRoleRequirement : IAuthorizationRequirement
{
    public TenantRole MinimumRole { get; }
    public TenantRoleRequirement(TenantRole minimumRole) => MinimumRole = minimumRole;
}

public class TenantRoleHandler : AuthorizationHandler<TenantRoleRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantRoleHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        // Extract tenant ID from route
        var tenantId = _httpContextAccessor.HttpContext?
            .Request.RouteValues["tenantId"]?.ToString();

        if (string.IsNullOrEmpty(tenantId))
            return Task.CompletedTask; // Fail

        // Check user's claims for this tenant's role
        var claim = context.User.FindFirst(c =>
            c.Type == "tenant_role" &&
            c.Value.StartsWith($"{tenantId}:"));

        if (claim != null)
        {
            var parts = claim.Value.Split(':');
            if (parts.Length == 2 &&
                Enum.TryParse<TenantRole>(parts[1], out var userRole) &&
                userRole >= requirement.MinimumRole)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
```

One note, I don't love the use of simple arithmetic for determining if a role is
"equal to or better than". I will want something more specific.

### Policy attribute access to tenant roles

**Question**: How do the policy attributes get access to tenant role assignments?

They are simply looking at the claims in the claims principal.

### Adding policy into application

**Question**: How are the policies added into the DI container?

There will be an `AddTenantPolicies` service collection extension.

**Question**: What does that look like exactly?

We inject into the DI container when setting it in up `Program.cs`. Of course we will provide a service collection extension for this.

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationHandler, TenantRoleHandler>();

builder.Services.AddAuthorization(options =>
{
    // Register each policy NAME that the attribute might create
    foreach (TenantRole role in Enum.GetValues<TenantRole>())
    {
        // This creates: "TenantRole_Viewer", "TenantRole_Editor", "TenantRole_Owner"
        options.AddPolicy($"TenantRole_{role}", policy =>
            policy.Requirements.Add(new TenantRoleRequirement(role)));
    }
});
```

## Tenant-scoped data provider

**Question**: How do we restrict application features to only the tenant and role assignment we've previously validated?

1. Authorization middleware will leave the tenantid and highest claimed tenant role on the HTTP Context `Items`
2. Create a new tenant-scoped data provider, `TenantDataProvider:ITenantDataProvider`, which takes `IHttpContextAccessor` as its constructor. It will pull the tenant and highest role from the context items, and craft a limited
3. Tenant-scoped feature services use `ITenantDataProvider` instead of the more general-purpose `IDataProvider`.

### Setting Tenant Context on HTTP Context Items

```csharp
public class TenantRoleHandler : AuthorizationHandler<TenantRoleRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var tenantId = httpContext?.Request.RouteValues["tenantId"]?.ToString();

        if (string.IsNullOrEmpty(tenantId))
            return Task.CompletedTask;

        var claim = context.User.FindFirst(c =>
            c.Type == "tenant_role" &&
            c.Value.StartsWith($"{tenantId}:"));

        if (claim != null)
        {
            var parts = claim.Value.Split(':');
            if (parts.Length == 2 &&
                Enum.TryParse<TenantRole>(parts[1], out var userRole) &&
                userRole >= requirement.MinimumRole)
            {
                // STORE IT in HttpContext.Items for later use
                httpContext.Items["TenantId"] = Guid.Parse(tenantId);
                httpContext.Items["TenantRole"] = userRole;

                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
```

### Creating tenant-scoped data provider

```csharp
// 3. Create tenant-aware data provider
public class TenantDataProvider : IDataProvider
{
    private readonly ApplicationDbContext _context;
    private readonly Guid _tenantId;

    public TenantDataProvider(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _tenantId = (Guid?)_httpContextAccessor.HttpContext?.Items["TenantId"];

    }

    public IQueryable<TEntity> Get<TEntity>() where TEntity : class, ITenantModel
    {
        var query = _context.Set<TEntity>();

        query = query.Where(e => e.TenantId == _tenantId);

        return query;
    }

    public void Add(ITenantModel item)
    {
        item.TenantId = _tenantId;
        _context.Add(item);
    }

    // ... other methods
}

```

## Migration of existing data

**Question**: How will existing single-user YoFi data migrate to the multi-tenant model?

Single-user YoFi will export their data using that application's exsiting export process, then import it using the coming import functionality in YoFi.V3.

## Invitation System

**Question**: How does the invitation system work specifically?

Invitations are out of scope for this feature. They will be a separate "Invitations" feature.

## Future Topics

There are some questions which I'd consider in scope, but will think more about these once I have the initial design proven out.

### Performance and Scalability

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

### Concurrency and Race Conditions

**Question**: How do we handle concurrent tenant operations?

**Missing considerations**:
- Two owners simultaneously removing each other
- Last owner attempting to leave/delete tenant
- Concurrent role changes
- Optimistic concurrency control

### Testing Strategy

**Question**: How will tenant isolation be tested?

**Consideration**: While functional tests are specified, the design should specify:
- Unit testing tenant-scoped data providers
- Integration testing cross-tenant isolation
- Security testing for tenant bypass attempts
