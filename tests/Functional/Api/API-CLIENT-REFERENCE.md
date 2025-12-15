# API Client Reference

**Auto-generated from:** [`ApiClient.cs`](ApiClient.cs)
**Purpose:** Quick reference for API client interfaces and data models

---

## Client Interfaces

### IAuthClient

Authentication and user session management.

```csharp
Task<LoginResponse> LoginAsync(LoginRequest request)
Task<LoginResponse> SignUpAsync(SignUpRequest request)
Task<SessionResponse> GetSessionAsync()
Task<RefreshResponse> RefreshTokensAsync(RefreshRequest request)
Task LogoutAsync(RefreshRequest request)
```

**Endpoints:**
- `POST api/auth/login`
- `POST api/auth/signup`
- `GET api/auth/user`
- `POST api/auth/refresh`
- `POST api/auth/logout`

---

### ITestControlClient

Test control endpoints for setting up test data and users.

```csharp
Task<TestUserCredentials> CreateUserAsync()
Task DeleteUsersAsync()
Task<ICollection<TestUserCredentials>> CreateBulkUsersAsync(IEnumerable<string> usernames)
Task ApproveUserAsync(string username)
Task<TenantResultDto> CreateWorkspaceForUserAsync(string username, WorkspaceCreateRequest request)
Task AssignUserToWorkspaceAsync(string username, Guid workspaceKey, UserRoleAssignment assignment)
Task<ICollection<TransactionResultDto>> SeedTransactionsAsync(string username, Guid tenantKey, TransactionSeedRequest request)
Task DeleteAllTestDataAsync()
Task<ICollection<WorkspaceSetupResult>> BulkWorkspaceSetupAsync(string username, IEnumerable<WorkspaceSetupRequest> workspaces)
Task<ICollection<ErrorCodeInfo>> ListErrorsAsync()
Task ReturnErrorAsync(string code)
```

**Endpoints:**
- `POST TestControl/users`
- `DELETE TestControl/users`
- `POST TestControl/users/bulk`
- `PUT TestControl/users/{username}/approve`
- `POST TestControl/users/{username}/workspaces`
- `POST TestControl/users/{username}/workspaces/{workspaceKey}/assign`
- `POST TestControl/users/{username}/workspaces/{tenantKey}/transactions/seed`
- `DELETE TestControl/data`
- `POST TestControl/users/{username}/workspaces/bulk`
- `GET TestControl/errors`
- `GET TestControl/errors/{code}`

---

### ITransactionsClient

Transaction CRUD operations.

```csharp
Task<ICollection<TransactionResultDto>> GetTransactionsAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate, string tenantKey)
Task<TransactionResultDto> CreateTransactionAsync(Guid tenantKey, TransactionEditDto transaction)
Task<TransactionResultDto> GetTransactionByIdAsync(Guid key, string tenantKey)
Task UpdateTransactionAsync(Guid key, string tenantKey, TransactionEditDto transaction)
Task DeleteTransactionAsync(Guid key, string tenantKey)
```

**Endpoints:**
- `GET api/tenant/{tenantKey}/Transactions?fromDate={date}&toDate={date}`
- `POST api/tenant/{tenantKey}/Transactions`
- `GET api/tenant/{tenantKey}/Transactions/{key}`
- `PUT api/tenant/{tenantKey}/Transactions/{key}`
- `DELETE api/tenant/{tenantKey}/Transactions/{key}`

---

### IVersionClient

API version information.

```csharp
Task<string> GetVersionAsync()
```

**Endpoints:**
- `GET Version`

---

### IWeatherClient

Weather forecast data (example endpoint).

```csharp
Task<ICollection<WeatherForecast>> GetWeatherForecastsAsync()
```

**Endpoints:**
- `GET api/Weather`

---

### ITenantClient

Workspace/tenant management.

```csharp
Task<ICollection<TenantRoleResultDto>> GetTenantsAsync()
Task<TenantResultDto> CreateTenantAsync(TenantEditDto tenantDto)
Task<TenantRoleResultDto> GetTenantAsync(Guid key)
Task<TenantResultDto> UpdateTenantAsync(Guid tenantKey, TenantEditDto tenantDto)
Task DeleteTenantAsync(Guid tenantKey)
```

**Endpoints:**
- `GET api/Tenant`
- `POST api/Tenant`
- `GET api/Tenant/{key}`
- `PUT api/Tenant/{tenantKey}`
- `DELETE api/Tenant/{tenantKey}`

---

## Data Models

### Authentication Models

#### LoginRequest
```csharp
class LoginRequest
{
    string Username { get; set; }
    string Password { get; set; }
}
```

#### LoginResponse
```csharp
class LoginResponse
{
    TokenPair Token { get; set; }
    UserInfo User { get; set; }
}
```

#### SignUpRequest
```csharp
class SignUpRequest
{
    string Username { get; set; }
    string Email { get; set; }
    string Password { get; set; }
}
```

#### SessionResponse
```csharp
class SessionResponse
{
    UserInfo User { get; set; }
}
```

#### RefreshRequest
```csharp
class RefreshRequest
{
    string RefreshToken { get; set; }
}
```

#### RefreshResponse
```csharp
class RefreshResponse
{
    TokenPair Token { get; set; }
}
```

#### TokenPair
```csharp
class TokenPair
{
    string AccessToken { get; set; }
    string RefreshToken { get; set; }
}
```

#### UserInfo
```csharp
class UserInfo
{
    string Id { get; set; }
    string Name { get; set; }
    string Email { get; set; }
    ICollection<string> Roles { get; set; }
    ICollection<ClaimInfo> Claims { get; set; }
}
```

#### ClaimInfo
```csharp
class ClaimInfo
{
    string Type { get; set; }
    string Value { get; set; }
}
```

---

### Test Control Models

#### TestUserCredentials
```csharp
class TestUserCredentials
{
    Guid Id { get; set; }
    string Username { get; set; }
    string Email { get; set; }
    string Password { get; set; }
}
```

#### WorkspaceCreateRequest
```csharp
class WorkspaceCreateRequest
{
    string Name { get; set; }
    string Description { get; set; }
    string Role { get; set; }
}
```

#### WorkspaceSetupRequest
```csharp
class WorkspaceSetupRequest
{
    string Name { get; set; }
    string Description { get; set; }
    string Role { get; set; }
}
```

#### WorkspaceSetupResult
```csharp
class WorkspaceSetupResult
{
    Guid Key { get; set; }
    string Name { get; set; }
    string Role { get; set; }
}
```

#### UserRoleAssignment
```csharp
class UserRoleAssignment
{
    string Role { get; set; }
}
```

#### TransactionSeedRequest
```csharp
class TransactionSeedRequest
{
    int Count { get; set; }
    string PayeePrefix { get; set; }
}
```

#### ErrorCodeInfo
```csharp
class ErrorCodeInfo
{
    string Code { get; set; }
    string Description { get; set; }
}
```

---

### Transaction Models

#### TransactionEditDto
```csharp
class TransactionEditDto
{
    [JsonConverter(typeof(DateFormatConverter))]
    DateTimeOffset Date { get; set; }
    decimal Amount { get; set; }
    string Payee { get; set; }
}
```

#### TransactionResultDto
```csharp
class TransactionResultDto
{
    Guid Key { get; set; }
    [JsonConverter(typeof(DateFormatConverter))]
    DateTimeOffset Date { get; set; }
    decimal Amount { get; set; }
    string Payee { get; set; }
}
```

---

### Tenant/Workspace Models

#### TenantEditDto
```csharp
class TenantEditDto
{
    string Name { get; set; }
    string Description { get; set; }
}
```

#### TenantResultDto
```csharp
class TenantResultDto
{
    Guid Key { get; set; }
    string Name { get; set; }
    string Description { get; set; }
    DateTimeOffset CreatedAt { get; set; }
}
```

#### TenantRoleResultDto
```csharp
class TenantRoleResultDto
{
    Guid Key { get; set; }
    string Name { get; set; }
    string Description { get; set; }
    TenantRole Role { get; set; }
    DateTimeOffset CreatedAt { get; set; }
}
```

#### TenantRole (enum)
```csharp
enum TenantRole
{
    Viewer = 1,
    Editor = 2,
    Owner = 3
}
```

---

### Weather Models (Example)

#### WeatherForecast
```csharp
class WeatherForecast : BaseModel
{
    [JsonConverter(typeof(DateFormatConverter))]
    DateTimeOffset Date { get; set; }
    int TemperatureC { get; set; }
    string Summary { get; set; }
    int TemperatureF { get; set; }
}
```

#### BaseModel
```csharp
class BaseModel
{
    long Id { get; set; }
    Guid Key { get; set; }
}
```

---

### Error Models

#### ProblemDetails
```csharp
class ProblemDetails
{
    string Type { get; set; }
    string Title { get; set; }
    int? Status { get; set; }
    string Detail { get; set; }
    string Instance { get; set; }
    IDictionary<string, object> AdditionalProperties { get; set; }
}
```

#### ApiException
```csharp
class ApiException : Exception
{
    int StatusCode { get; }
    string Response { get; }
    IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; }
}
```

#### ApiException<TResult>
```csharp
class ApiException<TResult> : ApiException
{
    TResult Result { get; }
}
```

---

## Usage Examples

### Authentication
```csharp
var authClient = new AuthClient(baseUrl, httpClient);

// Login
var loginResponse = await authClient.LoginAsync(new LoginRequest
{
    Username = "user@example.com",
    Password = "password123"
});

// Access token
var accessToken = loginResponse.Token.AccessToken;

// Get session
var session = await authClient.GetSessionAsync();
```

### Test Control
```csharp
var testClient = new TestControlClient(baseUrl, httpClient);

// Create test user
var user = await testClient.CreateUserAsync();

// Create workspace for user
var workspace = await testClient.CreateWorkspaceForUserAsync(
    user.Username,
    new WorkspaceCreateRequest
    {
        Name = "Test Workspace",
        Description = "Description",
        Role = "Owner"
    }
);

// Seed transactions
var transactions = await testClient.SeedTransactionsAsync(
    user.Username,
    workspace.Key,
    new TransactionSeedRequest
    {
        Count = 10,
        PayeePrefix = "TestPayee"
    }
);
```

### Transactions
```csharp
var transactionsClient = new TransactionsClient(baseUrl, httpClient);

// Get transactions
var transactions = await transactionsClient.GetTransactionsAsync(
    fromDate: DateTimeOffset.Now.AddMonths(-1),
    toDate: DateTimeOffset.Now,
    tenantKey: workspaceKey.ToString()
);

// Create transaction
var newTransaction = await transactionsClient.CreateTransactionAsync(
    tenantKey: workspaceKey,
    transaction: new TransactionEditDto
    {
        Date = DateTimeOffset.Now,
        Amount = 100.00m,
        Payee = "Test Payee"
    }
);
```

### Tenants/Workspaces
```csharp
var tenantClient = new TenantClient(baseUrl, httpClient);

// Get all workspaces
var workspaces = await tenantClient.GetTenantsAsync();

// Create workspace
var workspace = await tenantClient.CreateTenantAsync(new TenantEditDto
{
    Name = "My Workspace",
    Description = "Workspace description"
});

// Update workspace
var updated = await tenantClient.UpdateTenantAsync(
    workspace.Key,
    new TenantEditDto
    {
        Name = "Updated Name",
        Description = "Updated description"
    }
);

// Delete workspace
await tenantClient.DeleteTenantAsync(workspace.Key);
```

---

## Common HTTP Status Codes

- **200 OK** - Successful GET, PUT requests
- **201 Created** - Successful POST requests
- **204 No Content** - Successful DELETE requests
- **400 Bad Request** - Validation errors
- **401 Unauthorized** - Authentication required
- **403 Forbidden** - Insufficient permissions
- **404 Not Found** - Resource not found
- **409 Conflict** - Resource conflict (e.g., duplicate)
- **500 Internal Server Error** - Server error

All error responses return `ProblemDetails` with error information.

---

## Notes

- All clients are generated from OpenAPI/Swagger specification using NSwag
- All async methods have overloads with `CancellationToken` parameter
- Date properties use `DateFormatConverter` for "yyyy-MM-dd" format
- `Guid` parameters are typically workspace/tenant keys or entity keys
- Test Control endpoints are only available in test environments
