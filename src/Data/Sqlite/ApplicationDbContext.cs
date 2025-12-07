using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<IdentityUser>(options), IDataProvider
{
    #region Data

    public DbSet<WeatherForecast> WeatherForecasts
    {
        get; set;
    }

    public DbSet<Tenant> Tenants
    {
        get; set;
    }

    public DbSet<UserTenantRoleAssignment> UserTenantRoleAssignments
    {
        get; set;
    }

    /// <summary>
    /// Referesh tokens for Nuxt Identity
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

    // Tenant entity configuration
    modelBuilder.Entity<Tenant>(entity =>
    {
        entity.HasKey(a => a.Id);

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

    #endregion


    #region Query Runners

#pragma warning disable S2325 // These methods can't be static, as they are accessed via interface

    Task<List<T>> IDataProvider.ToListNoTrackingAsync<T>(IQueryable<T> query)
        => query.AsNoTracking().ToListAsync();

    Task<List<T>> IDataProvider.ToListAsync<T>(IQueryable<T> query)
        => query.ToListAsync();

#pragma warning restore S2325

    #endregion

}
