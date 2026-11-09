using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Models;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

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
/// These tests cover registration, login, tenant creation, transaction management,
/// and Scalar's token exchange, including refresh rotation and rejected token requests.
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
    private BaseTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new BaseTestWebApplicationFactory(new Dictionary<string, string?>
        {
            ["Application:Version"] = "e2e-test-version",
            ["Application:Environment"] = "Local",
            ["Application:AllowedCorsOrigins:0"] = "http://localhost:3000"
        }, useTestAuthentication: false);

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task ScalarToken_WithValidCredentials_AuthorizesProtectedRequests()
    {
        var username = $"scalar_{Guid.NewGuid():N}";
        const string password = "TestPassword123!";
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new
        {
            email = $"{username}@example.com",
            username,
            password,
            confirmPassword = password
        });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var logs = new CapturingLoggerProvider();
        _factory.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password
        });
        var response = await _client.PostAsync("/api/auth/token", form);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("access_token").GetString();
        var refreshToken = body.RootElement.GetProperty("refresh_token").GetString();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            Assert.That(body.RootElement.GetProperty("token_type").GetString(), Is.EqualTo("Bearer"));
            Assert.That(refreshToken, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
            Assert.That(response.Headers.Pragma.Select(p => p.Name), Does.Contain("no-cache"));
            Assert.That(response.Headers.TryGetValues("Set-Cookie", out _), Is.False);
        }

        using var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var tenants = await authenticatedClient.GetAsync("/api/tenant");
        Assert.That(tenants.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        for (var rotation = 0; rotation < 2; rotation++)
        {
            using var refreshForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken!
            });
            var refreshed = await _client.PostAsync("/api/auth/token", refreshForm);
            Assert.That(refreshed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using var refreshedBody = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
            var newAccessToken = refreshedBody.RootElement.GetProperty("access_token").GetString();
            var newRefreshToken = refreshedBody.RootElement.GetProperty("refresh_token").GetString();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(newAccessToken, Is.Not.Null.And.Not.Empty);
                Assert.That(newRefreshToken, Is.Not.Null.And.Not.Empty.And.Not.EqualTo(refreshToken));
                Assert.That(refreshedBody.RootElement.GetProperty("token_type").GetString(), Is.EqualTo("Bearer"));
                Assert.That(refreshed.Headers.CacheControl?.NoStore, Is.True);
                Assert.That(refreshed.Headers.TryGetValues("Set-Cookie", out _), Is.False);
            }

            authenticatedClient.DefaultRequestHeaders.Authorization = new("Bearer", newAccessToken);
            var authorized = await authenticatedClient.GetAsync("/api/tenant");
            Assert.That(authorized.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using var reusedForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken!
            });
            var reused = await _client.PostAsync("/api/auth/token", reusedForm);
            Assert.That(reused.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            using var reusedBody = JsonDocument.Parse(await reused.Content.ReadAsStringAsync());
            Assert.That(reusedBody.RootElement.GetProperty("error").GetString(), Is.EqualTo("invalid_grant"));
            Assert.That(reusedBody.RootElement.TryGetProperty("access_token", out _), Is.False);
            refreshToken = newRefreshToken;
        }

        var logout = await _client.PostAsJsonAsync("/api/auth/logout", new { refreshToken });
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var revokedForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken!
        });
        var revoked = await _client.PostAsync("/api/auth/token", revokedForm);
        Assert.That(revoked.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var revokedBody = JsonDocument.Parse(await revoked.Content.ReadAsStringAsync());
        Assert.That(revokedBody.RootElement.GetProperty("error").GetString(), Is.EqualTo("invalid_grant"));

        using var incorrectPassword = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = "IncorrectPassword!"
        });
        var failure = await _client.PostAsync("/api/auth/token", incorrectPassword);
        Assert.That(failure.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var error = JsonDocument.Parse(await failure.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("error").GetString(), Is.EqualTo("invalid_grant"));

        var tokenOkLogs = logs.Entries.Where(e => e.Category == typeof(Controllers.AuthController).FullName
            && e.EventId.Name == "LogTokenOk").ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokenOkLogs, Has.Length.EqualTo(3), "Only the login and two successful refreshes should log OK.");
            Assert.That(tokenOkLogs.Select(e => e.EventId.Id), Is.All.EqualTo(4));
            Assert.That(tokenOkLogs.Select(e => e.Level), Is.All.EqualTo(LogLevel.Information));
            Assert.That(tokenOkLogs.Select(e => e.Message), Is.All.EqualTo("Token: OK"));
        }
    }

    [TestCase("", "invalid_request")]
    [TestCase("grant_type=password", "invalid_request")]
    [TestCase("grant_type=password&username=test", "invalid_request")]
    [TestCase("grant_type=password&password=test", "invalid_request")]
    [TestCase("grant_type=password&username=test&password=", "invalid_request")]
    [TestCase("grant_type=client_credentials", "unsupported_grant_type")]
    [TestCase("grant_type=refresh_token", "invalid_request")]
    [TestCase("grant_type=refresh_token&refresh_token=", "invalid_request")]
    [TestCase("grant_type=refresh_token&refresh_token=%20", "invalid_request")]
    [TestCase("grant_type=refresh_token&refresh_token=invalid", "invalid_grant")]
    [TestCase("grant_type=password&username=nonexistent_scalar_user&password=incorrect", "invalid_grant")]
    public async Task ScalarToken_WithInvalidForm_ReturnsOAuthError(string form, string expectedError)
    {
        using var content = new StringContent(form, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await _client.PostAsync("/api/auth/token", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(body.RootElement.GetProperty("error").GetString(), Is.EqualTo(expectedError));
            Assert.That(body.RootElement.GetProperty("error_description").GetString(), Is.Not.Empty);
            Assert.That(body.RootElement.TryGetProperty("access_token", out _), Is.False);
            Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        }
    }

    [Test]
    public async Task ScalarToken_WithJson_ReturnsUnsupportedMediaType()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/token", new
        {
            grant_type = "password",
            username = "test",
            password = "test"
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
    }

    [Test]
    public async Task OpenApi_OffersScalarLoginAsAlternativeToBearer()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        var login = schemes.GetProperty("ScalarLogin");
        var flow = login.GetProperty("flows").GetProperty("password");
        var paths = root.GetProperty("paths");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(login.GetProperty("type").GetString(), Is.EqualTo("oauth2"));
            Assert.That(flow.GetProperty("tokenUrl").GetString(), Is.EqualTo("/api/auth/token"));
            Assert.That(flow.GetProperty("refreshUrl").GetString(), Is.EqualTo("/api/auth/token"));
            Assert.That(flow.GetProperty("scopes").EnumerateObject(), Is.Empty);
            Assert.That(schemes.GetProperty("Bearer").GetProperty("scheme").GetString(), Is.EqualTo("bearer"));
        }

        var requirements = paths.GetProperty("/api/Tenant").GetProperty("get").GetProperty("security")
            .EnumerateArray().ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requirements.Any(r => r.TryGetProperty("ScalarLogin", out _)
                && r.EnumerateObject().Count() == 1), Is.True);
            Assert.That(requirements.Any(r => r.TryGetProperty("Bearer", out _)
                && r.EnumerateObject().Count() == 1), Is.True);
        }

        foreach (var path in new[] { "/api/auth/login", "/api/auth/token" })
        {
            var operation = paths.GetProperty(path).GetProperty("post");
            Assert.That(!operation.TryGetProperty("security", out var security)
                || security.GetArrayLength() == 0, Is.True, $"{path} must allow anonymous requests");
        }
        var tokenContent = paths.GetProperty("/api/auth/token").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content");
        Assert.That(tokenContent.TryGetProperty("application/x-www-form-urlencoded", out _), Is.True);

        var scalar = await _client.GetStringAsync("/scalar/v1");
        Assert.That(scalar, Does.Contain("\"persistAuth\":true"));
        Assert.That(scalar, Does.Contain("ScalarLogin"));
    }

    [Test]
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
            Payee: "Test Payee 1",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var transaction2 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Amount: 250.75m,
            Payee: "Test Payee 2",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
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

        // And: Response should contain both transactions in paginated result
        var result = await getTransactionsResponse.Content
            .ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Count, Is.EqualTo(2));

        // And: Transactions should match the created data
        var tx1 = result.Items.FirstOrDefault(t => t.Payee == "Test Payee 1");
        var tx2 = result.Items.FirstOrDefault(t => t.Payee == "Test Payee 2");
        Assert.That(tx1, Is.Not.Null, "Transaction 1 should be in results");
        Assert.That(tx1!.Amount, Is.EqualTo(100.50m));
        Assert.That(tx1.Key, Is.EqualTo(createdTx1.Key));
        Assert.That(tx2, Is.Not.Null, "Transaction 2 should be in results");
        Assert.That(tx2!.Amount, Is.EqualTo(250.75m));
        Assert.That(tx2.Key, Is.EqualTo(createdTx2.Key));
    }

    // Note: Using NuxtIdentity.Core.Models for LoginResponse, RefreshResponse, TokenPair, UserInfo, etc.
}
