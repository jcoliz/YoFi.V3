using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NuxtIdentity.Core.Models;
using YoFi.V3.Application.Dto;
using YoFi.V3.Controllers.Tenancy.Api.Dto;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>
/// End-to-end integration tests using built-in ASP.NET Identity authentication.
/// </summary>
/// <remarks>
/// <para>
/// Unlike other integration tests that use TestAuthenticationHandler, these tests use
/// the actual ASP.NET Identity + NuxtIdentity authentication stack to verify the complete
/// authentication and authorization flow from registration through API operations.
/// </para>
///
/// <para><strong>Test Scope:</strong></para>
/// <para>
/// These tests focus on the happy path only - successful registration, login, tenant creation,
/// and transaction management. Edge cases, validation errors, and failure scenarios are covered
/// by other test suites that use TestAuthenticationHandler for more focused testing.
/// </para>
///
/// <para><strong>Authentication Flow:</strong></para>
/// <list type="number">
/// <item>Register a new user via POST /api/auth/signup</item>
/// <item>Login with credentials via POST /api/auth/login to get JWT tokens</item>
/// <item>Use access token in Authorization header for authenticated requests</item>
/// <item>Create tenant and perform operations within that tenant</item>
/// </list>
/// </remarks>
[TestFixture]
public class EndToEndAuthenticationTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _dbPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"e2e_test_{Guid.NewGuid()}.db");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    var configOverrides = new Dictionary<string, string?>
                    {
                        ["Application:Version"] = "e2e-test-version",
                        ["Application:Environment"] = "Local",
                        ["Application:AllowedCorsOrigins:0"] = "http://localhost:3000",
                        ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}"
                    };
                    config.AddInMemoryCollection(configOverrides);
                });
            });

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();

        // Clean up the temporary database file
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    // WARNING: This test is flaky, due to DbContext concurrency issues.
    // Investigate and fix root cause to improve reliability.
    public async Task CompleteWorkflow_RegisterLoginAddTransactions_AutomaticTenantProvisioning()
    {
        // Given: A new user with unique credentials
        var testId = Guid.NewGuid().ToString("N")[..8];
        var email = $"e2etest_{testId}@example.com";
        var username = $"e2etest_{testId}";
        var password = "TestPassword123!";

        // When: User registers a new account
        var signupRequest = new
        {
            email = email,
            username = username,
            password = password,
            confirmPassword = password
        };
        var signupResponse = await _client.PostAsJsonAsync("/api/auth/signup", signupRequest);

        // Then: Registration should succeed
        Assert.That(signupResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain user information
        // TODO: NuxtIdentity signup endpoint returns LoginResponse instead of having a dedicated SignupResponse type.
        // Consider adding a SignupResponse type to NuxtIdentity.Core.Models for better API semantics.
        var signupResult = await signupResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(signupResult, Is.Not.Null);
        Assert.That(signupResult!.User, Is.Not.Null);
        Assert.That(signupResult.User.Email, Is.EqualTo(email));

        // When: User logs in with credentials
        var loginRequest = new
        {
            username = username,
            password = password
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Then: Login should succeed
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain JWT tokens
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginResult, Is.Not.Null);
        Assert.That(loginResult!.Token, Is.Not.Null);
        Assert.That(loginResult.Token.AccessToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResult.Token.RefreshToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResult.User, Is.Not.Null);
        Assert.That(loginResult.User.Name, Is.EqualTo(username));

        // When: User creates an authenticated client with access token from initial login
        using var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {loginResult.Token.AccessToken}");

        // And: User retrieves their tenant list
        var getTenantsResponse = await authenticatedClient.GetAsync("/api/tenant");

        // Then: Request should succeed
        Assert.That(getTenantsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: User should have exactly one automatically provisioned tenant
        var tenants = await getTenantsResponse.Content.ReadFromJsonAsync<ICollection<TenantRoleResultDto>>();
        Assert.That(tenants, Is.Not.Null);
        Assert.That(tenants!.Count, Is.EqualTo(1), "User should have exactly one auto-provisioned tenant");

        // And: The tenant should have expected properties
        var autoTenant = tenants.First();
        Assert.That(autoTenant.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(autoTenant.Name, Does.Contain(username), "Auto-provisioned tenant name should contain username");
        Assert.That(autoTenant.Role, Is.EqualTo(TenantRole.Owner), "User should be Owner of auto-provisioned tenant");

        // When: User adds transactions to the auto-provisioned tenant (without needing to refresh token)
        var transaction1 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.50m,
            Payee: "Test Payee 1"
        );
        var transaction2 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Amount: 250.75m,
            Payee: "Test Payee 2"
        );

        var addTx1Response = await authenticatedClient.PostAsJsonAsync(
            $"/api/tenant/{autoTenant.Key}/transactions",
            transaction1);
        var addTx2Response = await authenticatedClient.PostAsJsonAsync(
            $"/api/tenant/{autoTenant.Key}/transactions",
            transaction2);

        // Then: Both transactions should be created successfully
        Assert.That(addTx1Response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(addTx2Response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Responses should contain transaction details
        var createdTx1 = await addTx1Response.Content.ReadFromJsonAsync<TransactionResultDto>();
        var createdTx2 = await addTx2Response.Content.ReadFromJsonAsync<TransactionResultDto>();
        Assert.That(createdTx1, Is.Not.Null);
        Assert.That(createdTx1!.Payee, Is.EqualTo("Test Payee 1"));
        Assert.That(createdTx1.Amount, Is.EqualTo(100.50m));
        Assert.That(createdTx2, Is.Not.Null);
        Assert.That(createdTx2!.Payee, Is.EqualTo("Test Payee 2"));
        Assert.That(createdTx2.Amount, Is.EqualTo(250.75m));

        // When: User retrieves all transactions for the auto-provisioned tenant
        var getTransactionsResponse = await authenticatedClient.GetAsync(
            $"/api/tenant/{autoTenant.Key}/transactions");

        // Then: Request should succeed
        Assert.That(getTransactionsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain both transactions
        var retrievedTransactions = await getTransactionsResponse.Content
            .ReadFromJsonAsync<ICollection<TransactionResultDto>>();
        Assert.That(retrievedTransactions, Is.Not.Null);
        Assert.That(retrievedTransactions!.Count, Is.EqualTo(2));

        // And: Transactions should match the created data
        var tx1 = retrievedTransactions.FirstOrDefault(t => t.Payee == "Test Payee 1");
        var tx2 = retrievedTransactions.FirstOrDefault(t => t.Payee == "Test Payee 2");
        Assert.That(tx1, Is.Not.Null, "Transaction 1 should be in results");
        Assert.That(tx1!.Amount, Is.EqualTo(100.50m));
        Assert.That(tx1.Key, Is.EqualTo(createdTx1.Key));
        Assert.That(tx2, Is.Not.Null, "Transaction 2 should be in results");
        Assert.That(tx2!.Amount, Is.EqualTo(250.75m));
        Assert.That(tx2.Key, Is.EqualTo(createdTx2.Key));
    }

    // Note: Using NuxtIdentity.Core.Models for LoginResponse, RefreshResponse, TokenPair, UserInfo, etc.
}
