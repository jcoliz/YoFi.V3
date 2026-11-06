using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Application.Tenancy.Features;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TestControlControllerTests
{
    [TestCase("/TestControl/errors", HttpStatusCode.OK)]
    [TestCase("/TestControl/errors/400", HttpStatusCode.BadRequest)]
    [TestCase("/TestControl/errors/401", HttpStatusCode.Unauthorized)]
    [TestCase("/TestControl/errors/403", HttpStatusCode.Forbidden)]
    [TestCase("/TestControl/errors/404", HttpStatusCode.NotFound)]
    [TestCase("/TestControl/errors/409", HttpStatusCode.Conflict)]
    [TestCase("/TestControl/errors/500", HttpStatusCode.InternalServerError)]
    [TestCase("/TestControl/pagination/strings", HttpStatusCode.OK)]
    public async Task GetErrorTestingEndpoint_Anonymous_ReturnsExpectedStatus(
        string path, HttpStatusCode expectedStatus)
    {
        using var factory = new BaseTestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
    }

    [TestCase(1, HttpStatusCode.OK)]
    [TestCase(0, HttpStatusCode.BadRequest)]
    public async Task ValidateTransactions_Anonymous_ValidatesRequest(
        int amount, HttpStatusCode expectedStatus)
    {
        using var factory = new BaseTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var request = new CollectionRequest<TransactionEditDto>(
            [new(DateOnly.FromDateTime(DateTime.UtcNow), amount, "Test Payee", null, null, null, null)]);

        using var response = await client.PostAsJsonAsync("/TestControl/validate/transactions", request);

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
    }

    [Test]
    public async Task Identify_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new BaseTestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/TestControl/ident", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SeedPayeeRules_ValidRequest_ReturnsCreatedRulesInRequestedWorkspace()
    {
        using var factory = new BaseTestWebApplicationFactory();
        var (username, tenantKey) = await CreateSeedWorkspaceAsync(factory);
        var (_, otherTenantKey) = await CreateSeedWorkspaceAsync(factory);
        using var client = CreateControlClient(factory);
        var request = new CollectionRequest<PayeeMatchingRuleEditDto>(
            [new("Grocery Store", false, "Food:Groceries"), new("^COFFEE", true, "Food:Coffee")]);
        var path = $"/TestControl/users/{username}/workspaces/{tenantKey}/payee-rules/seed";

        using var response = await client.PostAsJsonAsync(path, request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location?.AbsolutePath, Is.EqualTo(path));
        var rules = await response.Content.ReadFromJsonAsync<List<PayeeMatchingRuleResultDto>>();
        Assert.That(rules, Is.Not.Null);
        Assert.That(rules!, Has.Count.EqualTo(2));
        Assert.That(rules.Select(r => new PayeeMatchingRuleEditDto(r.PayeePattern, r.PayeeIsRegex, r.Category)),
            Is.EqualTo(request.Items));
        Assert.That(rules.Select(r => r.Key), Is.Unique);
        Assert.That(rules.All(r => r.Key != Guid.Empty && r.MatchCount == 0 && r.LastUsedAt == null), Is.True);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.Set<Tenant>().SingleAsync(t => t.Key == tenantKey);
        var storedRules = await db.Set<PayeeMatchingRule>()
            .Where(r => r.TenantId == tenant.Id).ToListAsync();
        Assert.That(storedRules.Select(r => r.Key), Is.EquivalentTo(rules.Select(r => r.Key)));
        Assert.That(storedRules.All(r => r.TenantId == tenant.Id), Is.True);

        using var reader = factory.CreateAuthenticatedClient(tenantKey);
        var listed = await reader.GetFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>(
            $"/api/tenant/{tenantKey}/payee-rules");
        Assert.That(listed!.Items.Select(r => r.Key), Is.EquivalentTo(rules.Select(r => r.Key)));

        using var additionalResponse = await client.PostAsJsonAsync(path,
            new CollectionRequest<PayeeMatchingRuleEditDto>([new("Bus", false, "Transport")]));
        Assert.That(additionalResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var updated = await reader.GetFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>(
            $"/api/tenant/{tenantKey}/payee-rules");
        Assert.That(updated!.Items, Has.Count.EqualTo(3));
        Assert.That(updated.Items.Select(r => r.Key), Is.SupersetOf(rules.Select(r => r.Key)));

        using var otherReader = factory.CreateAuthenticatedClient(otherTenantKey);
        var otherRules = await otherReader.GetFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>(
            $"/api/tenant/{otherTenantKey}/payee-rules");
        Assert.That(otherRules!.Items, Is.Empty);
    }

    [TestCase("", false, "Food")]
    [TestCase("Store", false, " ")]
    [TestCase("[", true, "Food")]
    public async Task SeedPayeeRules_InvalidItem_ReturnsBadRequestWithoutCreatingRules(
        string pattern, bool isRegex, string category)
    {
        using var factory = new BaseTestWebApplicationFactory();
        var (username, tenantKey) = await CreateSeedWorkspaceAsync(factory);
        using var client = CreateControlClient(factory);
        var request = new CollectionRequest<PayeeMatchingRuleEditDto>(
            [new("Valid Store", false, "Food"), new(pattern, isRegex, category)]);

        using var response = await client.PostAsJsonAsync(
            $"/TestControl/users/{username}/workspaces/{tenantKey}/payee-rules/seed", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem!.Errors.Keys, Has.Some.Contains("Items[1]"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.That(await db.Set<PayeeMatchingRule>().CountAsync(r => r.Tenant!.Key == tenantKey), Is.Zero);
    }

    [Test]
    public async Task SeedPayeeRules_EmptyCollection_ReturnsEmptyCreatedCollection()
    {
        using var factory = new BaseTestWebApplicationFactory();
        var (username, tenantKey) = await CreateSeedWorkspaceAsync(factory);
        using var client = CreateControlClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/TestControl/users/{username}/workspaces/{tenantKey}/payee-rules/seed",
            new CollectionRequest<PayeeMatchingRuleEditDto>([]));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(await response.Content.ReadFromJsonAsync<List<PayeeMatchingRuleResultDto>>(), Is.Empty);
    }

    [Test]
    public async Task SeedTransactionWithSplits_CreatesTransactionWithOrderedAllocations()
    {
        using var factory = new BaseTestWebApplicationFactory();
        var (username, tenantKey) = await CreateSeedWorkspaceAsync(factory);
        using var client = CreateControlClient(factory);
        var path = $"/TestControl/users/{username}/workspaces/{tenantKey}/transactions/seed-with-splits";
        var request = new
        {
            Payee = "Super Duper Mart",
            Splits = new[]
            {
                new SplitEditDto(-125m, "Groceries", "First memo"),
                new SplitEditDto(-50m, "Supplies", null),
                new SplitEditDto(-75m, "Healthcare:Prescriptions", "Medication"),
                new SplitEditDto(-10m, null, null)
            }
        };

        using var response = await client.PostAsJsonAsync(path, request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location?.AbsolutePath, Is.EqualTo(path));
        var transaction = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(transaction, Is.Not.Null);
        Assert.That(transaction!.Payee, Is.EqualTo("Super Duper Mart"));
        Assert.That(transaction.Amount, Is.EqualTo(-260m));
        Assert.That(transaction.Splits.Select(split => (split.Order, split.Amount, split.Category, split.Memo)),
            Is.EqualTo(new[]
            {
                (0, -125m, "Groceries", "First memo"),
                (1, -50m, "Supplies", (string?)null),
                (2, -75m, "Healthcare:Prescriptions", "Medication"),
                (3, -10m, "", (string?)null)
            }));
        Assert.That(transaction.Splits.Sum(split => split.Amount), Is.EqualTo(transaction.Amount));
    }

    [Test]
    public async Task SeedTransactionWithSplits_EmptySplits_ReturnsBadRequest()
    {
        using var factory = new BaseTestWebApplicationFactory();
        var (username, tenantKey) = await CreateSeedWorkspaceAsync(factory);
        using var client = CreateControlClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/TestControl/users/{username}/workspaces/{tenantKey}/transactions/seed-with-splits",
            new { Payee = "Super Duper Mart", Splits = Array.Empty<object>() });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("regular-user", "__TEST__workspace", true)]
    [TestCase("__TEST__user", "regular-workspace", true)]
    [TestCase("__TEST__user", "__TEST__workspace", false)]
    public async Task SeedPayeeRules_UnsafeWorkspaceAccess_ReturnsForbidden(
        string username, string workspaceName, bool hasAccess)
    {
        using var factory = new BaseTestWebApplicationFactory();
        var (_, tenantKey) = await CreateSeedWorkspaceAsync(factory, username, workspaceName, hasAccess);
        using var client = CreateControlClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/TestControl/users/{username}/workspaces/{tenantKey}/payee-rules/seed",
            new CollectionRequest<PayeeMatchingRuleEditDto>([new("Store", false, "Food")]));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.That(await db.Set<PayeeMatchingRule>().CountAsync(r => r.Tenant!.Key == tenantKey), Is.Zero);
    }

    [TestCase(true, HttpStatusCode.NotFound)]
    [TestCase(false, HttpStatusCode.Forbidden)]
    public async Task SeedPayeeRules_MissingUserOrWorkspace_RejectsRequest(
        bool missingUser, HttpStatusCode expectedStatus)
    {
        using var factory = new BaseTestWebApplicationFactory();
        var (username, tenantKey) = await CreateSeedWorkspaceAsync(factory);
        using var client = CreateControlClient(factory);
        if (missingUser)
        {
            username = "__TEST__missing";
        }
        else
        {
            tenantKey = Guid.NewGuid();
        }

        using var response = await client.PostAsJsonAsync(
            $"/TestControl/users/{username}/workspaces/{tenantKey}/payee-rules/seed",
            new CollectionRequest<PayeeMatchingRuleEditDto>([new("Store", false, "Food")]));

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
    }

    [TestCase(null, HttpStatusCode.Unauthorized)]
    [TestCase("Other", HttpStatusCode.Forbidden)]
    public async Task SeedPayeeRules_WithoutFunctionalTestsRole_RejectsRequest(
        string? role, HttpStatusCode expectedStatus)
    {
        using var factory = new BaseTestWebApplicationFactory();
        var (username, tenantKey) = await CreateSeedWorkspaceAsync(factory);
        using var client = role == null ? factory.CreateClient() : CreateControlClient(factory, role);

        using var response = await client.PostAsJsonAsync(
            $"/TestControl/users/{username}/workspaces/{tenantKey}/payee-rules/seed",
            new CollectionRequest<PayeeMatchingRuleEditDto>([new("Store", false, "Food")]));

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
    }

    private static HttpClient CreateControlClient(
        BaseTestWebApplicationFactory factory, string role = "Functional Tests")
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var validation = options.TokenValidationParameters;
        var token = new JwtSecurityToken(
            issuer: validation.ValidIssuer,
            audience: validation.ValidAudience,
            claims: [new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(validation.IssuerSigningKey, SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<(string Username, Guid TenantKey)> CreateSeedWorkspaceAsync(
        BaseTestWebApplicationFactory factory,
        string? username = null,
        string workspaceName = "__TEST__workspace",
        bool hasAccess = true)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        username ??= $"__TEST__user_{Guid.NewGuid():N}";
        var user = new IdentityUser { UserName = username, Email = $"{username}@test.com" };
        var result = await userManager.CreateAsync(user);
        Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors.Select(e => e.Description)));
        var feature = scope.ServiceProvider.GetRequiredService<TenantFeature>();
        var tenant = await feature.CreateTenantForUserAsync(
            Guid.Parse(user.Id), new TenantEditDto(workspaceName, "Payee rule seeding test"));
        if (!hasAccess)
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var assignment = await db.Set<UserTenantRoleAssignment>().SingleAsync(r => r.UserId == user.Id);
            db.Remove(assignment);
            await db.SaveChangesAsync();
        }

        return (username, tenant.Key);
    }
}
