using Microsoft.EntityFrameworkCore;
using YoFi.V3.Data;
using YoFi.V3.Entities.Tenancy.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Tests.Integration.Data;

/// <summary>
/// Integration tests for ITenantRepository implementation in ApplicationDbContext.
/// These tests verify repository methods for tenant-related data operations.
/// </summary>
public class TenantRepositoryTests
{
    private ApplicationDbContext _context;
    private ITenantRepository _repository;
    private DbContextOptions<ApplicationDbContext> _options;
    private Tenant _tenant1;
    private Tenant _tenant2;

    [SetUp]
    public async Task Setup()
    {
        // Given: An in-memory database for testing
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new ApplicationDbContext(_options);
        _repository = _context;
        _context.Database.OpenConnection(); // Keep in-memory DB alive
        _context.Database.EnsureCreated();

        // And: Test tenants are created
        _tenant1 = new Tenant { Name = "Tenant 1", Description = "First test tenant" };
        _tenant2 = new Tenant { Name = "Tenant 2", Description = "Second test tenant" };
        _context.Tenants.AddRange(_tenant1, _tenant2);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Test]
    public async Task GetUserTenantRolesAsync_NoRoles_ReturnsEmptyCollection()
    {
        // Given: A user with no role assignments

        // When: Getting user tenant roles
        var result = await _repository.GetUserTenantRolesAsync("user-no-roles");

        // Then: Should return empty collection
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetUserTenantRolesAsync_SingleRole_ReturnsSingleAssignment()
    {
        // Given: A user with one role assignment
        var assignment = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };
        _context.UserTenantRoleAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        // When: Getting user tenant roles
        var result = await _repository.GetUserTenantRolesAsync("user123");

        // Then: Should return single assignment
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().UserId, Is.EqualTo("user123"));
        Assert.That(result.First().TenantId, Is.EqualTo(_tenant1.Id));
        Assert.That(result.First().Role, Is.EqualTo(TenantRole.Editor));
    }

    [Test]
    public async Task GetUserTenantRolesAsync_MultipleRoles_ReturnsAllAssignments()
    {
        // Given: A user with multiple role assignments
        var assignments = new[]
        {
            new UserTenantRoleAssignment { UserId = "user123", TenantId = _tenant1.Id, Role = TenantRole.Editor },
            new UserTenantRoleAssignment { UserId = "user123", TenantId = _tenant2.Id, Role = TenantRole.Owner }
        };
        _context.UserTenantRoleAssignments.AddRange(assignments);
        await _context.SaveChangesAsync();

        // When: Getting user tenant roles
        var result = await _repository.GetUserTenantRolesAsync("user123");

        // Then: Should return all assignments
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(r => r.UserId == "user123"), Is.True);
    }

    [Test]
    public async Task GetUserTenantRolesAsync_IncludesTenantNavigationProperty()
    {
        // Given: A user with role assignments
        var assignment = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };
        _context.UserTenantRoleAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        // When: Getting user tenant roles
        var result = await _repository.GetUserTenantRolesAsync("user123");

        // Then: Tenant navigation property should be loaded
        Assert.That(result.First().Tenant, Is.Not.Null);
        Assert.That(result.First().Tenant!.Name, Is.EqualTo("Tenant 1"));
    }

    [Test]
    public async Task GetUserTenantRoleAsync_ExistingRole_ReturnsAssignment()
    {
        // Given: A user with a role assignment
        var assignment = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };
        _context.UserTenantRoleAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        // When: Getting specific user tenant role
        var result = await _repository.GetUserTenantRoleAsync("user123", _tenant1.Id);

        // Then: Should return the assignment
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.UserId, Is.EqualTo("user123"));
        Assert.That(result.TenantId, Is.EqualTo(_tenant1.Id));
        Assert.That(result.Role, Is.EqualTo(TenantRole.Editor));
    }

    [Test]
    public async Task GetUserTenantRoleAsync_NonExistingRole_ReturnsNull()
    {
        // Given: No role assignment for user and tenant

        // When: Getting specific user tenant role
        var result = await _repository.GetUserTenantRoleAsync("user-no-role", _tenant1.Id);

        // Then: Should return null
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetUserTenantRoleAsync_IncludesTenantNavigationProperty()
    {
        // Given: A user with a role assignment
        var assignment = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };
        _context.UserTenantRoleAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        // When: Getting specific user tenant role
        var result = await _repository.GetUserTenantRoleAsync("user123", _tenant1.Id);

        // Then: Tenant navigation property should be loaded
        Assert.That(result!.Tenant, Is.Not.Null);
        Assert.That(result.Tenant!.Name, Is.EqualTo("Tenant 1"));
    }

    [Test]
    public async Task AddUserTenantRoleAsync_NewAssignment_SavesSuccessfully()
    {
        // Given: A new role assignment
        var assignment = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Viewer
        };

        // When: Adding the assignment
        await _repository.AddUserTenantRoleAsync(assignment);

        // Then: Assignment should be saved
        var saved = await _context.UserTenantRoleAssignments
            .FirstOrDefaultAsync(a => a.UserId == "user123" && a.TenantId == _tenant1.Id);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Role, Is.EqualTo(TenantRole.Viewer));
    }

    [Test]
    public async Task AddUserTenantRoleAsync_DuplicateAssignment_ThrowsDuplicateUserTenantRoleException()
    {
        // Given: An existing role assignment
        var existing = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };
        _context.UserTenantRoleAssignments.Add(existing);
        await _context.SaveChangesAsync();

        // And: A duplicate assignment (same user and tenant)
        var duplicate = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Owner // Different role, but same user/tenant
        };

        // When: Attempting to add the duplicate
        // Then: Should throw DuplicateUserTenantRoleException
        var ex = Assert.ThrowsAsync<DuplicateUserTenantRoleException>(
            async () => await _repository.AddUserTenantRoleAsync(duplicate));

        // And: Exception should contain correct information
        Assert.That(ex!.UserId, Is.EqualTo("user123"));
        Assert.That(ex.TenantKey, Is.EqualTo(_tenant1.Key));
    }

    [Test]
    public async Task RemoveUserTenantRoleAsync_ExistingAssignment_RemovesSuccessfully()
    {
        // Given: An existing role assignment
        var assignment = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };
        _context.UserTenantRoleAssignments.Add(assignment);
        await _context.SaveChangesAsync();
        var assignmentId = assignment.Id;

        // When: Removing the assignment
        await _repository.RemoveUserTenantRoleAsync(assignment);

        // Then: Assignment should be removed
        var removed = await _context.UserTenantRoleAssignments.FindAsync(assignmentId);
        Assert.That(removed, Is.Null);
    }

    [Test]
    public async Task RemoveUserTenantRoleAsync_NonExistingAssignment_ThrowsUserTenantRoleNotFoundException()
    {
        // Given: A non-existing assignment
        var nonExisting = new UserTenantRoleAssignment
        {
            UserId = "user-not-exists",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };

        // When: Attempting to remove the non-existing assignment
        // Then: Should throw UserTenantRoleNotFoundException
        var ex = Assert.ThrowsAsync<UserTenantRoleNotFoundException>(
            async () => await _repository.RemoveUserTenantRoleAsync(nonExisting));

        // And: Exception should contain correct information
        Assert.That(ex!.UserId, Is.EqualTo("user-not-exists"));
        Assert.That(ex.TenantKey, Is.EqualTo(_tenant1.Key));
    }

    [Test]
    public async Task RemoveUserTenantRoleAsync_WithUntrackedEntity_RemovesSuccessfully()
    {
        // Given: An assignment that exists in DB
        var assignment = new UserTenantRoleAssignment
        {
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };
        _context.UserTenantRoleAssignments.Add(assignment);
        await _context.SaveChangesAsync();
        var assignmentId = assignment.Id;

        // And: An untracked entity with the same details (simulating detached entity)
        _context.Entry(assignment).State = EntityState.Detached;
        var untrackedAssignment = new UserTenantRoleAssignment
        {
            Id = assignmentId,
            UserId = "user123",
            TenantId = _tenant1.Id,
            Role = TenantRole.Editor
        };

        // When: Removing the untracked assignment
        // Then: Should not throw (the check will find it exists)
        Assert.DoesNotThrowAsync(async () => await _repository.RemoveUserTenantRoleAsync(untrackedAssignment));

        // And: Assignment should be removed
        var removed = await _context.UserTenantRoleAssignments.FindAsync(assignmentId);
        Assert.That(removed, Is.Null);
    }

    [Test]
    public async Task GetTenantAsync_ExistingTenant_ReturnsTenant()
    {
        // Given: An existing tenant

        // When: Getting tenant by ID
        var result = await _repository.GetTenantAsync(_tenant1.Id);

        // Then: Should return the tenant
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(_tenant1.Id));
        Assert.That(result.Name, Is.EqualTo("Tenant 1"));
    }

    [Test]
    public async Task GetTenantAsync_NonExistingTenant_ReturnsNull()
    {
        // Given: No tenant with ID 999999

        // When: Getting tenant by non-existing ID
        var result = await _repository.GetTenantAsync(999999);

        // Then: Should return null
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task RepositoryOperations_WorkAcrossMultipleUsers()
    {
        // Given: Multiple users with various role assignments
        var assignments = new[]
        {
            new UserTenantRoleAssignment { UserId = "user1", TenantId = _tenant1.Id, Role = TenantRole.Owner },
            new UserTenantRoleAssignment { UserId = "user2", TenantId = _tenant1.Id, Role = TenantRole.Editor },
            new UserTenantRoleAssignment { UserId = "user3", TenantId = _tenant2.Id, Role = TenantRole.Viewer }
        };
        foreach (var assignment in assignments)
        {
            await _repository.AddUserTenantRoleAsync(assignment);
        }

        // When: Getting roles for each user
        var user1Roles = await _repository.GetUserTenantRolesAsync("user1");
        var user2Roles = await _repository.GetUserTenantRolesAsync("user2");
        var user3Roles = await _repository.GetUserTenantRolesAsync("user3");

        // Then: Each user should have their correct assignments
        Assert.That(user1Roles, Has.Count.EqualTo(1));
        Assert.That(user1Roles.First().Role, Is.EqualTo(TenantRole.Owner));

        Assert.That(user2Roles, Has.Count.EqualTo(1));
        Assert.That(user2Roles.First().Role, Is.EqualTo(TenantRole.Editor));

        Assert.That(user3Roles, Has.Count.EqualTo(1));
        Assert.That(user3Roles.First().Role, Is.EqualTo(TenantRole.Viewer));
    }

    [Test]
    public async Task GetTenantByKeyAsync_ExistingTenant_ReturnsTenant()
    {
        // Given: An existing tenant with a known key
        var tenantKey = _tenant1.Key;

        // When: Getting tenant by key
        var result = await _repository.GetTenantByKeyAsync(tenantKey);

        // Then: Should return the tenant
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(_tenant1.Id));
        Assert.That(result.Key, Is.EqualTo(tenantKey));
        Assert.That(result.Name, Is.EqualTo("Tenant 1"));
    }

    [Test]
    public async Task GetTenantByKeyAsync_NonExistingKey_ReturnsNull()
    {
        // Given: A non-existing tenant key
        var nonExistingKey = Guid.NewGuid();

        // When: Getting tenant by non-existing key
        var result = await _repository.GetTenantByKeyAsync(nonExistingKey);

        // Then: Should return null
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task RepositoryOperations_CompleteLifecycle()
    {
        // Given: A new tenant and user
        var tenant = new Tenant { Name = "New Tenant", Description = "Test tenant" };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        // When: Adding a role assignment
        var assignment = new UserTenantRoleAssignment
        {
            UserId = "lifecycle-user",
            TenantId = tenant.Id,
            Role = TenantRole.Editor
        };
        await _repository.AddUserTenantRoleAsync(assignment);

        // Then: Assignment should be retrievable
        var retrieved = await _repository.GetUserTenantRoleAsync("lifecycle-user", tenant.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Role, Is.EqualTo(TenantRole.Editor));

        // When: Removing the assignment
        await _repository.RemoveUserTenantRoleAsync(retrieved);

        // Then: Assignment should be gone
        var afterRemoval = await _repository.GetUserTenantRoleAsync("lifecycle-user", tenant.Id);
        Assert.That(afterRemoval, Is.Null);
    }
}
