using Microsoft.EntityFrameworkCore;
using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Application.Tenancy.Features;
using YoFi.V3.Entities.Tenancy.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Integration tests for TenantFeature operations.
/// </summary>
/// <remarks>
/// Tests the Application layer's tenant management functionality with real database
/// through ITenantRepository, verifying business logic without HTTP overhead.
/// </remarks>
public class TenantFeatureTests : FeatureTestBase
{
    private TenantFeature _tenantFeature = null!;
    private TestTimeProvider _timeProvider = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        // ApplicationDbContext implements ITenantRepository
        ITenantRepository tenantRepository = _context;
        _timeProvider = new TestTimeProvider();
        _tenantFeature = new TenantFeature(tenantRepository, _timeProvider);
    }

    #region CreateTenantForUserAsync Tests

    [Test]
    public async Task CreateTenantForUserAsync_ValidData_CreatesTenantAndAssignsOwnerRole()
    {
        // Given: A valid user ID and tenant data
        var userId = Guid.NewGuid();
        var tenantDto = new TenantEditDto(
            Name: "New Tenant",
            Description: "This is a new tenant"
        );

        // When: User creates a new tenant
        var result = await _tenantFeature.CreateTenantForUserAsync(userId, tenantDto);

        // Then: Tenant should be created with correct data
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.Name, Is.EqualTo("New Tenant"));
        Assert.That(result.Description, Is.EqualTo("This is a new tenant"));
        Assert.That(result.CreatedAt, Is.EqualTo(_timeProvider.GetUtcNow()));

        // And: Tenant should exist in database
        _context.ChangeTracker.Clear();
        var tenant = await _context.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Key == result.Key);
        Assert.That(tenant, Is.Not.Null);
        Assert.That(tenant!.Name, Is.EqualTo("New Tenant"));
        Assert.That(tenant.CreatedAt, Is.EqualTo(_timeProvider.GetUtcNow()));

        // And: User should be assigned Owner role
        var roleAssignment = await _context.Set<UserTenantRoleAssignment>()
            .FirstOrDefaultAsync(utr => utr.UserId == userId.ToString() && utr.TenantId == tenant.Id);
        Assert.That(roleAssignment, Is.Not.Null);
        Assert.That(roleAssignment!.Role, Is.EqualTo(TenantRole.Owner));
    }


    #endregion

    #region GetTenantsForUserAsync Tests




    #endregion

    #region GetTenantForUserAsync Tests




    #endregion

    #region UpdateTenantForUserAsync Tests




    #endregion

    #region DeleteTenantForUserAsync Tests




    #endregion

    #region ADMIN Functionality Tests












    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a tenant and assigns a user with the specified role.
    /// </summary>
    private async Task<Guid> CreateTestTenantWithUserRoleAsync(Guid userId, string name, TenantRole role)
    {
        var tenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = name,
            Description = $"Test tenant: {name}",
            CreatedAt = _timeProvider.GetUtcNow()
        };

        _context.Set<Tenant>().Add(tenant);
        await _context.SaveChangesAsync();

        var roleAssignment = new UserTenantRoleAssignment
        {
            UserId = userId.ToString(),
            TenantId = tenant.Id,
            Role = role
        };

        _context.Set<UserTenantRoleAssignment>().Add(roleAssignment);
        await _context.SaveChangesAsync();

        return tenant.Key;
    }

    #endregion
}
