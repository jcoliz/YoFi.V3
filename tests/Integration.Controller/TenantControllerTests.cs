using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Data;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>
/// Integration tests for TenantController endpoint operations.
/// </summary>
/// <remarks>
/// <para>
/// Unlike tenant-scoped controllers (e.g., TransactionsController), TenantController operates
/// at the user level, managing the user's tenant memberships rather than resources within a tenant.
/// Routes are /api/tenant (not /api/tenant/{tenantKey}/...).
/// </para>
///
/// <para><strong>Authentication:</strong></para>
/// <para>
/// All endpoints require authentication via [Authorize] attribute, but do NOT require
/// tenant-scoped authorization. The controller validates user access to tenants via
/// the TenantFeature, which queries UserTenantRoleAssignment records.
/// </para>
///
/// <para><strong>Test Coverage:</strong></para>
/// <list type="bullet">
/// <item>GET /api/tenant - Retrieve all tenants for authenticated user</item>
/// <item>GET /api/tenant/{key} - Retrieve specific tenant with role verification</item>
/// <item>POST /api/tenant - Create new tenant with user as owner</item>
/// <item>PUT /api/tenant/{tenantKey} - Update tenant (requires Owner role)</item>
/// <item>DELETE /api/tenant/{tenantKey} - Delete tenant (requires Owner role)</item>
/// </list>
/// </remarks>
[TestFixture]
public class TenantControllerTests
{
    private BaseTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _userId;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new BaseTestWebApplicationFactory();
        _userId = Guid.NewGuid();
        // Create an authenticated client without tenant roles (empty array)
        // The TenantController doesn't use tenant-scoped authorization
        _client = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: _userId.ToString(),
            userName: "Test User");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    #region Unauthenticated Tests

    [Test]
    [Explicit("TODO: TestAuthenticationHandler always creates authenticated users. Requires test infrastructure enhancement to support truly unauthenticated requests.")]
    public async Task GetTenants_Unauthenticated_Returns401()
    {
        // Given: An unauthenticated client
        using var unauthClient = _factory.CreateClient();

        // When: Client requests tenants without authentication
        var response = await unauthClient.GetAsync("/api/tenant");

        // Then: 401 Unauthorized should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Explicit("TODO: TestAuthenticationHandler always creates authenticated users. Requires test infrastructure enhancement to support truly unauthenticated requests.")]
    public async Task GetTenant_Unauthenticated_Returns401()
    {
        // Given: An unauthenticated client
        using var unauthClient = _factory.CreateClient();

        // And: A valid tenant key
        var tenantKey = Guid.NewGuid();

        // When: Client requests tenant without authentication
        var response = await unauthClient.GetAsync($"/api/tenant/{tenantKey}");

        // Then: 401 Unauthorized should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Explicit("TODO: TestAuthenticationHandler always creates authenticated users. Requires test infrastructure enhancement to support truly unauthenticated requests.")]
    public async Task CreateTenant_Unauthenticated_Returns401()
    {
        // Given: An unauthenticated client
        using var unauthClient = _factory.CreateClient();

        // And: Valid tenant data
        var tenantDto = new TenantEditDto("Test Tenant", "Test Description");

        // When: Client attempts to create tenant without authentication
        var response = await unauthClient.PostAsJsonAsync("/api/tenant", tenantDto);

        // Then: 401 Unauthorized should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetTenant_InvalidTenantIdFormat_Returns404WithProblemDetails()
    {
        // Given: An authenticated user
        // And: A request with an invalid tenant ID format (not a valid GUID)

        // When: User requests tenant with invalid ID format
        var response = await _client.GetAsync("/api/tenant/not-a-guid");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details (not empty body)
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");

        // And: Response should be valid problem details JSON
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null, "Response should be deserializable as ProblemDetails");
        Assert.That(problemDetails!.Status, Is.EqualTo(404));
    }

    #endregion

    #region GET /api/tenant Tests

    [Test]
    public async Task GetTenants_NoTenants_ReturnsEmptyCollection()
    {
        // Given: An authenticated user with no tenant memberships

        // When: User requests all tenants
        var response = await _client.GetAsync("/api/tenant");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain an empty collection
        var tenants = await response.Content.ReadFromJsonAsync<ICollection<TenantRoleResultDto>>();
        Assert.That(tenants, Is.Not.Null);
        Assert.That(tenants, Is.Empty);
    }

    #endregion

    #region GET /api/tenant/{key} Tests

    [Test]
    public async Task GetTenant_NonExistent_Returns403()
    {
        // Given: An authenticated user
        // And: A tenant key that doesn't exist
        var nonExistentKey = Guid.NewGuid();

        // When: User requests the non-existent tenant
        var response = await _client.GetAsync($"/api/tenant/{nonExistentKey}");

        // Then: 403 Forbidden should be returned (anti-enumeration: same as access denied)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetTenant_WithoutAccess_Returns403()
    {
        // Given: Two different users
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // And: User 1 creates a tenant (has access)
        var tenantKey = await CreateTestTenantWithUserRoleAsync(user1Id, "User 1 Tenant", TenantRole.Owner);

        // When: User 2 attempts to access User 1's tenant
        using var user2Client = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: user2Id.ToString(),
            userName: "User 2");
        var response = await user2Client.GetAsync($"/api/tenant/{tenantKey}");

        // Then: 403 Forbidden should be returned (anti-enumeration: same as non-existent)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region POST /api/tenant Tests

    [Test]
    public async Task CreateTenant_ValidData_Returns201CreatedWithLocationHeader()
    {
        // Given: An authenticated user
        var userIdForTest = Guid.NewGuid();
        using var testClient = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: userIdForTest.ToString(),
            userName: "Test User");

        // And: Valid tenant data
        var tenantDto = new TenantEditDto(
            Name: "New Tenant",
            Description: "This is a new tenant"
        );

        // When: User creates a new tenant
        var response = await testClient.PostAsJsonAsync("/api/tenant", tenantDto);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Response should contain serialized tenant data
        var created = await response.Content.ReadFromJsonAsync<TenantResultDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Key, Is.Not.EqualTo(Guid.Empty));

        // And: Location header should point to the created resource
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location!.ToString(), Does.Contain($"/api/Tenant/{created.Key}"));
    }

    #endregion

    #region PUT /api/tenant/{tenantKey} Tests

    [Test]
    public async Task UpdateTenant_AsOwner_Returns200Ok()
    {
        // Given: An authenticated user with Owner role for a tenant
        var userIdForTest = Guid.NewGuid();
        var tenantKey = await CreateTestTenantWithUserRoleAsync(userIdForTest, "Original Name", TenantRole.Owner);

        using var testClient = _factory.CreateAuthenticatedClient(
            new[] { (tenantKey, TenantRole.Owner) },
            userId: userIdForTest.ToString(),
            userName: "Test User");

        // And: Updated tenant data
        var updateDto = new TenantEditDto(
            Name: "Updated Name",
            Description: "Updated Description"
        );

        // When: User updates the tenant
        var response = await testClient.PutAsJsonAsync($"/api/tenant/{tenantKey}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain serialized tenant data
        var updated = await response.Content.ReadFromJsonAsync<TenantResultDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Key, Is.EqualTo(tenantKey));
    }

    [Test]
    public async Task UpdateTenant_AsEditor_Returns403()
    {
        // Given: A tenant with an owner and an editor
        var ownerId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var tenantKey = await CreateTestTenantWithUserRoleAsync(ownerId, "Test Tenant", TenantRole.Owner);

        // And: Add editor role for different user
        using (var scope2 = _factory.Services.CreateScope())
        {
            var dbContext2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant2 = await dbContext2.Tenants.FirstOrDefaultAsync(t => t.Key == tenantKey);
            var editorAssignment = new UserTenantRoleAssignment
            {
                UserId = editorId.ToString(),
                TenantId = tenant2!.Id,
                Role = TenantRole.Editor
            };
            dbContext2.UserTenantRoleAssignments.Add(editorAssignment);
            await dbContext2.SaveChangesAsync();
        }

        // And: Client authenticated as editor
        using var editorClient = _factory.CreateAuthenticatedClient(
            new[] { (tenantKey, TenantRole.Editor) },
            userId: editorId.ToString(),
            userName: "Editor User");

        // And: Updated tenant data
        var updateDto = new TenantEditDto("Unauthorized Update", "Should not work");

        // When: Editor attempts to update the tenant
        var response = await editorClient.PutAsJsonAsync($"/api/tenant/{tenantKey}", updateDto);

        // Then: 403 Forbidden should be returned (insufficient permissions)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateTenant_NonExistent_Returns403()
    {
        // Given: An authenticated user with Owner role claims for a non-existent tenant
        var userIdForTest = Guid.NewGuid();
        var nonExistentKey = Guid.NewGuid();

        using var testClient = _factory.CreateAuthenticatedClient(
            new[] { (nonExistentKey, TenantRole.Owner) },
            userId: userIdForTest.ToString(),
            userName: "Test User");

        // And: Update data
        var updateDto = new TenantEditDto("Name", "Description");

        // When: User attempts to update non-existent tenant
        var response = await testClient.PutAsJsonAsync($"/api/tenant/{nonExistentKey}", updateDto);

        // Then: 403 Forbidden should be returned (anti-enumeration)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateTenant_WithoutAccess_Returns403()
    {
        // Given: Two different users
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // And: Owner creates a tenant
        var tenantKey = await CreateTestTenantWithUserRoleAsync(ownerId, "Owner's Tenant", TenantRole.Owner);

        // And: Other user attempts to update with fake Owner claims
        using var otherUserClient = _factory.CreateAuthenticatedClient(
            new[] { (tenantKey, TenantRole.Owner) },  // Claims Owner but not in database
            userId: otherUserId.ToString(),
            userName: "Other User");

        // And: Update data
        var updateDto = new TenantEditDto("Hacked Name", "Should not work");

        // When: Other user attempts to update the tenant
        var response = await otherUserClient.PutAsJsonAsync($"/api/tenant/{tenantKey}", updateDto);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region DELETE /api/tenant/{tenantKey} Tests

    [Test]
    public async Task DeleteTenant_AsOwner_Returns204NoContent()
    {
        // Given: An authenticated user with Owner role for a tenant
        var userIdForTest = Guid.NewGuid();
        var tenantKey = await CreateTestTenantWithUserRoleAsync(userIdForTest, "Tenant To Delete", TenantRole.Owner);

        using var testClient = _factory.CreateAuthenticatedClient(
            new[] { (tenantKey, TenantRole.Owner) },
            userId: userIdForTest.ToString(),
            userName: "Test User");

        // When: User deletes the tenant
        var response = await testClient.DeleteAsync($"/api/tenant/{tenantKey}");

        // Then: 204 No Content should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task DeleteTenant_AsEditor_Returns403()
    {
        // Given: A tenant with an owner and an editor
        var ownerId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var tenantKey = await CreateTestTenantWithUserRoleAsync(ownerId, "Test Tenant", TenantRole.Owner);

        // And: Add editor role for different user
        using (var scope3 = _factory.Services.CreateScope())
        {
            var dbContext3 = scope3.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant3 = await dbContext3.Tenants.FirstOrDefaultAsync(t => t.Key == tenantKey);
            var editorAssignment = new UserTenantRoleAssignment
            {
                UserId = editorId.ToString(),
                TenantId = tenant3!.Id,
                Role = TenantRole.Editor
            };
            dbContext3.UserTenantRoleAssignments.Add(editorAssignment);
            await dbContext3.SaveChangesAsync();
        }

        // And: Client authenticated as editor
        using var editorClient = _factory.CreateAuthenticatedClient(
            new[] { (tenantKey, TenantRole.Editor) },
            userId: editorId.ToString(),
            userName: "Editor User");

        // When: Editor attempts to delete the tenant
        var response = await editorClient.DeleteAsync($"/api/tenant/{tenantKey}");

        // Then: 403 Forbidden should be returned (insufficient permissions)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteTenant_NonExistent_Returns403()
    {
        // Given: An authenticated user with Owner role claims for a non-existent tenant
        var userIdForTest = Guid.NewGuid();
        var nonExistentKey = Guid.NewGuid();

        using var testClient = _factory.CreateAuthenticatedClient(
            new[] { (nonExistentKey, TenantRole.Owner) },
            userId: userIdForTest.ToString(),
            userName: "Test User");

        // When: User attempts to delete non-existent tenant
        var response = await testClient.DeleteAsync($"/api/tenant/{nonExistentKey}");

        // Then: 403 Forbidden should be returned (anti-enumeration)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteTenant_WithoutAccess_Returns403()
    {
        // Given: Two different users
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // And: Owner creates a tenant
        var tenantKey = await CreateTestTenantWithUserRoleAsync(ownerId, "Owner's Tenant", TenantRole.Owner);

        // And: Other user attempts to delete with fake Owner claims
        using var otherUserClient = _factory.CreateAuthenticatedClient(
            new[] { (tenantKey, TenantRole.Owner) },  // Claims Owner but not in database
            userId: otherUserId.ToString(),
            userName: "Other User");

        // When: Other user attempts to delete the tenant
        var response = await otherUserClient.DeleteAsync($"/api/tenant/{tenantKey}");

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }


    #endregion


    #region Helper Methods

    /// <summary>
    /// Creates a tenant and assigns a user with the specified role
    /// </summary>
    private async Task<Guid> CreateTestTenantWithUserRoleAsync(Guid userId, string name, TenantRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = name,
            Description = $"Test tenant: {name}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Set<Tenant>().Add(tenant);
        await dbContext.SaveChangesAsync();

        var roleAssignment = new UserTenantRoleAssignment
        {
            UserId = userId.ToString(),
            TenantId = tenant.Id,
            Role = role
        };

        dbContext.Set<UserTenantRoleAssignment>().Add(roleAssignment);
        await dbContext.SaveChangesAsync();

        return tenant.Key;
    }

    #endregion
}

