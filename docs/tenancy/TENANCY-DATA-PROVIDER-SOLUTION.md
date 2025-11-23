# Data Provider Solution for Mixed Tenant/Non-Tenant Data

## The Problem You Identified

My original recommendation was **wrong**:

```csharp
// WRONG - Replaces the original IDataProvider entirely
services.AddScoped<IDataProvider, TenantDataProvider>();
```

**The issue**: Not all data is tenant-scoped! You need access to:
- **Tenant-scoped data**: Transactions, Budgets, Categories (filtered by TenantId)
- **Non-tenant data**: Tenants themselves, UserTenantRoleAssignments, global settings, etc.

If we replace `IDataProvider` with `TenantDataProvider`, we can't access non-tenant data anymore!

## Solution Options

### Option 1: Separate Interfaces (RECOMMENDED)

Create distinct interfaces for different scopes:

```csharp
// Base interface (unchanged)
public interface IDataProvider
{
    IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel;
    void Add(IModel item);
    void AddRange(IEnumerable<IModel> items);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class, IModel;
}

// New interface for tenant-scoped operations
public interface ITenantDataProvider
{
    IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel;
    void Add(IModel item);
    void AddRange(IEnumerable<IModel> items);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class, IModel;
}
```

**Registration:**
```csharp
// Both providers available
services.AddScoped<IDataProvider, ApplicationDbContext>();        // Original, unrestricted
services.AddScoped<ITenantDataProvider, TenantDataProvider>();    // Tenant-scoped
services.AddScoped<ITenantContext, TenantContext>();
```

**Implementation:**
```csharp
public class TenantDataProvider : ITenantDataProvider
{
    private readonly IDataProvider _baseProvider;
    private readonly ITenantContext _tenantContext;

    public TenantDataProvider(IDataProvider baseProvider, ITenantContext tenantContext)
    {
        _baseProvider = baseProvider;
        _tenantContext = tenantContext;
    }

    public IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel
    {
        var query = _baseProvider.Get<TEntity>();

        // Automatically filter tenant-scoped entities
        if (typeof(TEntity).GetProperty("TenantId") != null)
        {
            query = query.Where(e => EF.Property<Guid>(e, "TenantId") == _tenantContext.TenantId);
        }
        else
        {
            // Non-tenant entity requested through tenant provider
            // This might be an error, but allow it for flexibility
        }

        return query;
    }

    public void Add(IModel item)
    {
        // Auto-assign tenant ID
        if (item.GetType().GetProperty("TenantId") != null)
        {
            item.GetType().GetProperty("TenantId")!.SetValue(item, _tenantContext.TenantId);
        }
        _baseProvider.Add(item);
    }

    // Other methods delegate to base provider
    public void AddRange(IEnumerable<IModel> items)
    {
        foreach (var item in items)
        {
            Add(item); // Use Add to get tenant ID assignment
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _baseProvider.SaveChangesAsync(cancellationToken);

    public Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class, IModel
        => _baseProvider.ToListNoTrackingAsync(query);
}
```

**Usage:**
```csharp
// Application features that work with tenant-scoped data
public class TransactionFeature
{
    private readonly ITenantDataProvider _dataProvider;

    public TransactionFeature(ITenantDataProvider dataProvider)
    {
        _dataProvider = dataProvider; // Automatically filtered
    }

    public async Task<List<Transaction>> GetAllTransactions()
    {
        // Automatically filtered to current tenant
        var query = _dataProvider.Get<Transaction>()
            .OrderByDescending(t => t.Date);
        return await _dataProvider.ToListAsync(query);
    }
}

// Features that manage tenants themselves (not tenant-scoped)
public class TenantManagementFeature
{
    private readonly IDataProvider _dataProvider;

    public TenantManagementFeature(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider; // Unrestricted access
    }

    public async Task<List<Tenant>> GetUserTenants(string userId)
    {
        // Need unrestricted access to query across tenants
        var query = _dataProvider.Get<UserTenantRoleAssignment>()
            .Where(utra => utra.UserId == userId)
            .Select(utra => utra.Tenant);
        return await _dataProvider.ToListAsync(query);
    }
}
```

**Pros:**
- ✅ Clear separation of concerns
- ✅ Type-safe - can't accidentally use wrong provider
- ✅ Both scoped and unscoped data accessible
- ✅ Explicit about tenant context requirement

**Cons:**
- ⚠️ Two interfaces with identical signatures (might be confusing)
- ⚠️ Need to choose correct interface

---

### Option 2: Marker Interface Pattern

Keep one interface, use marker to indicate tenant scope:

```csharp
// Marker interface
public interface ITenantScoped { }

// Mark entities that are tenant-scoped
public class Transaction : IModel, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    // ...
}

// Tenant entity is NOT tenant-scoped
public class Tenant : IModel
{
    public Guid Id { get; set; }
    // No ITenantScoped marker
}

// Single data provider that checks marker
public class SmartDataProvider : IDataProvider
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext? _tenantContext; // Nullable!

    public IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel
    {
        var query = _context.Set<TEntity>();

        // Auto-filter if entity is tenant-scoped AND we have tenant context
        if (typeof(ITenantScoped).IsAssignableFrom(typeof(TEntity)) &&
            _tenantContext?.TenantId != null)
        {
            query = query.Where(e =>
                EF.Property<Guid>(e, "TenantId") == _tenantContext.TenantId);
        }

        return query;
    }
}
```

**Pros:**
- ✅ Single interface
- ✅ Automatic tenant filtering based on context

**Cons:**
- ⚠️ Less explicit
- ⚠️ Nullable tenant context is confusing
- ⚠️ Can still accidentally query tenant data without context

---

### Option 3: Explicit Tenant Scope Methods

Add methods specifically for tenant operations:

```csharp
public interface IDataProvider
{
    // Unrestricted methods
    IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel;
    void Add(IModel item);

    // Tenant-scoped methods
    IQueryable<TEntity> GetForTenant<TEntity>(Guid tenantId) where TEntity : class, IModel;
    void AddToTenant(IModel item, Guid tenantId);
}

public class ApplicationDbContext : IDataProvider
{
    public IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel
    {
        return Set<TEntity>(); // Unrestricted
    }

    public IQueryable<TEntity> GetForTenant<TEntity>(Guid tenantId) where TEntity : class, IModel
    {
        var query = Set<TEntity>();

        if (typeof(TEntity).GetProperty("TenantId") != null)
        {
            query = query.Where(e => EF.Property<Guid>(e, "TenantId") == tenantId);
        }

        return query;
    }

    public void AddToTenant(IModel item, Guid tenantId)
    {
        if (item.GetType().GetProperty("TenantId") != null)
        {
            item.GetType().GetProperty("TenantId")!.SetValue(item, tenantId);
        }
        Add(item);
    }
}
```

**Usage:**
```csharp
public class TransactionFeature
{
    private readonly IDataProvider _dataProvider;
    private readonly ITenantContext _tenantContext;

    public async Task<List<Transaction>> GetAllTransactions()
    {
        // Explicit tenant scoping
        var query = _dataProvider.GetForTenant<Transaction>(_tenantContext.TenantId)
            .OrderByDescending(t => t.Date);
        return await _dataProvider.ToListAsync(query);
    }
}
```

**Pros:**
- ✅ Single interface
- ✅ Explicit about tenant scoping
- ✅ Both scoped and unscoped available

**Cons:**
- ⚠️ Verbose - need to pass tenantId explicitly
- ⚠️ Can forget to use tenant methods

---

### Option 4: Decorator Pattern (BEST BALANCE)

Keep original provider, wrap it for tenant contexts:

```csharp
// Original stays unchanged
public interface IDataProvider { /* as is */ }

public class ApplicationDbContext : IDataProvider { /* as is */ }

// New decorator for tenant scoping
public class TenantScopedDataProvider : IDataProvider
{
    private readonly IDataProvider _inner;
    private readonly Guid _tenantId;

    public TenantScopedDataProvider(IDataProvider inner, Guid tenantId)
    {
        _inner = inner;
        _tenantId = tenantId;
    }

    public IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel
    {
        var query = _inner.Get<TEntity>();

        // Filter if tenant-scoped
        if (typeof(TEntity).GetProperty("TenantId") != null)
        {
            query = query.Where(e => EF.Property<Guid>(e, "TenantId") == _tenantId);
        }

        return query;
    }

    public void Add(IModel item)
    {
        // Auto-assign tenant ID
        if (item.GetType().GetProperty("TenantId") != null)
        {
            item.GetType().GetProperty("TenantId")!.SetValue(item, _tenantId);
        }
        _inner.Add(item);
    }

    // Delegate everything else
    public void AddRange(IEnumerable<IModel> items)
    {
        foreach (var item in items) Add(item);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _inner.SaveChangesAsync(cancellationToken);

    public Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class, IModel
        => _inner.ToListNoTrackingAsync(query);
}

// Factory to create scoped providers
public interface IDataProviderFactory
{
    IDataProvider CreateTenantScoped(Guid tenantId);
    IDataProvider CreateUnscoped();
}

public class DataProviderFactory : IDataProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DataProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IDataProvider CreateTenantScoped(Guid tenantId)
    {
        var baseProvider = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        return new TenantScopedDataProvider(baseProvider, tenantId);
    }

    public IDataProvider CreateUnscoped()
    {
        return _serviceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
```

**Registration:**
```csharp
services.AddDbContext<ApplicationDbContext>();
services.AddScoped<IDataProvider>(sp => sp.GetRequiredService<ApplicationDbContext>());
services.AddScoped<IDataProviderFactory, DataProviderFactory>();
```

**Usage:**
```csharp
public class TransactionFeature
{
    private readonly IDataProvider _dataProvider;

    // Inject tenant-scoped provider
    public TransactionFeature(IDataProviderFactory factory, ITenantContext tenantContext)
    {
        _dataProvider = factory.CreateTenantScoped(tenantContext.TenantId);
    }

    public async Task<List<Transaction>> GetAllTransactions()
    {
        // Automatically filtered
        var query = _dataProvider.Get<Transaction>();
        return await _dataProvider.ToListAsync(query);
    }
}
```

**Pros:**
- ✅ No interface changes
- ✅ Original IDataProvider still works
- ✅ Flexible - create scoped providers as needed

**Cons:**
- ⚠️ More setup code
- ⚠️ Factory pattern might be unfamiliar

---

## My Recommendation: Option 1 (Separate Interfaces)

**Use separate `IDataProvider` and `ITenantDataProvider` interfaces.**

### Why?

1. **Clear intent**: When you inject `ITenantDataProvider`, it's obvious the feature works with tenant-scoped data
2. **Type safety**: Can't accidentally use wrong provider
3. **Simple**: No factories, no complex patterns
4. **Explicit**: Clear at the feature level which scope you're in

### Complete Implementation

```csharp
// ============ INTERFACES ============

public interface IDataProvider
{
    IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel;
    void Add(IModel item);
    void AddRange(IEnumerable<IModel> items);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class, IModel;
    Task<List<T>> ToListAsync<T>(IQueryable<T> query) where T : class, IModel;
}

public interface ITenantDataProvider
{
    IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel;
    void Add(IModel item);
    void AddRange(IEnumerable<IModel> items);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class, IModel;
    Task<List<T>> ToListAsync<T>(IQueryable<T> query) where T : class, IModel;
}

// ============ CONTEXT INTERFACE ============

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

// ============ IMPLEMENTATIONS ============

// Unchanged - your existing DbContext
public class ApplicationDbContext : DbContext, IDataProvider
{
    // Existing implementation
}

// New tenant-scoped wrapper
public class TenantDataProvider : ITenantDataProvider
{
    private readonly IDataProvider _baseProvider;
    private readonly ITenantContext _tenantContext;

    public TenantDataProvider(IDataProvider baseProvider, ITenantContext tenantContext)
    {
        _baseProvider = baseProvider;
        _tenantContext = tenantContext;
    }

    public IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel
    {
        var query = _baseProvider.Get<TEntity>();

        if (typeof(TEntity).GetProperty("TenantId") != null)
        {
            query = query.Where(e => EF.Property<Guid>(e, "TenantId") == _tenantContext.TenantId);
        }

        return query;
    }

    public void Add(IModel item)
    {
        if (item.GetType().GetProperty("TenantId") != null)
        {
            item.GetType().GetProperty("TenantId")!.SetValue(item, _tenantContext.TenantId);
        }
        _baseProvider.Add(item);
    }

    public void AddRange(IEnumerable<IModel> items)
    {
        foreach (var item in items) Add(item);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _baseProvider.SaveChangesAsync(cancellationToken);

    public Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class, IModel
        => _baseProvider.ToListNoTrackingAsync(query);

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query) where T : class, IModel
        => _baseProvider.ToListAsync(query);
}

// ============ REGISTRATION ============

public static IServiceCollection AddTenantServices(this IServiceCollection services)
{
    // Tenant context
    services.AddScoped<ITenantContext, TenantContext>();

    // Data providers - BOTH registered
    services.AddScoped<IDataProvider>(sp => sp.GetRequiredService<ApplicationDbContext>());
    services.AddScoped<ITenantDataProvider, TenantDataProvider>();

    // Other tenant services...
    services.AddScoped<IUserClaimsProvider<ApplicationUser>, TenantClaimsProvider>();
    services.AddSingleton<IAuthorizationHandler, TenantRoleHandler>();

    return services;
}

// ============ USAGE ============

// Tenant-scoped feature (most features)
public class TransactionFeature
{
    private readonly ITenantDataProvider _dataProvider;

    public TransactionFeature(ITenantDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public async Task<List<Transaction>> GetAllTransactions()
    {
        // Automatically filtered to current tenant
        var query = _dataProvider.Get<Transaction>()
            .OrderByDescending(t => t.Date);
        return await _dataProvider.ToListAsync(query);
    }
}

// Non-tenant-scoped feature (tenant management, user management, etc.)
public class TenantManagementFeature
{
    private readonly IDataProvider _dataProvider;

    public TenantManagementFeature(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public async Task<List<Tenant>> GetUserTenants(string userId)
    {
        // Need access across all tenants
        var query = _dataProvider.Get<UserTenantRoleAssignment>()
            .Where(utra => utra.UserId == userId)
            .Include(utra => utra.Tenant)
            .Select(utra => utra.Tenant);
        return await _dataProvider.ToListAsync(query);
    }

    public async Task<Tenant> CreateTenant(string name, string creatorUserId)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedDate = DateTime.UtcNow,
            IsActive = true
        };

        _dataProvider.Add(tenant);

        // Make creator the owner
        var assignment = new UserTenantRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = creatorUserId,
            TenantId = tenant.Id,
            Role = TenantRole.Owner
        };

        _dataProvider.Add(assignment);
        await _dataProvider.SaveChangesAsync();

        return tenant;
    }
}
```

### Summary

**Problem**: You correctly identified that replacing `IDataProvider` breaks non-tenant data access.

**Solution**: Register **both** providers:
- `IDataProvider` - Unrestricted access (for Tenant, UserTenantRoleAssignment, etc.)
- `ITenantDataProvider` - Tenant-scoped access (for Transaction, Budget, etc.)

Features choose the appropriate provider based on their needs. Most application features use `ITenantDataProvider`. Management features use `IDataProvider`.
