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

```csharp
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<UserTenantRoleAssignment> UserAccess { get; set; } = new List<UserAccountAccess>();
}

public class UserTenantRoleAssignment
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public AccountRole Role { get; set; }

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
            .HasMaxLength(200);

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

### User navigation properties [???]

**Question**: Do I need to make an application specific ApplicationUser with navigation property to the user's tenant roles? For example, something like:

```csharp
public class ApplicationUser: IdentityUser
{
    // ... other properties

    // Navigation properties
    public virtual ICollection<UserTenantRoleAssignment> TenantRoleAssignments { get; set; } = new List<UserTenantRoleAssignment>();
}

        // User relationship
        entity.HasOne(uaa => uaa.User)
            .WithMany(u => u.TenantRoleAssignments)
            .HasForeignKey(uaa => uaa.UserId)
            .OnDelete(DeleteBehavior.Cascade);
```

> [!WARNING]
> This question is still open

### Correlating application data types

**Question**: How will we correlate to Tenants in application code?

Each domain-specific tenant-constrained data type will have a `TenantId` property. For example, consider a `Transaction` entity:

```csharp
public class Transaction
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

## Communication to client

**Question**: How do available tenants & role assignments get to the client?

NuxtIdentity returns claims both in the JWT access token, and in the user data structure. We will implement a tenant manager.
It can implement `IUserClaimsProvider<TUser>`, providing tenant role assignments for the current user as claims.

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

## Data Provider tenant scope

**Question**: How do we restrict application features to only the tenant and role assignment we've previously validated?

We will extend the current `IDataProvider` design to add a scoped data provider, *e.g.* `ITenantDataProvider`. It will be restricted to only allow data from a specific tenant. If possible we will try to restrict it to the role level access as well, *e.g.* a read-only data provider if user only nas `Viewer` role assignment.

### Creation time/place

**Question**: When is this created?

It would seem the best time to create this is in the Authorization middleware at the moment we have

Alternately, I could create a separate middleware to live downstream from the authorization middleware, which would consume the

### Creation methodology

**Question**: Exactly how is the tenant data provider created?

> [!WARNING]
> This question is still open

### Access

**Question**: How does the Application Feature get access to this scoped tenant data provider?

This is a big open question I have to figure out. The nearest I can imagine is that the middleware can place it on the context, and then the controller can pick it up from there and explicitly hand it off to the application feature.

## Invitation System

**Question**: How does the invitation system work specifically?

Invitations are out of scope for this feature. They will be a separate "Invitations" feature.

## Additional Questions

Roo code Architecture Review identified some additional questions I will have to think some more about.

### 1.1 Migration and Onboarding

**Question**: How will existing single-user YoFi data migrate to the multi-tenant model?

### 1.2 Tenant Creation Workflow

**Question**: What is the complete user journey for tenant creation?

### 1.3 Claim Structure Format

**Question**: What is the exact structure of tenant role claims?

### 1.4 Performance and Scalability

**Question**: How will tenant queries perform at scale?

### 1.5 Tenant Deletion and Soft Deletes

**Question**: Should tenants support soft deletes?

### 1.7 Concurrency and Race Conditions

**Question**: How do we handle concurrent tenant operations?

### 1.8 Error Handling and User Feedback

**Question**: What error messages should users see for tenant violations?

### 1.9 Testing Strategy

**Question**: How will tenant isolation be tested?

### 1.10 Tenant Metadata and Customization

**Question**: What additional tenant properties might be needed?
