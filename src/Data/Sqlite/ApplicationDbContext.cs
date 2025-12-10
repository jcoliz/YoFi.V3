using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Data;

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

    /// <summary>
    /// Refresh tokens for Nuxt Identity
    /// </summary>
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    #endregion

    #region Model Building

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

    IQueryable<T> IDataProvider.Get<T>()
        => base.Set<T>();

    #endregion

    #region Modifiers

    void IDataProvider.Add(IModel item)
        => base.Add(item);

    public void AddRange(IEnumerable<IModel> items)
        => base.AddRange(items);

    public void UpdateRange(IEnumerable<IModel> items)
        => base.UpdateRange(items);

    void IDataProvider.Remove(IModel item)
        => base.Remove(item);

    #endregion

    #region Query Runners

#pragma warning disable S2325 // These methods can't be static, as they are accessed via interface

    Task<List<T>> IDataProvider.ToListNoTrackingAsync<T>(IQueryable<T> query)
        => query.AsNoTracking().ToListAsync();

    Task<List<T>> IDataProvider.ToListAsync<T>(IQueryable<T> query)
        => query.ToListAsync();

    Task<T?> IDataProvider.SingleOrDefaultAsync<T>(IQueryable<T> query) where T : class
        => query.SingleOrDefaultAsync();

#pragma warning restore S2325

    #endregion

    #region ITenantRepository Implementation

    async Task<ICollection<UserTenantRoleAssignment>> ITenantRepository.GetUserTenantRolesAsync(string userId)
    {
        var roles = await UserTenantRoleAssignments
            .Include(utr => utr.Tenant)
            .Where(utr => utr.UserId == userId)
            .ToListAsync();
        return roles;
    }

    Task<UserTenantRoleAssignment?> ITenantRepository.GetUserTenantRoleAsync(string userId, long tenantId)
        => UserTenantRoleAssignments
            .Include(utr => utr.Tenant)
            .SingleOrDefaultAsync(utr => utr.UserId == userId && utr.TenantId == tenantId);

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

    Task<Tenant?> ITenantRepository.GetTenantAsync(long tenantId)
        => Tenants.SingleOrDefaultAsync(t => t.Id == tenantId);

    #endregion

}
