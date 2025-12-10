using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Controllers.Tenancy;
using YoFi.V3.Data;
using YoFi.V3.Entities.Tenancy;
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

    [Test]
    public async Task GetTenants_WithMultipleTenants_ReturnsAllTenants()
    {
        // Given: An authenticated user
        var userIdForTest = Guid.NewGuid();
        using var testClient = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: userIdForTest.ToString(),
            userName: "Test User");

        // And: User has access to multiple tenants with different roles
        var tenant1Key = await CreateTestTenantWithUserRoleAsync(userIdForTest, "Tenant One", TenantRole.Owner);
        var tenant2Key = await CreateTestTenantWithUserRoleAsync(userIdForTest, "Tenant Two", TenantRole.Editor);
        var tenant3Key = await CreateTestTenantWithUserRoleAsync(userIdForTest, "Tenant Three", TenantRole.Viewer);

        // When: User requests all tenants
        var response = await testClient.GetAsync("/api/tenant");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain all three tenants
        var tenants = await response.Content.ReadFromJsonAsync<ICollection<TenantRoleResultDto>>();
        Assert.That(tenants, Is.Not.Null);
        Assert.That(tenants!.Count, Is.EqualTo(3));

        // And: Each tenant should have correct key and role
        var tenant1 = tenants.FirstOrDefault(t => t.Key == tenant1Key);
        Assert.That(tenant1, Is.Not.Null, "Tenant One should be in results");
        Assert.That(tenant1!.Role, Is.EqualTo(TenantRole.Owner));
        Assert.That(tenant1.Name, Is.EqualTo("Tenant One"));

        var tenant2 = tenants.FirstOrDefault(t => t.Key == tenant2Key);
        Assert.That(tenant2, Is.Not.Null, "Tenant Two should be in results");
        Assert.That(tenant2!.Role, Is.EqualTo(TenantRole.Editor));

        var tenant3 = tenants.FirstOrDefault(t => t.Key == tenant3Key);
        Assert.That(tenant3, Is.Not.Null, "Tenant Three should be in results");
        Assert.That(tenant3!.Role, Is.EqualTo(TenantRole.Viewer));
    }

    [Test]
    public async Task GetTenants_OnlyReturnsUsersTenants()
    {
        // Given: Multiple users exist with different tenant access
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // And: User 1 has access to tenant A and B
        var tenantAKey = await CreateTestTenantWithUserRoleAsync(user1Id, "Tenant A", TenantRole.Owner);
        var tenantBKey = await CreateTestTenantWithUserRoleAsync(user1Id, "Tenant B", TenantRole.Editor);

        // And: User 2 has access to tenant C only
        var tenantCKey = await CreateTestTenantWithUserRoleAsync(user2Id, "Tenant C", TenantRole.Owner);

        // When: User 1 requests their tenants
        using var user1Client = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: user1Id.ToString(),
            userName: "User 1");
        var response = await user1Client.GetAsync("/api/tenant");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should only contain User 1's tenants (A and B)
        var tenants = await response.Content.ReadFromJsonAsync<ICollection<TenantRoleResultDto>>();
        Assert.That(tenants, Is.Not.Null);
        Assert.That(tenants!.Count, Is.EqualTo(2));
        Assert.That(tenants.Any(t => t.Key == tenantAKey), Is.True, "Should contain Tenant A");
        Assert.That(tenants.Any(t => t.Key == tenantBKey), Is.True, "Should contain Tenant B");
        Assert.That(tenants.Any(t => t.Key == tenantCKey), Is.False, "Should NOT contain Tenant C (belongs to User 2)");
    }

    #endregion

    #region GET /api/tenant/{key} Tests

    [Test]
    public async Task GetTenant_WithAccess_ReturnsTenantWithRole()
    {
        // Given: An authenticated user
        var userIdForTest = Guid.NewGuid();
        using var testClient = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: userIdForTest.ToString(),
            userName: "Test User");

        // And: User has Editor access to a tenant
        var tenantKey = await CreateTestTenantWithUserRoleAsync(userIdForTest, "My Tenant", TenantRole.Editor);

        // When: User requests the specific tenant by key
        var response = await testClient.GetAsync($"/api/tenant/{tenantKey}");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain tenant details with user's role
        var tenant = await response.Content.ReadFromJsonAsync<TenantRoleResultDto>();
        Assert.That(tenant, Is.Not.Null);
        Assert.That(tenant!.Key, Is.EqualTo(tenantKey));
        Assert.That(tenant.Name, Is.EqualTo("My Tenant"));
        Assert.That(tenant.Role, Is.EqualTo(TenantRole.Editor));
        Assert.That(tenant.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
    }

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
    public async Task CreateTenant_ValidData_ReturnsCreatedTenant()
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

        // And: Response should contain the created tenant
        var created = await response.Content.ReadFromJsonAsync<TenantResultDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(created.Name, Is.EqualTo("New Tenant"));
        Assert.That(created.Description, Is.EqualTo("This is a new tenant"));
        Assert.That(created.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));

        // And: Location header should point to the created resource
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location!.ToString(), Does.Contain($"/api/Tenant/{created.Key}"));

        // And: User should be able to retrieve the created tenant
        var getResponse = await testClient.GetAsync($"/api/tenant/{created.Key}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: User should have Owner role for the new tenant
        var retrievedTenant = await getResponse.Content.ReadFromJsonAsync<TenantRoleResultDto>();
        Assert.That(retrievedTenant!.Role, Is.EqualTo(TenantRole.Owner));
    }

    [Test]
    public async Task CreateTenant_CreatesUserTenantRoleAssignment()
    {
        // Given: An authenticated user
        var userIdForTest = Guid.NewGuid();
        using var testClient = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: userIdForTest.ToString(),
            userName: "Test User");

        // And: Valid tenant data
        var tenantDto = new TenantEditDto("Tenant With Role", "Test role assignment");

        // When: User creates a new tenant
        var response = await testClient.PostAsJsonAsync("/api/tenant", tenantDto);
        var created = await response.Content.ReadFromJsonAsync<TenantResultDto>();

        // Then: Tenant should be created
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: UserTenantRoleAssignment should exist in database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Key == created!.Key);
        var roleAssignment = await dbContext.UserTenantRoleAssignments
            .FirstOrDefaultAsync(utr => utr.UserId == userIdForTest.ToString() && utr.TenantId == tenant!.Id);

        Assert.That(roleAssignment, Is.Not.Null, "UserTenantRoleAssignment should exist");
        Assert.That(roleAssignment!.Role, Is.EqualTo(TenantRole.Owner), "User should be assigned Owner role");
    }

    [Test]
    public async Task CreateTenant_MultipleUsers_EachCanCreateTenants()
    {
        // Given: Two different authenticated users
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        using var user1Client = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: user1Id.ToString(),
            userName: "User 1");
        using var user2Client = _factory.CreateAuthenticatedClient(
            Array.Empty<(Guid, TenantRole)>(),
            userId: user2Id.ToString(),
            userName: "User 2");

        // When: User 1 creates a tenant
        var tenant1Dto = new TenantEditDto("User 1 Tenant", "User 1's tenant");
        var response1 = await user1Client.PostAsJsonAsync("/api/tenant", tenant1Dto);
        var created1 = await response1.Content.ReadFromJsonAsync<TenantResultDto>();

        // And: User 2 creates a tenant
        var tenant2Dto = new TenantEditDto("User 2 Tenant", "User 2's tenant");
        var response2 = await user2Client.PostAsJsonAsync("/api/tenant", tenant2Dto);
        var created2 = await response2.Content.ReadFromJsonAsync<TenantResultDto>();

        // Then: Both creations should succeed
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Each user can only access their own tenant
        var user1Tenants = await (await user1Client.GetAsync("/api/tenant"))
            .Content.ReadFromJsonAsync<ICollection<TenantRoleResultDto>>();
        var user2Tenants = await (await user2Client.GetAsync("/api/tenant"))
            .Content.ReadFromJsonAsync<ICollection<TenantRoleResultDto>>();

        Assert.That(user1Tenants!.Any(t => t.Key == created1!.Key), Is.True);
        Assert.That(user1Tenants.Any(t => t.Key == created2!.Key), Is.False);
        Assert.That(user2Tenants!.Any(t => t.Key == created2!.Key), Is.True);
        Assert.That(user2Tenants.Any(t => t.Key == created1!.Key), Is.False);
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
