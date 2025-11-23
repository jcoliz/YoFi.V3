# Simplest Data Provider Solution: Concrete Class Injection

## Your Insight

Instead of creating a new interface, just inject the `TenantDataProvider` as a **concrete class**:

```csharp
// NO new interface needed!
public class TransactionFeature
{
    private readonly TenantDataProvider _tenantData;
    private readonly IDataProvider _globalData;

    public TransactionFeature(TenantDataProvider tenantData, IDataProvider globalData)
    {
        _tenantData = tenantData;  // ← Concrete class for tenant-scoped
        _globalData = globalData;   // ← Interface for unrestricted
    }
}
```

This is **simpler and better** than creating `ITenantDataProvider`!

## Complete Implementation

### 1. Keep Original Interface

```csharp
// Unchanged
public interface IDataProvider
{
    IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel;
    void Add(IModel item);
    void AddRange(IEnumerable<IModel> items);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class, IModel;
    Task<List<T>> ToListAsync<T>(IQueryable<T> query) where T : class, IModel;
}
```

### 2. Create Concrete Tenant Provider

```csharp
// Concrete class - NO interface
public class TenantDataProvider
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

        // Automatically filter by tenant
        if (typeof(TEntity).GetProperty("TenantId") != null)
        {
            query = query.Where(e => EF.Property<Guid>(e, "TenantId") == _tenantContext.TenantId);
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
```

### 3. Registration

```csharp
public static IServiceCollection AddTenantServices(this IServiceCollection services)
{
    // Tenant context
    services.AddScoped<ITenantContext, TenantContext>();

    // Base data provider (unchanged)
    services.AddScoped<IDataProvider>(sp => sp.GetRequiredService<ApplicationDbContext>());

    // Tenant-scoped provider as CONCRETE CLASS
    services.AddScoped<TenantDataProvider>();

    // Other services...
    return services;
}
```

### 4. Usage Patterns

#### Pattern 1: Tenant-Only Features (Most Common)

Features that only work with tenant-scoped data:

```csharp
public class TransactionFeature
{
    private readonly TenantDataProvider _data;

    public TransactionFeature(TenantDataProvider data)
    {
        _data = data; // Automatically tenant-scoped
    }

    public async Task<List<Transaction>> GetAllTransactions()
    {
        var query = _data.Get<Transaction>()
            .OrderByDescending(t => t.Date);
        return await _data.ToListAsync(query);
    }

    public async Task<Transaction> CreateTransaction(Transaction transaction)
    {
        _data.Add(transaction); // TenantId auto-assigned
        await _data.SaveChangesAsync();
        return transaction;
    }
}
```

#### Pattern 2: Global-Only Features

Features that manage tenants or other global data:

```csharp
public class TenantManagementFeature
{
    private readonly IDataProvider _data;

    public TenantManagementFeature(IDataProvider data)
    {
        _data = data; // Unrestricted access
    }

    public async Task<List<Tenant>> GetUserTenants(string userId)
    {
        var query = _data.Get<UserTenantRoleAssignment>()
            .Where(utra => utra.UserId == userId)
            .Include(utra => utra.Tenant)
            .Select(utra => utra.Tenant);
        return await _data.ToListAsync(query);
    }

    public async Task<Tenant> CreateTenant(string name, string creatorUserId)
    {
        var tenant = new Tenant { Name = name };
        _data.Add(tenant);

        var assignment = new UserTenantRoleAssignment
        {
            UserId = creatorUserId,
            TenantId = tenant.Id,
            Role = TenantRole.Owner
        };
        _data.Add(assignment);

        await _data.SaveChangesAsync();
        return tenant;
    }
}
```

#### Pattern 3: Mixed Features (If Needed)

Features that need both scoped and unscoped data:

```csharp
public class ReportingFeature
{
    private readonly TenantDataProvider _tenantData;
    private readonly IDataProvider _globalData;

    public ReportingFeature(TenantDataProvider tenantData, IDataProvider globalData)
    {
        _tenantData = tenantData;
        _globalData = globalData;
    }

    public async Task<TenantReport> GenerateReport()
    {
        // Get tenant info (unrestricted)
        var tenant = await _globalData.Get<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId);

        // Get tenant data (scoped)
        var transactions = await _tenantData.Get<Transaction>().ToListAsync();

        return new TenantReport
        {
            TenantName = tenant.Name,
            TransactionCount = transactions.Count
        };
    }
}
```

## Why This Is Better

### ✅ Simpler
- No new interface to define
- No interface duplication
- Less code overall

### ✅ Clearer Intent
```csharp
// Crystal clear this works with tenant-scoped data
public TransactionFeature(TenantDataProvider data) { }

// vs. with interface (less obvious)
public TransactionFeature(ITenantDataProvider data) { }
```

### ✅ Flexible
- Can inject both if needed
- Can inject just one
- No confusion about which interface to use

### ✅ Type-Safe
- Still get compile-time checking
- IntelliSense works
- No magic strings

### ✅ Testable
```csharp
// Easy to mock for testing
var mockBase = new Mock<IDataProvider>();
var mockContext = new Mock<ITenantContext>();
mockContext.Setup(c => c.TenantId).Returns(testTenantId);

var tenantProvider = new TenantDataProvider(mockBase.Object, mockContext.Object);
var feature = new TransactionFeature(tenantProvider);
```

## Comparison with Interface Approach

### With Interface (More Complex)
```csharp
// Define interface
public interface ITenantDataProvider { /* ... */ }

// Implement interface
public class TenantDataProvider : ITenantDataProvider { /* ... */ }

// Register both
services.AddScoped<IDataProvider>(...);
services.AddScoped<ITenantDataProvider, TenantDataProvider>();

// Inject interface
public TransactionFeature(ITenantDataProvider data) { }
```

### With Concrete Class (Simpler)
```csharp
// Just implement class
public class TenantDataProvider { /* ... */ }

// Register both
services.AddScoped<IDataProvider>(...);
services.AddScoped<TenantDataProvider>();

// Inject concrete class
public TransactionFeature(TenantDataProvider data) { }
```

**Fewer moving parts, same benefits!**

## When You Might Still Want an Interface

There are a few scenarios where you'd prefer `ITenantDataProvider`:

### 1. Multiple Implementations
If you plan to have different tenant-scoped providers:
```csharp
public class ReadOnlyTenantDataProvider : ITenantDataProvider { }
public class ReadWriteTenantDataProvider : ITenantDataProvider { }
```

**But**: This is unlikely in your scenario. Tenant scoping is consistent.

### 2. Library/Framework Code
If you're building a reusable library where others might provide implementations:
```csharp
// In your library
public interface ITenantDataProvider { }

// In consumer's code
public class CustomTenantDataProvider : ITenantDataProvider { }
```

**But**: You're building an application, not a library.

### 3. Strict DDD/Clean Architecture
If you follow strict architectural rules requiring all dependencies be abstractions:
```csharp
// Domain layer depends on interface only
public class TransactionFeature(ITenantDataProvider data) { }
```

**But**: `TenantDataProvider` is infrastructure, not domain. Injecting it is fine.

## Recommendation: Use Concrete Class

For your YoFi.V3 application, **inject `TenantDataProvider` as a concrete class**.

### Benefits:
- ✅ Simpler code
- ✅ Clearer intent
- ✅ Same functionality
- ✅ Easier to understand
- ✅ One less abstraction to maintain

### Implementation Checklist:

```csharp
// 1. Define concrete class
public class TenantDataProvider { /* full implementation */ }

// 2. Register in DI
services.AddScoped<TenantDataProvider>();

// 3. Inject where needed
public class TransactionFeature
{
    private readonly TenantDataProvider _data;
    public TransactionFeature(TenantDataProvider data) => _data = data;
}
```

**That's it! No interface needed.**

## Full Working Example

```csharp
// ============ TENANT CONTEXT ============
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

// ============ TENANT DATA PROVIDER (CONCRETE CLASS) ============
public class TenantDataProvider
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
    services.AddScoped<ITenantContext, TenantContext>();
    services.AddScoped<IDataProvider>(sp => sp.GetRequiredService<ApplicationDbContext>());
    services.AddScoped<TenantDataProvider>(); // ← Concrete class

    return services;
}

// ============ USAGE ============
public class TransactionFeature
{
    private readonly TenantDataProvider _data;

    public TransactionFeature(TenantDataProvider data)
    {
        _data = data;
    }

    public async Task<List<Transaction>> GetAll()
    {
        return await _data.Get<Transaction>()
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }
}
```

Perfect! Simple, clear, and effective.
