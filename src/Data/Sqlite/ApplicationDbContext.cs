using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Data;

/// <summary>
/// Entity Framework Core database context for the YoFi.V3 application.
/// </summary>
/// <param name="options">Database context configuration options.</param>
/// <remarks>
/// <para>
/// This context serves multiple architectural roles through interface implementations:
/// </para>
///
/// <para><strong>IDataProvider:</strong></para>
/// <para>
/// Provides generic data access operations for application features. This interface abstracts
/// Entity Framework Core operations, allowing the Application layer to remain database-agnostic.
/// Features interact with data through IDataProvider without direct DbContext dependencies.
/// </para>
///
/// <para><strong>ITenantRepository:</strong></para>
/// <para>
/// Implements tenant-specific data operations for multi-tenancy support. This specialized
/// repository handles tenant management, user-tenant role assignments, and tenant-scoped queries.
/// Unlike IDataProvider's generic operations, ITenantRepository provides domain-specific methods
/// for the complex relationships between users, tenants, and roles. This separation ensures
/// tenant operations have proper validation, error handling, and business logic enforcement.
/// </para>
///
/// <para><strong>IDbContextCleaner:</strong></para>
/// <para>
/// Provides change tracker management for preventing DbContext concurrency issues. This interface
/// allows controllers (particularly authentication flows) to clear tracked entities between
/// operations when the same DbContext instance is used across multiple sequential operations.
/// Used by NuxtAuthControllerBase to ensure clean state before querying user roles and claims.
/// </para>
///
/// <para><strong>IdentityDbContext&lt;IdentityUser&gt;:</strong></para>
/// <para>
/// Inherits from ASP.NET Core Identity's database context, providing user authentication and
/// authorization data storage including users, roles, and claims.
/// </para>
/// </remarks>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IDataProvider, ITenantRepository
{
    #region Data

    public DbSet<Tenant> Tenants
    {
        get; set;
    }

    public DbSet<UserTenantRoleAssignment> UserTenantRoleAssignments
    {
        get; set;
    }

    public DbSet<WeatherForecast> WeatherForecasts
    {
        get; set;
    }

    public DbSet<Transaction> Transactions
    {
        get; set;
    }

    public DbSet<Split> Splits
    {
        get; set;
    }

    public DbSet<ImportReviewTransaction> ImportReviewTransactions
    {
        get; set;
    }

    /// <summary>
    /// Refresh tokens for Nuxt Identity
    /// </summary>
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    #endregion

    #region Model Building

    /// <summary>
    /// Configures the entity model and database schema.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Use the extension method from the library
        modelBuilder.ConfigureNuxtIdentityRefreshTokens();

        // Configure WeatherForecast
        modelBuilder.Entity<WeatherForecast>(entity =>
        {
            entity.HasIndex(e => e.Key)
                .IsUnique();
        });

        // Configure Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(e => e.Key)
                .IsUnique();

            // Index on TenantId for efficient tenant-scoped queries
            entity.HasIndex(e => e.TenantId);

            // Index on TenantId and date for common queries
            entity.HasIndex(e => new { e.TenantId, e.Date });

            // Composite index on TenantId + ExternalId for efficient duplicate checks
            entity.HasIndex(e => new { e.TenantId, e.ExternalId });

            // Payee is required
            entity.Property(a => a.Payee)
                .IsRequired()
                .HasMaxLength(200);

            // Amount precision for currency
            entity.Property(a => a.Amount)
                .HasPrecision(18, 2);

            // Source (nullable, max 200 chars)
            entity.Property(t => t.Source)
                .HasMaxLength(200);

            // ExternalId (nullable, max 100 chars)
            entity.Property(t => t.ExternalId)
                .HasMaxLength(100);

            // Memo (nullable, max 1000 chars)
            entity.Property(t => t.Memo)
                .HasMaxLength(1000);

            // Foreign key relationship to Tenant
            entity.HasOne(t => t.Tenant)
                .WithMany()
                .HasForeignKey(t => t.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Split
        modelBuilder.Entity<Split>(entity =>
        {
            // Unique Guid key (standard pattern)
            entity.HasIndex(e => e.Key)
                .IsUnique();

            // Index on TransactionId for efficient split queries
            entity.HasIndex(s => s.TransactionId);

            // Composite index on TransactionId + Order for ordered split retrieval
            entity.HasIndex(s => new { s.TransactionId, s.Order });

            // Index on Category for category-based queries and reports
            entity.HasIndex(s => s.Category);

            // Category is required (empty string for uncategorized)
            entity.Property(s => s.Category)
                .IsRequired()
                .HasMaxLength(100);

            // Memo is optional
            entity.Property(s => s.Memo)
                .HasMaxLength(500);

            // Amount precision for currency
            entity.Property(s => s.Amount)
                .HasPrecision(18, 2);

            // Foreign key relationship to Transaction
            entity.HasOne(s => s.Transaction)
                .WithMany(t => t.Splits)
                .HasForeignKey(s => s.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ImportReviewTransaction
        modelBuilder.Entity<ImportReviewTransaction>(entity =>
        {
            // Unique Guid key (standard pattern)
            entity.HasIndex(e => e.Key)
                .IsUnique();

            // Index on TenantId for efficient tenant-scoped queries
            entity.HasIndex(e => e.TenantId);

            // Composite index on TenantId + Date for common queries
            entity.HasIndex(e => new { e.TenantId, e.Date });

            // Composite index on TenantId + ExternalId for duplicate detection
            entity.HasIndex(e => new { e.TenantId, e.ExternalId });

            // Composite index on TenantId + IsSelected for selection queries
            entity.HasIndex(e => new { e.TenantId, e.IsSelected });

            // Payee is required
            entity.Property(e => e.Payee)
                .IsRequired()
                .HasMaxLength(200);

            // Amount precision for currency
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2);

            // Source (nullable, max 200 chars)
            entity.Property(e => e.Source)
                .HasMaxLength(200);

            // ExternalId (nullable, max 100 chars)
            entity.Property(e => e.ExternalId)
                .HasMaxLength(100);

            // Memo (nullable, max 1000 chars)
            entity.Property(e => e.Memo)
                .HasMaxLength(1000);

            // DuplicateStatus stored as integer
            entity.Property(e => e.DuplicateStatus)
                .HasConversion<int>();

            // IsSelected defaults to false
            entity.Property(e => e.IsSelected)
                .IsRequired()
                .HasDefaultValue(false);

            // Foreign key relationship to Tenant
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Tenant entity configuration
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.HasIndex(e => e.Key)
                .IsUnique();

            entity.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(a => a.CreatedAt)
                .IsRequired();
        });

        // UserTenantRoleAssignment entity configuration
        modelBuilder.Entity<UserTenantRoleAssignment>(entity =>
        {
            entity.HasKey(uaa => uaa.Id);

            entity.Property(uaa => uaa.UserId)
                .IsRequired()
                .HasMaxLength(450); // Standard Identity user ID length

            // Tenant relationship
            entity.HasOne(uaa => uaa.Tenant)
                .WithMany(a => a.RoleAssignments)
                .HasForeignKey(uaa => uaa.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: one user can have only one role per account
            entity.HasIndex(uaa => new { uaa.UserId, uaa.TenantId })
                .IsUnique();

            // Convert enum to string in database
            entity.Property(uaa => uaa.Role)
                .HasConversion<string>();
        });
    }

    #endregion

    #region Query Builders

    /// <inheritdoc />
    IQueryable<T> IDataProvider.Get<T>()
        => base.Set<T>();

    /// <inheritdoc />
    IQueryable<Transaction> IDataProvider.GetTransactionsWithSplits()
        => Transactions.Include(t => t.Splits);

    #endregion

    #region Modifiers

    /// <inheritdoc />
    void IDataProvider.Add(IModel item)
        => base.Add(item);

    /// <inheritdoc />
    public void AddRange(IEnumerable<IModel> items)
        => base.AddRange(items);

    /// <inheritdoc />
    public void UpdateRange(IEnumerable<IModel> items)
        => base.UpdateRange(items);

    /// <inheritdoc />
    void IDataProvider.Remove(IModel item)
        => base.Remove(item);

    /// <inheritdoc />
    public void RemoveRange(IEnumerable<IModel> items)
        => base.RemoveRange(items);

    #endregion

    #region Query Runners

#pragma warning disable S2325 // These methods can't be static, as they are accessed via interface

    /// <inheritdoc />
    Task<List<T>> IDataProvider.ToListNoTrackingAsync<T>(IQueryable<T> query)
        => query.AsNoTracking().ToListAsync();

    /// <inheritdoc />
    Task<List<T>> IDataProvider.ToListAsync<T>(IQueryable<T> query)
        => query.ToListAsync();

    /// <inheritdoc />
    Task<T?> IDataProvider.SingleOrDefaultAsync<T>(IQueryable<T> query) where T : class
        => query.SingleOrDefaultAsync();

    /// <inheritdoc />
    Task<int> IDataProvider.CountAsync<T>(IQueryable<T> query)
        => query.CountAsync();

    /// <inheritdoc />
    Task<int> IDataProvider.ExecuteDeleteAsync<T>(IQueryable<T> query)
        => query.ExecuteDeleteAsync();

#pragma warning restore S2325

    #endregion

    #region ITenantRepository Implementation

    /// <inheritdoc />
    async Task<Tenant> ITenantRepository.AddTenantAsync(Tenant tenant)
    {
        Tenants.Add(tenant);
        await SaveChangesAsync();
        return tenant;
    }

    /// <inheritdoc />
    async Task ITenantRepository.UpdateTenantAsync(Tenant tenant)
    {
        Tenants.Update(tenant);
        await SaveChangesAsync();
    }

    /// <inheritdoc />
    async Task ITenantRepository.DeleteTenantAsync(Tenant tenant)
    {
        Tenants.Remove(tenant);
        await SaveChangesAsync();
    }

    /// <inheritdoc />
    async Task<ICollection<UserTenantRoleAssignment>> ITenantRepository.GetUserTenantRolesAsync(string userId)
    {
        var roles = await UserTenantRoleAssignments
            .Include(utr => utr.Tenant)
            .Where(utr => utr.UserId == userId)
            .ToListAsync();
        return roles;
    }

    /// <inheritdoc />
    Task<UserTenantRoleAssignment?> ITenantRepository.GetUserTenantRoleAsync(string userId, long tenantId)
        => UserTenantRoleAssignments
            .Include(utr => utr.Tenant)
            .SingleOrDefaultAsync(utr => utr.UserId == userId && utr.TenantId == tenantId);

    /// <inheritdoc />
    async Task ITenantRepository.AddUserTenantRoleAsync(UserTenantRoleAssignment assignment)
    {
        UserTenantRoleAssignments.Add(assignment);

        try
        {
            await SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Check if this is a unique constraint violation
            // Look up tenant key from database since assignment.Tenant may not be loaded
            var tenantKey = await Tenants
                .Where(t => t.Id == assignment.TenantId)
                .Select(t => t.Key)
                .SingleOrDefaultAsync();

            // Look up user name
            var userName = await Users
                .Where(u => u.Id == assignment.UserId)
                .Select(u => u.UserName)
                .SingleOrDefaultAsync() ?? assignment.UserId;

            throw new DuplicateUserTenantRoleException(assignment.UserId, userName, tenantKey, ex);
        }
    }

    /// <inheritdoc />
    async Task ITenantRepository.RemoveUserTenantRoleAsync(UserTenantRoleAssignment assignment)
    {
        // Verify the assignment exists in the database and get tenant key
        var tenantKey = await UserTenantRoleAssignments
            .Where(utr => utr.UserId == assignment.UserId && utr.TenantId == assignment.TenantId)
            .Select(utr => utr.Tenant!.Key)
            .SingleOrDefaultAsync();

        if (tenantKey == Guid.Empty)
        {
            // Look up tenant key from Tenants table as fallback
            tenantKey = await Tenants
                .Where(t => t.Id == assignment.TenantId)
                .Select(t => t.Key)
                .SingleOrDefaultAsync();

            // Look up user name
            var userName = await Users
                .Where(u => u.Id == assignment.UserId)
                .Select(u => u.UserName)
                .SingleOrDefaultAsync() ?? assignment.UserId;

            throw new UserTenantRoleNotFoundException(assignment.UserId, userName, tenantKey);
        }

        UserTenantRoleAssignments.Remove(assignment);
        await SaveChangesAsync();
    }

    /// <inheritdoc />
    Task<Tenant?> ITenantRepository.GetTenantAsync(long tenantId)
        => Tenants.SingleOrDefaultAsync(t => t.Id == tenantId);

    /// <inheritdoc />
    Task<Tenant?> ITenantRepository.GetTenantByKeyAsync(Guid tenantKey)
        => Tenants.SingleOrDefaultAsync(t => t.Key == tenantKey);

    #endregion

}
