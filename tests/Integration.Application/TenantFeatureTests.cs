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
[TestFixture]
public class TenantFeatureTests : FeatureTestBase
{
    private TenantFeature _tenantFeature = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        // ApplicationDbContext implements ITenantRepository
        ITenantRepository tenantRepository = _context;
        _tenantFeature = new TenantFeature(tenantRepository);
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
        Assert.That(result.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));

        // And: Tenant should exist in database
        var tenant = await _context.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Key == result.Key);
        Assert.That(tenant, Is.Not.Null);
        Assert.That(tenant!.Name, Is.EqualTo("New Tenant"));

        // And: User should be assigned Owner role
        var roleAssignment = await _context.Set<UserTenantRoleAssignment>()
            .FirstOrDefaultAsync(utr => utr.UserId == userId.ToString() && utr.TenantId == tenant.Id);
        Assert.That(roleAssignment, Is.Not.Null);
        Assert.That(roleAssignment!.Role, Is.EqualTo(TenantRole.Owner));
    }

    [Test]
    public async Task CreateTenantForUserAsync_MultipleUsers_EachCanCreateTenants()
    {
        // Given: Two different users
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // When: User 1 creates a tenant
        var tenant1Dto = new TenantEditDto("User 1 Tenant", "User 1's tenant");
        var result1 = await _tenantFeature.CreateTenantForUserAsync(user1Id, tenant1Dto);

        // And: User 2 creates a tenant
        var tenant2Dto = new TenantEditDto("User 2 Tenant", "User 2's tenant");
        var result2 = await _tenantFeature.CreateTenantForUserAsync(user2Id, tenant2Dto);

        // Then: Both tenants should be created successfully
        Assert.That(result1.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result2.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result1.Key, Is.Not.EqualTo(result2.Key));

        // And: Each user can only access their own tenant
        var user1Tenants = await _tenantFeature.GetTenantsForUserAsync(user1Id);
        var user2Tenants = await _tenantFeature.GetTenantsForUserAsync(user2Id);

        Assert.That(user1Tenants, Has.Count.EqualTo(1));
        Assert.That(user1Tenants.First().Key, Is.EqualTo(result1.Key));

        Assert.That(user2Tenants, Has.Count.EqualTo(1));
        Assert.That(user2Tenants.First().Key, Is.EqualTo(result2.Key));
    }

    #endregion

    #region GetTenantsForUserAsync Tests

    [Test]
    public async Task GetTenantsForUserAsync_NoTenants_ReturnsEmptyCollection()
    {
        // Given: A user with no tenant memberships
        var userId = Guid.NewGuid();

        // When: User requests their tenants
        var result = await _tenantFeature.GetTenantsForUserAsync(userId);

        // Then: Empty collection should be returned
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetTenantsForUserAsync_WithMultipleTenants_ReturnsAllTenantsWithRoles()
    {
        // Given: A user with access to multiple tenants with different roles
        var userId = Guid.NewGuid();

        var tenant1Key = await CreateTestTenantWithUserRoleAsync(userId, "Tenant One", TenantRole.Owner);
        var tenant2Key = await CreateTestTenantWithUserRoleAsync(userId, "Tenant Two", TenantRole.Editor);
        var tenant3Key = await CreateTestTenantWithUserRoleAsync(userId, "Tenant Three", TenantRole.Viewer);

        // When: User requests their tenants
        var result = await _tenantFeature.GetTenantsForUserAsync(userId);

        // Then: All three tenants should be returned
        Assert.That(result, Has.Count.EqualTo(3));

        // And: Each tenant should have correct key and role
        var tenant1 = result.FirstOrDefault(t => t.Key == tenant1Key);
        Assert.That(tenant1, Is.Not.Null);
        Assert.That(tenant1!.Role, Is.EqualTo(TenantRole.Owner));
        Assert.That(tenant1.Name, Is.EqualTo("Tenant One"));

        var tenant2 = result.FirstOrDefault(t => t.Key == tenant2Key);
        Assert.That(tenant2, Is.Not.Null);
        Assert.That(tenant2!.Role, Is.EqualTo(TenantRole.Editor));

        var tenant3 = result.FirstOrDefault(t => t.Key == tenant3Key);
        Assert.That(tenant3, Is.Not.Null);
        Assert.That(tenant3!.Role, Is.EqualTo(TenantRole.Viewer));
    }

    [Test]
    public async Task GetTenantsForUserAsync_OnlyReturnsUsersTenants()
    {
        // Given: Multiple users with different tenant access
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // And: User 1 has access to tenant A and B
        var tenantAKey = await CreateTestTenantWithUserRoleAsync(user1Id, "Tenant A", TenantRole.Owner);
        var tenantBKey = await CreateTestTenantWithUserRoleAsync(user1Id, "Tenant B", TenantRole.Editor);

        // And: User 2 has access to tenant C only
        var tenantCKey = await CreateTestTenantWithUserRoleAsync(user2Id, "Tenant C", TenantRole.Owner);

        // When: User 1 requests their tenants
        var user1Tenants = await _tenantFeature.GetTenantsForUserAsync(user1Id);

        // Then: Should only contain User 1's tenants (A and B)
        Assert.That(user1Tenants, Has.Count.EqualTo(2));
        Assert.That(user1Tenants.Any(t => t.Key == tenantAKey), Is.True);
        Assert.That(user1Tenants.Any(t => t.Key == tenantBKey), Is.True);
        Assert.That(user1Tenants.Any(t => t.Key == tenantCKey), Is.False);
    }

    #endregion

    #region GetTenantForUserAsync Tests

    [Test]
    public async Task GetTenantForUserAsync_WithAccess_ReturnsTenantWithRole()
    {
        // Given: A user with Editor access to a tenant
        var userId = Guid.NewGuid();
        var tenantKey = await CreateTestTenantWithUserRoleAsync(userId, "My Tenant", TenantRole.Editor);

        // When: User requests the specific tenant by key
        var result = await _tenantFeature.GetTenantForUserAsync(userId, tenantKey);

        // Then: Should return tenant details with user's role
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Key, Is.EqualTo(tenantKey));
        Assert.That(result.Name, Is.EqualTo("My Tenant"));
        Assert.That(result.Role, Is.EqualTo(TenantRole.Editor));
        Assert.That(result.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public async Task GetTenantForUserAsync_NonExistentTenant_ThrowsTenantNotFoundException()
    {
        // Given: A user and a tenant key that doesn't exist
        var userId = Guid.NewGuid();
        var nonExistentKey = Guid.NewGuid();

        // When: User requests the non-existent tenant
        // Then: TenantNotFoundException should be thrown
        var ex = Assert.ThrowsAsync<TenantNotFoundException>(async () =>
            await _tenantFeature.GetTenantForUserAsync(userId, nonExistentKey));

        Assert.That(ex!.Message, Does.Contain(nonExistentKey.ToString()));
    }

    [Test]
    public async Task GetTenantForUserAsync_WithoutAccess_ThrowsTenantAccessDeniedException()
    {
        // Given: Two different users
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // And: User 1 creates a tenant (has access)
        var tenantKey = await CreateTestTenantWithUserRoleAsync(user1Id, "User 1 Tenant", TenantRole.Owner);

        // When: User 2 attempts to access User 1's tenant
        // Then: TenantAccessDeniedException should be thrown
        var ex = Assert.ThrowsAsync<TenantAccessDeniedException>(async () =>
            await _tenantFeature.GetTenantForUserAsync(user2Id, tenantKey));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain(tenantKey.ToString()));
    }

    #endregion

    #region UpdateTenantForUserAsync Tests

    [Test]
    public async Task UpdateTenantForUserAsync_AsOwner_UpdatesSuccessfully()
    {
        // Given: A user with Owner role for a tenant
        var userId = Guid.NewGuid();
        var tenantKey = await CreateTestTenantWithUserRoleAsync(userId, "Original Name", TenantRole.Owner);

        // And: Updated tenant data
        var updateDto = new TenantEditDto(
            Name: "Updated Name",
            Description: "Updated Description"
        );

        // When: User updates the tenant
        var result = await _tenantFeature.UpdateTenantForUserAsync(userId, tenantKey, updateDto);

        // Then: Should return updated tenant
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Key, Is.EqualTo(tenantKey));
        Assert.That(result.Name, Is.EqualTo("Updated Name"));
        Assert.That(result.Description, Is.EqualTo("Updated Description"));

        // And: Changes should be persisted in database
        _context.ChangeTracker.Clear();
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == tenantKey);
        Assert.That(tenant, Is.Not.Null);
        Assert.That(tenant!.Name, Is.EqualTo("Updated Name"));
        Assert.That(tenant.Description, Is.EqualTo("Updated Description"));
    }

    [Test]
    public async Task UpdateTenantForUserAsync_NonExistentTenant_ThrowsTenantNotFoundException()
    {
        // Given: A user and a non-existent tenant key
        var userId = Guid.NewGuid();
        var nonExistentKey = Guid.NewGuid();
        var updateDto = new TenantEditDto("Name", "Description");

        // When: User attempts to update non-existent tenant
        // Then: TenantNotFoundException should be thrown
        var ex = Assert.ThrowsAsync<TenantNotFoundException>(async () =>
            await _tenantFeature.UpdateTenantForUserAsync(userId, nonExistentKey, updateDto));

        Assert.That(ex!.Message, Does.Contain(nonExistentKey.ToString()));
    }

    [Test]
    public async Task UpdateTenantForUserAsync_WithoutAccess_ThrowsTenantAccessDeniedException()
    {
        // Given: Two different users
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // And: Owner creates a tenant
        var tenantKey = await CreateTestTenantWithUserRoleAsync(ownerId, "Owner's Tenant", TenantRole.Owner);

        // And: Update data
        var updateDto = new TenantEditDto("Hacked Name", "Should not work");

        // When: Other user attempts to update the tenant
        // Then: TenantAccessDeniedException should be thrown
        var ex = Assert.ThrowsAsync<TenantAccessDeniedException>(async () =>
            await _tenantFeature.UpdateTenantForUserAsync(otherUserId, tenantKey, updateDto));

        // And: Exception should contain tenant key and userId property
        Assert.That(ex!.Message, Does.Contain(tenantKey.ToString()));
        Assert.That(ex.UserId, Is.EqualTo(otherUserId));
        Assert.That(ex.TenantKey, Is.EqualTo(tenantKey));

        // And: Tenant should remain unchanged
        _context.ChangeTracker.Clear();
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == tenantKey);
        Assert.That(tenant!.Name, Is.EqualTo("Owner's Tenant"));
    }

    #endregion

    #region DeleteTenantForUserAsync Tests

    [Test]
    public async Task DeleteTenantForUserAsync_AsOwner_DeletesSuccessfully()
    {
        // Given: A user with Owner role for a tenant
        var userId = Guid.NewGuid();
        var tenantKey = await CreateTestTenantWithUserRoleAsync(userId, "Tenant To Delete", TenantRole.Owner);

        // When: User deletes the tenant
        await _tenantFeature.DeleteTenantForUserAsync(userId, tenantKey);

        // Then: Tenant should be removed from database
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == tenantKey);
        Assert.That(tenant, Is.Null);

        // And: User tenant role assignment should also be deleted (cascade)
        var roleAssignment = await _context.Set<UserTenantRoleAssignment>()
            .FirstOrDefaultAsync(utr => utr.UserId == userId.ToString());
        Assert.That(roleAssignment, Is.Null);
    }

    [Test]
    public async Task DeleteTenantForUserAsync_NonExistentTenant_ThrowsTenantNotFoundException()
    {
        // Given: A user and a non-existent tenant key
        var userId = Guid.NewGuid();
        var nonExistentKey = Guid.NewGuid();

        // When: User attempts to delete non-existent tenant
        // Then: TenantNotFoundException should be thrown
        var ex = Assert.ThrowsAsync<TenantNotFoundException>(async () =>
            await _tenantFeature.DeleteTenantForUserAsync(userId, nonExistentKey));

        Assert.That(ex!.Message, Does.Contain(nonExistentKey.ToString()));
    }

    [Test]
    public async Task DeleteTenantForUserAsync_WithoutAccess_ThrowsTenantAccessDeniedException()
    {
        // Given: Two different users
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // And: Owner creates a tenant
        var tenantKey = await CreateTestTenantWithUserRoleAsync(ownerId, "Owner's Tenant", TenantRole.Owner);

        // When: Other user attempts to delete the tenant
        // Then: TenantAccessDeniedException should be thrown
        var ex = Assert.ThrowsAsync<TenantAccessDeniedException>(async () =>
            await _tenantFeature.DeleteTenantForUserAsync(otherUserId, tenantKey));

        // And: Exception should contain tenant key and userId property
        Assert.That(ex!.Message, Does.Contain(tenantKey.ToString()));
        Assert.That(ex.UserId, Is.EqualTo(otherUserId));
        Assert.That(ex.TenantKey, Is.EqualTo(tenantKey));

        // And: Tenant should still exist
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == tenantKey);
        Assert.That(tenant, Is.Not.Null);
    }

    #endregion

    #region ADMIN Functionality Tests

    [Test]
    public async Task GetTenantByKeyAsync_ExistingTenant_ReturnsTenant()
    {
        // Given: A tenant exists in the database
        var userId = Guid.NewGuid();
        var tenantKey = await CreateTestTenantWithUserRoleAsync(userId, "Test Tenant", TenantRole.Owner);

        // When: Getting tenant by key (admin method, no access checks)
        var result = await _tenantFeature.GetTenantByKeyAsync(tenantKey);

        // Then: Tenant should be returned
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Key, Is.EqualTo(tenantKey));
        Assert.That(result.Name, Is.EqualTo("Test Tenant"));
    }

    [Test]
    public async Task GetTenantByKeyAsync_NonExistentTenant_ReturnsNull()
    {
        // Given: A tenant key that doesn't exist
        var nonExistentKey = Guid.NewGuid();

        // When: Getting tenant by key
        var result = await _tenantFeature.GetTenantByKeyAsync(nonExistentKey);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task HasUserTenantRoleAsync_UserHasRole_ReturnsTrue()
    {
        // Given: A user with Editor role for a tenant
        var userId = Guid.NewGuid();
        await CreateTestTenantWithUserRoleAsync(userId, "Test Tenant", TenantRole.Editor);

        // And: Get the tenant ID
        var tenant = await _context.Set<Tenant>().FirstAsync();

        // When: Checking if user has role
        var result = await _tenantFeature.HasUserTenantRoleAsync(userId, tenant.Id);

        // Then: Should return true
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task HasUserTenantRoleAsync_UserHasNoRole_ReturnsFalse()
    {
        // Given: A tenant with one user
        var user1Id = Guid.NewGuid();
        await CreateTestTenantWithUserRoleAsync(user1Id, "Test Tenant", TenantRole.Owner);

        // And: A different user without access
        var user2Id = Guid.NewGuid();

        // And: Get the tenant ID
        var tenant = await _context.Set<Tenant>().FirstAsync();

        // When: Checking if user2 has role
        var result = await _tenantFeature.HasUserTenantRoleAsync(user2Id, tenant.Id);

        // Then: Should return false
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task GetTenantsByNamePrefixAsync_MatchingTenants_ReturnsFiltered()
    {
        // Given: Multiple tenants with different name prefixes
        var userId = Guid.NewGuid();
        await CreateTestTenantWithUserRoleAsync(userId, "__TEST__Tenant1", TenantRole.Owner);
        await CreateTestTenantWithUserRoleAsync(userId, "__TEST__Tenant2", TenantRole.Owner);
        await CreateTestTenantWithUserRoleAsync(userId, "Production Tenant", TenantRole.Owner);

        // When: Getting tenants with __TEST__ prefix
        var result = await _tenantFeature.GetTenantsByNamePrefixAsync("__TEST__");

        // Then: Should return only test tenants
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(t => t.Name.StartsWith("__TEST__")), Is.True);
    }

    [Test]
    public async Task DeleteTenantsByKeysAsync_MultipleKeys_DeletesAllTenants()
    {
        // Given: Multiple test tenants
        var userId = Guid.NewGuid();
        var tenant1Key = await CreateTestTenantWithUserRoleAsync(userId, "__TEST__Tenant1", TenantRole.Owner);
        var tenant2Key = await CreateTestTenantWithUserRoleAsync(userId, "__TEST__Tenant2", TenantRole.Owner);
        var tenant3Key = await CreateTestTenantWithUserRoleAsync(userId, "Keep This", TenantRole.Owner);

        // When: Deleting multiple tenants by keys
        await _tenantFeature.DeleteTenantsByKeysAsync(new[] { tenant1Key, tenant2Key });

        // Then: Specified tenants should be deleted
        var tenant1 = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == tenant1Key);
        var tenant2 = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == tenant2Key);
        Assert.That(tenant1, Is.Null);
        Assert.That(tenant2, Is.Null);

        // And: Other tenant should remain
        var tenant3 = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == tenant3Key);
        Assert.That(tenant3, Is.Not.Null);
    }

    [Test]
    public async Task CreateTenantAsync_ValidData_CreatesTenantWithoutRoleAssignments()
    {
        // Given: Valid tenant data
        var tenantDto = new TenantEditDto(
            Name: "__TEST__AdminTenant",
            Description: "Tenant created by admin"
        );

        // When: Admin creates a tenant without role assignments
        var result = await _tenantFeature.CreateTenantAsync(tenantDto);

        // Then: Tenant should be created with correct data
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.Name, Is.EqualTo("__TEST__AdminTenant"));
        Assert.That(result.Description, Is.EqualTo("Tenant created by admin"));
        Assert.That(result.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));

        // And: Tenant should exist in database
        var tenant = await _context.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Key == result.Key);
        Assert.That(tenant, Is.Not.Null);
        Assert.That(tenant!.Name, Is.EqualTo("__TEST__AdminTenant"));

        // And: No role assignments should exist for this tenant
        var roleAssignments = await _context.Set<UserTenantRoleAssignment>()
            .Where(utr => utr.TenantId == tenant.Id)
            .ToListAsync();
        Assert.That(roleAssignments, Is.Empty);
    }

    [Test]
    public async Task CreateTenantAsync_MultipleTenants_CreatesDistinctKeys()
    {
        // Given: Two tenant creation requests
        var tenant1Dto = new TenantEditDto("__TEST__Tenant1", "First tenant");
        var tenant2Dto = new TenantEditDto("__TEST__Tenant2", "Second tenant");

        // When: Admin creates multiple tenants
        var result1 = await _tenantFeature.CreateTenantAsync(tenant1Dto);
        var result2 = await _tenantFeature.CreateTenantAsync(tenant2Dto);

        // Then: Each tenant should have a unique key
        Assert.That(result1.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result2.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result1.Key, Is.Not.EqualTo(result2.Key));

        // And: Both tenants should exist in database
        var tenant1 = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == result1.Key);
        var tenant2 = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Key == result2.Key);
        Assert.That(tenant1, Is.Not.Null);
        Assert.That(tenant2, Is.Not.Null);
    }

    [Test]
    public async Task AddUserTenantRoleAsync_ValidParameters_AddsRole()
    {
        // Given: A tenant exists without role assignments
        var tenantDto = new TenantEditDto("__TEST__Workspace", "Test workspace");
        var tenantResult = await _tenantFeature.CreateTenantAsync(tenantDto);
        var tenant = await _context.Set<Tenant>().FirstAsync(t => t.Key == tenantResult.Key);

        // And: A user ID and role
        var userId = Guid.NewGuid();
        var role = TenantRole.Editor;

        // When: Admin assigns role to user
        await _tenantFeature.AddUserTenantRoleAsync(userId, tenant.Id, role);

        // Then: Role assignment should exist in database
        var roleAssignment = await _context.Set<UserTenantRoleAssignment>()
            .FirstOrDefaultAsync(utr => utr.UserId == userId.ToString() && utr.TenantId == tenant.Id);
        Assert.That(roleAssignment, Is.Not.Null);
        Assert.That(roleAssignment!.Role, Is.EqualTo(TenantRole.Editor));

        // And: User should be able to access the tenant
        var userTenants = await _tenantFeature.GetTenantsForUserAsync(userId);
        Assert.That(userTenants, Has.Count.EqualTo(1));
        Assert.That(userTenants.First().Key, Is.EqualTo(tenantResult.Key));
        Assert.That(userTenants.First().Role, Is.EqualTo(TenantRole.Editor));
    }

    [Test]
    public async Task AddUserTenantRoleAsync_MultipleRoles_AllowsDifferentUsersInSameTenant()
    {
        // Given: A tenant exists
        var tenantDto = new TenantEditDto("__TEST__SharedWorkspace", "Shared workspace");
        var tenantResult = await _tenantFeature.CreateTenantAsync(tenantDto);
        var tenant = await _context.Set<Tenant>().FirstAsync(t => t.Key == tenantResult.Key);

        // And: Multiple users
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var user3Id = Guid.NewGuid();

        // When: Admin assigns different roles to different users
        await _tenantFeature.AddUserTenantRoleAsync(user1Id, tenant.Id, TenantRole.Owner);
        await _tenantFeature.AddUserTenantRoleAsync(user2Id, tenant.Id, TenantRole.Editor);
        await _tenantFeature.AddUserTenantRoleAsync(user3Id, tenant.Id, TenantRole.Viewer);

        // Then: All three users should have access with correct roles
        var user1Tenants = await _tenantFeature.GetTenantsForUserAsync(user1Id);
        var user2Tenants = await _tenantFeature.GetTenantsForUserAsync(user2Id);
        var user3Tenants = await _tenantFeature.GetTenantsForUserAsync(user3Id);

        Assert.That(user1Tenants.First().Role, Is.EqualTo(TenantRole.Owner));
        Assert.That(user2Tenants.First().Role, Is.EqualTo(TenantRole.Editor));
        Assert.That(user3Tenants.First().Role, Is.EqualTo(TenantRole.Viewer));

        // And: Tenant should have 3 role assignments
        var roleAssignments = await _context.Set<UserTenantRoleAssignment>()
            .Where(utr => utr.TenantId == tenant.Id)
            .ToListAsync();
        Assert.That(roleAssignments, Has.Count.EqualTo(3));
    }

    [Test]
    public void AddUserTenantRoleAsync_DuplicateRole_ThrowsDuplicateUserTenantRoleException()
    {
        // Given: A tenant with a user already assigned a role
        var tenantDto = new TenantEditDto("__TEST__Workspace", "Test workspace");
        var userId = Guid.NewGuid();

        // When: Creating tenant and assigning role
        var tenantResult = _tenantFeature.CreateTenantAsync(tenantDto).GetAwaiter().GetResult();
        var tenant = _context.Set<Tenant>().First(t => t.Key == tenantResult.Key);
        _tenantFeature.AddUserTenantRoleAsync(userId, tenant.Id, TenantRole.Owner).GetAwaiter().GetResult();

        // Then: Attempting to add the same user again should throw
        var ex = Assert.ThrowsAsync<DuplicateUserTenantRoleException>(async () =>
            await _tenantFeature.AddUserTenantRoleAsync(userId, tenant.Id, TenantRole.Editor));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain(userId.ToString()));
    }

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
            CreatedAt = DateTimeOffset.UtcNow
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
