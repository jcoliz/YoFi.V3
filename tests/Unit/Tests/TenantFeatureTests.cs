using Moq;
using NUnit.Framework;
using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Application.Tenancy.Features;
using YoFi.V3.Entities.Tenancy.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Tests.Unit.Tests;

/// <summary>
/// Unit tests for TenantFeature focusing on new test control methods.
/// </summary>
[TestFixture]
public class TenantFeatureTests
{
    private Mock<ITenantRepository> _mockRepository = null!;
    private TenantFeature _tenantFeature = null!;

    [SetUp]
    public void Setup()
    {
        _mockRepository = new Mock<ITenantRepository>();
        _tenantFeature = new TenantFeature(_mockRepository.Object);
    }

    #region GetTenantByKeyAsync Tests

    [Test]
    public async Task GetTenantByKeyAsync_ExistingTenant_ReturnsTenant()
    {
        // Given: A tenant exists in the repository
        var tenantKey = Guid.NewGuid();
        var expectedTenant = new Tenant
        {
            Id = 1,
            Key = tenantKey,
            Name = "__TEST__TestTenant",
            Description = "Test tenant description"
        };
        _mockRepository.Setup(r => r.GetTenantByKeyAsync(tenantKey))
            .ReturnsAsync(expectedTenant);

        // When: GetTenantByKeyAsync is called
        var result = await _tenantFeature.GetTenantByKeyAsync(tenantKey);

        // Then: The tenant should be returned
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Key, Is.EqualTo(tenantKey));
        Assert.That(result.Name, Is.EqualTo("__TEST__TestTenant"));
    }

    [Test]
    public async Task GetTenantByKeyAsync_NonExistentTenant_ReturnsNull()
    {
        // Given: No tenant exists with the specified key
        var tenantKey = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetTenantByKeyAsync(tenantKey))
            .ReturnsAsync((Tenant?)null);

        // When: GetTenantByKeyAsync is called
        var result = await _tenantFeature.GetTenantByKeyAsync(tenantKey);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    #endregion

    #region AddUserTenantRoleAsync Tests

    [Test]
    public async Task AddUserTenantRoleAsync_ValidParameters_AddsRole()
    {
        // Given: Valid user ID, tenant ID, and role
        var userId = Guid.NewGuid();
        var tenantId = 42L;
        var role = TenantRole.Editor;

        UserTenantRoleAssignment? capturedAssignment = null;
        _mockRepository.Setup(r => r.AddUserTenantRoleAsync(It.IsAny<UserTenantRoleAssignment>()))
            .Callback<UserTenantRoleAssignment>(a => capturedAssignment = a)
            .Returns(Task.CompletedTask);

        // When: AddUserTenantRoleAsync is called
        await _tenantFeature.AddUserTenantRoleAsync(userId, tenantId, role);

        // Then: Repository method should be called with correct parameters
        _mockRepository.Verify(r => r.AddUserTenantRoleAsync(It.IsAny<UserTenantRoleAssignment>()), Times.Once);

        // And: Assignment should have correct values
        Assert.That(capturedAssignment, Is.Not.Null);
        Assert.That(capturedAssignment!.UserId, Is.EqualTo(userId.ToString()));
        Assert.That(capturedAssignment.TenantId, Is.EqualTo(tenantId));
        Assert.That(capturedAssignment.Role, Is.EqualTo(role));
    }

    [Test]
    public void AddUserTenantRoleAsync_DuplicateRole_ThrowsDuplicateUserTenantRoleException()
    {
        // Given: A user already has a role in the tenant
        var userId = Guid.NewGuid();
        var tenantId = 42L;
        var role = TenantRole.Owner;

        _mockRepository.Setup(r => r.AddUserTenantRoleAsync(It.IsAny<UserTenantRoleAssignment>()))
            .ThrowsAsync(new DuplicateUserTenantRoleException(userId.ToString(), "TestUser", Guid.NewGuid()));

        // When: AddUserTenantRoleAsync is called
        // Then: DuplicateUserTenantRoleException should be thrown
        Assert.ThrowsAsync<DuplicateUserTenantRoleException>(async () =>
            await _tenantFeature.AddUserTenantRoleAsync(userId, tenantId, role));
    }

    #endregion

    #region GetTenantsByNamePrefixAsync Tests

    [Test]
    public async Task GetTenantsByNamePrefixAsync_MatchingTenants_ReturnsFilteredList()
    {
        // Given: Multiple tenants exist, some with matching prefix
        var testTenant1 = new Tenant { Id = 1, Key = Guid.NewGuid(), Name = "__TEST__Workspace1" };
        var testTenant2 = new Tenant { Id = 2, Key = Guid.NewGuid(), Name = "__TEST__Workspace2" };
        var prodTenant = new Tenant { Id = 3, Key = Guid.NewGuid(), Name = "Production" };

        var roles = new List<UserTenantRoleAssignment>
        {
            new() { TenantId = 1, Tenant = testTenant1, UserId = "user1", Role = TenantRole.Owner },
            new() { TenantId = 2, Tenant = testTenant2, UserId = "user2", Role = TenantRole.Owner },
            new() { TenantId = 3, Tenant = prodTenant, UserId = "user3", Role = TenantRole.Owner }
        };

        _mockRepository.Setup(r => r.GetUserTenantRolesAsync(string.Empty))
            .ReturnsAsync(roles);

        // When: GetTenantsByNamePrefixAsync is called with test prefix
        var result = await _tenantFeature.GetTenantsByNamePrefixAsync("__TEST__");

        // Then: Only test tenants should be returned
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(t => t.Name.StartsWith("__TEST__")), Is.True);
        Assert.That(result.Select(t => t.Id), Does.Contain(1));
        Assert.That(result.Select(t => t.Id), Does.Contain(2));
        Assert.That(result.Select(t => t.Id), Does.Not.Contain(3));
    }

    [Test]
    public async Task GetTenantsByNamePrefixAsync_NoMatchingTenants_ReturnsEmptyList()
    {
        // Given: No tenants match the prefix
        var prodTenant = new Tenant { Id = 1, Key = Guid.NewGuid(), Name = "Production" };
        var roles = new List<UserTenantRoleAssignment>
        {
            new() { TenantId = 1, Tenant = prodTenant, UserId = "user1", Role = TenantRole.Owner }
        };

        _mockRepository.Setup(r => r.GetUserTenantRolesAsync(string.Empty))
            .ReturnsAsync(roles);

        // When: GetTenantsByNamePrefixAsync is called with test prefix
        var result = await _tenantFeature.GetTenantsByNamePrefixAsync("__TEST__");

        // Then: Empty list should be returned
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetTenantsByNamePrefixAsync_DuplicateTenants_ReturnsDistinct()
    {
        // Given: Same tenant appears in multiple role assignments
        var testTenant = new Tenant { Id = 1, Key = Guid.NewGuid(), Name = "__TEST__Workspace" };
        var roles = new List<UserTenantRoleAssignment>
        {
            new() { TenantId = 1, Tenant = testTenant, UserId = "user1", Role = TenantRole.Owner },
            new() { TenantId = 1, Tenant = testTenant, UserId = "user2", Role = TenantRole.Editor }
        };

        _mockRepository.Setup(r => r.GetUserTenantRolesAsync(string.Empty))
            .ReturnsAsync(roles);

        // When: GetTenantsByNamePrefixAsync is called
        var result = await _tenantFeature.GetTenantsByNamePrefixAsync("__TEST__");

        // Then: Only one instance of the tenant should be returned
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetTenantsByNamePrefixAsync_NullTenants_SkipsNulls()
    {
        // Given: Some role assignments have null Tenant navigation property
        var testTenant = new Tenant { Id = 1, Key = Guid.NewGuid(), Name = "__TEST__Workspace" };
        var roles = new List<UserTenantRoleAssignment>
        {
            new() { TenantId = 1, Tenant = testTenant, UserId = "user1", Role = TenantRole.Owner },
            new() { TenantId = 2, Tenant = null, UserId = "user2", Role = TenantRole.Editor }
        };

        _mockRepository.Setup(r => r.GetUserTenantRolesAsync(string.Empty))
            .ReturnsAsync(roles);

        // When: GetTenantsByNamePrefixAsync is called
        var result = await _tenantFeature.GetTenantsByNamePrefixAsync("__TEST__");

        // Then: Only non-null tenants should be returned
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().Id, Is.EqualTo(1));
    }

    #endregion

    #region DeleteTenantsByKeysAsync Tests

    [Test]
    public async Task DeleteTenantsByKeysAsync_ExistingTenants_DeletesAll()
    {
        // Given: Multiple tenants exist
        var key1 = Guid.NewGuid();
        var key2 = Guid.NewGuid();
        var tenant1 = new Tenant { Id = 1, Key = key1, Name = "__TEST__Tenant1" };
        var tenant2 = new Tenant { Id = 2, Key = key2, Name = "__TEST__Tenant2" };

        _mockRepository.Setup(r => r.GetTenantByKeyAsync(key1)).ReturnsAsync(tenant1);
        _mockRepository.Setup(r => r.GetTenantByKeyAsync(key2)).ReturnsAsync(tenant2);
        _mockRepository.Setup(r => r.DeleteTenantAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

        var keys = new List<Guid> { key1, key2 };

        // When: DeleteTenantsByKeysAsync is called
        await _tenantFeature.DeleteTenantsByKeysAsync(keys);

        // Then: Both tenants should be deleted
        _mockRepository.Verify(r => r.DeleteTenantAsync(tenant1), Times.Once);
        _mockRepository.Verify(r => r.DeleteTenantAsync(tenant2), Times.Once);
    }

    [Test]
    public async Task DeleteTenantsByKeysAsync_NonExistentTenant_SkipsDelete()
    {
        // Given: One tenant exists, one doesn't
        var existingKey = Guid.NewGuid();
        var nonExistentKey = Guid.NewGuid();
        var existingTenant = new Tenant { Id = 1, Key = existingKey, Name = "__TEST__Tenant1" };

        _mockRepository.Setup(r => r.GetTenantByKeyAsync(existingKey)).ReturnsAsync(existingTenant);
        _mockRepository.Setup(r => r.GetTenantByKeyAsync(nonExistentKey)).ReturnsAsync((Tenant?)null);
        _mockRepository.Setup(r => r.DeleteTenantAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

        var keys = new List<Guid> { existingKey, nonExistentKey };

        // When: DeleteTenantsByKeysAsync is called
        await _tenantFeature.DeleteTenantsByKeysAsync(keys);

        // Then: Only existing tenant should be deleted
        _mockRepository.Verify(r => r.DeleteTenantAsync(existingTenant), Times.Once);
        _mockRepository.Verify(r => r.DeleteTenantAsync(It.IsAny<Tenant>()), Times.Once);
    }

    [Test]
    public async Task DeleteTenantsByKeysAsync_EmptyCollection_DoesNothing()
    {
        // Given: Empty collection of keys
        var keys = new List<Guid>();

        // When: DeleteTenantsByKeysAsync is called
        await _tenantFeature.DeleteTenantsByKeysAsync(keys);

        // Then: No deletions should occur
        _mockRepository.Verify(r => r.GetTenantByKeyAsync(It.IsAny<Guid>()), Times.Never);
        _mockRepository.Verify(r => r.DeleteTenantAsync(It.IsAny<Tenant>()), Times.Never);
    }

    #endregion

    #region HasUserTenantRoleAsync Tests

    [Test]
    public async Task HasUserTenantRoleAsync_UserHasRole_ReturnsTrue()
    {
        // Given: User has a role in the tenant
        var userId = Guid.NewGuid();
        var tenantId = 42L;
        var role = new UserTenantRoleAssignment
        {
            UserId = userId.ToString(),
            TenantId = tenantId,
            Role = TenantRole.Editor
        };

        _mockRepository.Setup(r => r.GetUserTenantRoleAsync(userId.ToString(), tenantId))
            .ReturnsAsync(role);

        // When: HasUserTenantRoleAsync is called
        var result = await _tenantFeature.HasUserTenantRoleAsync(userId, tenantId);

        // Then: True should be returned
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task HasUserTenantRoleAsync_UserHasNoRole_ReturnsFalse()
    {
        // Given: User has no role in the tenant
        var userId = Guid.NewGuid();
        var tenantId = 42L;

        _mockRepository.Setup(r => r.GetUserTenantRoleAsync(userId.ToString(), tenantId))
            .ReturnsAsync((UserTenantRoleAssignment?)null);

        // When: HasUserTenantRoleAsync is called
        var result = await _tenantFeature.HasUserTenantRoleAsync(userId, tenantId);

        // Then: False should be returned
        Assert.That(result, Is.False);
    }

    #endregion

    #region CreateTenantForUserAsync Tests

    [Test]
    public async Task CreateTenantForUserAsync_ValidData_CreatesTenantWithOwnerRole()
    {
        // Given: Valid user and tenant data
        var userId = Guid.NewGuid();
        var tenantDto = new TenantEditDto("__TEST__MyWorkspace", "Test workspace");

        Tenant? capturedTenant = null;
        UserTenantRoleAssignment? capturedRole = null;

        _mockRepository.Setup(r => r.AddTenantAsync(It.IsAny<Tenant>()))
            .Callback<Tenant>(t => capturedTenant = t)
            .ReturnsAsync((Tenant t) => t);

        _mockRepository.Setup(r => r.AddUserTenantRoleAsync(It.IsAny<UserTenantRoleAssignment>()))
            .Callback<UserTenantRoleAssignment>(r => capturedRole = r)
            .Returns(Task.CompletedTask);

        // When: CreateTenantForUserAsync is called
        var result = await _tenantFeature.CreateTenantForUserAsync(userId, tenantDto);

        // Then: Tenant should be created
        Assert.That(capturedTenant, Is.Not.Null);
        Assert.That(capturedTenant!.Name, Is.EqualTo("__TEST__MyWorkspace"));
        Assert.That(capturedTenant.Description, Is.EqualTo("Test workspace"));

        // And: Owner role should be assigned
        Assert.That(capturedRole, Is.Not.Null);
        Assert.That(capturedRole!.UserId, Is.EqualTo(userId.ToString()));
        Assert.That(capturedRole.Role, Is.EqualTo(TenantRole.Owner));

        // And: Result should match created tenant
        Assert.That(result.Name, Is.EqualTo("__TEST__MyWorkspace"));
        Assert.That(result.Description, Is.EqualTo("Test workspace"));
    }

    #endregion

    #region CreateTenantAsync Tests

    [Test]
    public async Task CreateTenantAsync_ValidData_CreatesTenantWithoutRoles()
    {
        // Given: Valid tenant data
        var tenantDto = new TenantEditDto("__TEST__TestWorkspace", "Administrative test workspace");

        Tenant? capturedTenant = null;

        _mockRepository.Setup(r => r.AddTenantAsync(It.IsAny<Tenant>()))
            .Callback<Tenant>(t => capturedTenant = t)
            .ReturnsAsync((Tenant t) => t);

        // When: CreateTenantAsync is called
        var result = await _tenantFeature.CreateTenantAsync(tenantDto);

        // Then: Tenant should be created
        Assert.That(capturedTenant, Is.Not.Null);
        Assert.That(capturedTenant!.Name, Is.EqualTo("__TEST__TestWorkspace"));
        Assert.That(capturedTenant.Description, Is.EqualTo("Administrative test workspace"));
        Assert.That(capturedTenant.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));

        // And: No role assignments should be made
        _mockRepository.Verify(r => r.AddUserTenantRoleAsync(It.IsAny<UserTenantRoleAssignment>()), Times.Never);

        // And: Result should match created tenant
        Assert.That(result.Name, Is.EqualTo("__TEST__TestWorkspace"));
        Assert.That(result.Description, Is.EqualTo("Administrative test workspace"));
        Assert.That(result.Key, Is.EqualTo(capturedTenant.Key));
    }

    [Test]
    public async Task CreateTenantAsync_CreatesDistinctKey()
    {
        // Given: Multiple tenants are created
        var tenantDto1 = new TenantEditDto("__TEST__Workspace1", "First workspace");
        var tenantDto2 = new TenantEditDto("__TEST__Workspace2", "Second workspace");

        var capturedTenants = new List<Tenant>();

        _mockRepository.Setup(r => r.AddTenantAsync(It.IsAny<Tenant>()))
            .Callback<Tenant>(t => capturedTenants.Add(t))
            .ReturnsAsync((Tenant t) => t);

        // When: CreateTenantAsync is called multiple times
        var result1 = await _tenantFeature.CreateTenantAsync(tenantDto1);
        var result2 = await _tenantFeature.CreateTenantAsync(tenantDto2);

        // Then: Each tenant should have a unique key
        Assert.That(result1.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result2.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result1.Key, Is.Not.EqualTo(result2.Key));

        // And: Keys should match the captured tenants
        Assert.That(capturedTenants, Has.Count.EqualTo(2));
        Assert.That(capturedTenants[0].Key, Is.EqualTo(result1.Key));
        Assert.That(capturedTenants[1].Key, Is.EqualTo(result2.Key));
    }

    #endregion
}
