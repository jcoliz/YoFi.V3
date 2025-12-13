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
    : IdentityDbContext<IdentityUser>(options), IDataProvider, ITenantRepository, IDbContextCleaner
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

            entity.Property(a => a.Payee)
                .IsRequired()
                .HasMaxLength(200);

            // Foreign key relationship to Tenant
            entity.HasOne(t => t.Tenant)
                .WithMany()
                .HasForeignKey(t => t.TenantId)
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
    void IDbContextCleaner.ClearChangeTracker()
        => ChangeTracker.Clear();

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
            throw new DuplicateUserTenantRoleException(assignment.UserId, assignment.TenantId, ex);
        }
    }

    /// <inheritdoc />
    async Task ITenantRepository.RemoveUserTenantRoleAsync(UserTenantRoleAssignment assignment)
    {
        // Verify the assignment exists in the database
        var exists = await UserTenantRoleAssignments
            .AnyAsync(utr => utr.UserId == assignment.UserId && utr.TenantId == assignment.TenantId);

        if (!exists)
        {
            throw new UserTenantRoleNotFoundException(assignment.UserId, assignment.TenantId);
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
