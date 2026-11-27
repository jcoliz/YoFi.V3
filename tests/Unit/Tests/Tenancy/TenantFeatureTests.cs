using Moq;
using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Application.Tenancy.Features;
using YoFi.V3.Entities.Tenancy.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Tests.Unit.Application.Tenancy;

[TestFixture]
public class TenantFeatureTests
{
    private Mock<ITenantRepository> _repo = null!;
    private Mock<TimeProvider> _time = null!;
    private TenantFeature _feature = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new Mock<ITenantRepository>();
        _time = new Mock<TimeProvider>();
        _time.Setup(time => time.GetUtcNow())
            .Returns(new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));
        _feature = new TenantFeature(_repo.Object, _time.Object);
    }

    [Test]
    public async Task CreateTenantForUserAsync_AddsTenantAndOwnerRole()
    {
        UserTenantRoleAssignment? assignment = null;
        _repo.Setup(repo => repo.AddTenantAsync(It.IsAny<Tenant>()))
            .Returns((Tenant tenant) =>
            {
                tenant.Id = 42;
                return Task.FromResult(tenant);
            });
        _repo.Setup(repo => repo.AddUserTenantRoleAsync(It.IsAny<UserTenantRoleAssignment>()))
            .Callback((UserTenantRoleAssignment value) => assignment = value)
            .Returns(Task.CompletedTask);

        var result = await _feature.CreateTenantForUserAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new TenantEditDto("Workspace", "Description"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Name, Is.EqualTo("Workspace"));
            Assert.That(result.Description, Is.EqualTo("Description"));
            Assert.That(result.CreatedAt, Is.EqualTo(_time.Object.GetUtcNow()));
            Assert.That(assignment!.UserId, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
            Assert.That(assignment.TenantId, Is.EqualTo(42));
            Assert.That(assignment.Role, Is.EqualTo(TenantRole.Owner));
        }
    }

    [Test]
    public async Task GetTenantsForUserAsyncMapsRoles()
    {
        var tenant = Tenant("One", TenantRole.Owner);
        _repo.Setup(repo => repo.GetUserTenantRolesAsync(It.IsAny<string>()))
            .ReturnsAsync([tenant]);

        var result = await _feature.GetTenantsForUserAsync(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.That(result.Single().Name, Is.EqualTo("One"));
    }

    [Test]
    public async Task GetTenantForUserAsyncReturnsTenantRole()
    {
        var tenant = new Tenant { Id = 7, Key = Guid.NewGuid(), Name = "One", Description = "Desc" };
        _repo.Setup(repo => repo.GetTenantByKeyAsync(tenant.Key)).ReturnsAsync(tenant);
        _repo.Setup(repo => repo.GetUserTenantRoleAsync(It.IsAny<string>(), 7)).ReturnsAsync(new UserTenantRoleAssignment
        {
            UserId = "user", TenantId = 7, Role = TenantRole.Editor
        });

        var result = await _feature.GetTenantForUserAsync(Guid.Parse("33333333-3333-3333-3333-333333333333"), tenant.Key);

        Assert.That(result.Role, Is.EqualTo(TenantRole.Editor));
    }

    [Test]
    public async Task GetTenantForUserAsyncMissingTenantThrows()
    {
        _repo.Setup(repo => repo.GetTenantByKeyAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);

        await Assert.ThrowsAsync<TenantNotFoundException>(() => _feature.GetTenantForUserAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public async Task GetTenantForUserAsyncNoRoleThrowsAccessDenied()
    {
        var tenant = new Tenant { Id = 7, Key = Guid.NewGuid(), Name = "One" };
        _repo.Setup(repo => repo.GetTenantByKeyAsync(tenant.Key)).ReturnsAsync(tenant);
        _repo.Setup(repo => repo.GetUserTenantRoleAsync(It.IsAny<string>(), 7)).ReturnsAsync((UserTenantRoleAssignment?)null);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => _feature.GetTenantForUserAsync(Guid.NewGuid(), tenant.Key));
    }

    [Test]
    public async Task UpdateTenantForUserAsyncUpdatesProperties()
    {
        var tenant = new Tenant { Id = 7, Key = Guid.NewGuid(), Name = "Old", Description = "Old desc", CreatedAt = _time.Object.GetUtcNow() };
        _repo.Setup(repo => repo.GetTenantByKeyAsync(tenant.Key)).ReturnsAsync(tenant);
        _repo.Setup(repo => repo.GetUserTenantRoleAsync(It.IsAny<string>(), 7)).ReturnsAsync(new UserTenantRoleAssignment { Role = TenantRole.Owner });

        var result = await _feature.UpdateTenantForUserAsync(Guid.NewGuid(), tenant.Key,
            new TenantEditDto("New", "New desc"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Name, Is.EqualTo("New"));
            Assert.That(result.Description, Is.EqualTo("New desc"));
            Assert.That(tenant.Name, Is.EqualTo("New"));
        }
        _repo.Verify(repo => repo.UpdateTenantAsync(tenant), Times.Once);
    }

    [Test]
    public async Task UpdateTenantForUserAsyncMissingTenantThrows()
    {
        _repo.Setup(repo => repo.GetTenantByKeyAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);

        await Assert.ThrowsAsync<TenantNotFoundException>(() => _feature.UpdateTenantForUserAsync(Guid.NewGuid(), Guid.NewGuid(), new TenantEditDto("Name", "")));
    }

    [Test]
    public async Task UpdateTenantForUserAsyncNoRoleThrowsAccessDenied()
    {
        var tenant = new Tenant { Id = 7, Key = Guid.NewGuid() };
        _repo.Setup(repo => repo.GetTenantByKeyAsync(tenant.Key)).ReturnsAsync(tenant);
        _repo.Setup(repo => repo.GetUserTenantRoleAsync(It.IsAny<string>(), 7)).ReturnsAsync((UserTenantRoleAssignment?)null);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => _feature.UpdateTenantForUserAsync(Guid.NewGuid(), tenant.Key, new TenantEditDto("Name", "")));
    }

    [Test]
    public async Task DeleteTenantForUserAsyncDeletesAuthorizedTenant()
    {
        var tenant = new Tenant { Id = 7, Key = Guid.NewGuid() };
        _repo.Setup(repo => repo.GetTenantByKeyAsync(tenant.Key)).ReturnsAsync(tenant);
        _repo.Setup(repo => repo.GetUserTenantRoleAsync(It.IsAny<string>(), 7)).ReturnsAsync(new UserTenantRoleAssignment { Role = TenantRole.Owner });

        await _feature.DeleteTenantForUserAsync(Guid.NewGuid(), tenant.Key);

        _repo.Verify(repo => repo.DeleteTenantAsync(tenant), Times.Once);
    }

    [Test]
    public async Task DeleteTenantForUserAsyncMissingTenantThrows()
    {
        _repo.Setup(repo => repo.GetTenantByKeyAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);

        await Assert.ThrowsAsync<TenantNotFoundException>(() => _feature.DeleteTenantForUserAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public async Task DeleteTenantForUserAsyncNoRoleThrowsAccessDenied()
    {
        var tenant = new Tenant { Id = 7, Key = Guid.NewGuid() };
        _repo.Setup(repo => repo.GetTenantByKeyAsync(tenant.Key)).ReturnsAsync(tenant);
        _repo.Setup(repo => repo.GetUserTenantRoleAsync(It.IsAny<string>(), 7)).ReturnsAsync((UserTenantRoleAssignment?)null);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => _feature.DeleteTenantForUserAsync(Guid.NewGuid(), tenant.Key));
    }

    [Test]
    public async Task AdminMethodsCoverCreateLookupRolePrefixAndBulkDelete()
    {
        var tenant = new Tenant { Id = 7, Key = Guid.NewGuid(), Name = "__TEST__One" };
        _repo.Setup(repo => repo.AddTenantAsync(It.IsAny<Tenant>())).ReturnsAsync(tenant);
        _repo.Setup(repo => repo.GetTenantByKeyAsync(tenant.Key)).ReturnsAsync(tenant);
        _repo.Setup(repo => repo.GetTenantsByNamePrefixAsync("__TEST__")).ReturnsAsync([tenant]);

        var created = await _feature.CreateTenantAsync(new TenantEditDto("Admin", ""));
        var lookedUp = await _feature.GetTenantByKeyAsync(tenant.Key);
        await _feature.AddUserTenantRoleAsync(Guid.NewGuid(), 7, TenantRole.Viewer);
        var prefix = await _feature.GetTenantsByNamePrefixAsync("__TEST__");
        await _feature.DeleteTenantsByKeysAsync([tenant.Key, Guid.NewGuid()]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Name, Is.EqualTo("Admin"));
            Assert.That(lookedUp, Is.SameAs(tenant));
            Assert.That(prefix.Single().Name, Is.EqualTo("__TEST__One"));
        }
        _repo.Verify(repo => repo.AddUserTenantRoleAsync(It.Is<UserTenantRoleAssignment>(role => role.TenantId == 7 && role.Role == TenantRole.Viewer)), Times.Once);
        _repo.Verify(repo => repo.DeleteTenantAsync(tenant), Times.Once);
    }

    [Test]
    public async Task HasUserTenantRoleAsyncReturnsPresence()
    {
        _repo.Setup(repo => repo.GetUserTenantRoleAsync(It.IsAny<string>(), 42)).ReturnsAsync(new UserTenantRoleAssignment { Role = TenantRole.Owner });

        Assert.That(await _feature.HasUserTenantRoleAsync(Guid.Parse("44444444-4444-4444-4444-444444444444"), 42), Is.True);

        _repo.Setup(repo => repo.GetUserTenantRoleAsync(It.IsAny<string>(), 99)).ReturnsAsync((UserTenantRoleAssignment?)null);
        Assert.That(await _feature.HasUserTenantRoleAsync(Guid.Parse("44444444-4444-4444-4444-444444444444"), 99), Is.False);
    }

    private static UserTenantRoleAssignment Tenant(string name, TenantRole role)
    {
        var tenant = new Tenant { Key = Guid.NewGuid(), Name = name, CreatedAt = DateTimeOffset.UtcNow };
        return new UserTenantRoleAssignment { Tenant = tenant, Role = role };
    }
}
