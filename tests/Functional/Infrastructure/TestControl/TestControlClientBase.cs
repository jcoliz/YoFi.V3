using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace YoFi.V3.Tests.Functional.Infrastructure.TestControl;

/// <summary>
/// Provides authentication and non-OpenAPI operations for the generated test-control client.
/// </summary>
public abstract class TestControlClientBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _authenticationLock = new(1, 1);
    private HttpClient? _httpClient;
    private string? _userName;
    private string? _userPassword;
    private string? _accessToken;
    private Guid? _tenantId;

    /// <summary>
    /// Gets the tenant identified during authentication.
    /// </summary>
    public Guid TenantId => _tenantId
        ?? throw new InvalidOperationException("The test-control client has not been initialized");

    /// <summary>
    /// Initializes authentication credentials and discovers the user's default tenant.
    /// </summary>
    public async Task InitializeAsync(HttpClient httpClient, string userName, string userPassword)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(userPassword);

        _httpClient = httpClient;
        _userName = userName;
        _userPassword = userPassword;
        await EnsureAuthenticatedAsync(CancellationToken.None);
    }

    /// <summary>
    /// Authenticates and adds the bearer token before the generated client builds a request URL.
    /// </summary>
    protected virtual async Task PrepareRequestAsync(
        HttpClient client,
        HttpRequestMessage request,
        StringBuilder urlBuilder,
        CancellationToken cancellationToken)
    {
        await PrepareAuthorizedRequestAsync(client, request, cancellationToken);
    }

    /// <summary>
    /// Authenticates and adds the bearer token before the generated client sends a request.
    /// </summary>
    protected virtual async Task PrepareRequestAsync(
        HttpClient client,
        HttpRequestMessage request,
        string url,
        CancellationToken cancellationToken)
    {
        await PrepareAuthorizedRequestAsync(client, request, cancellationToken);
    }

    /// <summary>
    /// Provides the generated client's asynchronous response-processing hook.
    /// </summary>
    protected virtual Task ProcessResponseAsync(
        HttpClient client,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private HttpClient HttpClient => _httpClient
        ?? throw new InvalidOperationException("The test-control client has not been initialized");

    private async Task PrepareAuthorizedRequestAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && _tenantId is not null)
            return;

        await _authenticationLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && _tenantId is not null)
                return;

            var loginPayload = new { username = _userName, password = _userPassword };
            using var loginResponse = await HttpClient.PostAsJsonAsync(
                "/api/auth/login",
                loginPayload,
                JsonOptions,
                cancellationToken);
            loginResponse.EnsureSuccessStatusCode();

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>(
                JsonOptions,
                cancellationToken)
                ?? throw new InvalidOperationException("Login returned null");

            var accessToken = loginResult.Token?.AccessToken
                ?? throw new InvalidOperationException("Login response missing access token");

            TestContext.Out.WriteLine("[TestControlClient] Login: OK");

            var entitlementClaims = loginResult.User?.Claims?
                .Where(claim => claim.Type == "entitlement" && claim.Value.Contains(":Default"))
                .ToList();

            if (entitlementClaims is null)
                throw new InvalidOperationException("No entitlement claims found for test user");

            if (entitlementClaims.Count == 0)
                throw new InvalidOperationException("No default entitlement claims found for test user");

            if (entitlementClaims.Count > 1)
                throw new InvalidOperationException("Multiple default entitlement claims found for test user");

            var parts = entitlementClaims[0].Value.Split(':');
            if (parts.Length == 0 || !Guid.TryParse(parts[0], out var tenantId))
                throw new InvalidOperationException(
                    $"Cannot parse tenant GUID from entitlement claim: {entitlementClaims[0].Value}");

            _accessToken = accessToken;
            _tenantId = tenantId;
            TestContext.Out.WriteLine($"[TestControlClient] Tenant: {_tenantId}");
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    private sealed record LoginResponseDto
    {
        public TokenPairDto? Token { get; init; }
        public UserInfoDto? User { get; init; }
    }

    private sealed record TokenPairDto
    {
        public string? AccessToken { get; init; }
    }

    private sealed record UserInfoDto
    {
        public ClaimInfoDto[]? Claims { get; init; }
    }

    private sealed record ClaimInfoDto
    {
        public string Type { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
    }
}
