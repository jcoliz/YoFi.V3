# API Client Quick Reference

Auto-generated from NSwag. See [`apiclient.ts`](apiclient.ts:1) for full implementation.

## Client Classes

### AuthClient

Authentication and session management.

- `login(request: LoginRequest): Promise<LoginResponse>` - Authenticates a user with username and password
- `signUp(request: SignUpRequest): Promise<LoginResponse>` - Registers a new user
- `getSession(): Promise<SessionResponse>` - Retrieves the current user's session information
- `refreshTokens(request: RefreshRequest): Promise<RefreshResponse>` - Handles token refresh logic
- `logout(request: RefreshRequest): Promise<void>` - Handles logout logic

### TransactionsClient

Transaction CRUD operations within tenant workspaces.

- `getTransactions(fromDate?, toDate?, tenantKey): Promise<TransactionResultDto[]>` - Retrieves all transactions for the tenant, optionally filtered by date range
- `createTransaction(tenantKey, transaction: TransactionEditDto): Promise<TransactionResultDto>` - Creates a new transaction in the tenant workspace
- `getTransactionById(key, tenantKey): Promise<TransactionResultDto>` - Retrieves a specific transaction by its unique key
- `updateTransaction(key, tenantKey, transaction: TransactionEditDto): Promise<void>` - Updates an existing transaction in the tenant workspace
- `deleteTransaction(key, tenantKey): Promise<void>` - Deletes a transaction from the tenant workspace

### TenantClient

Tenant/workspace management for the current authenticated user.

- `getTenants(): Promise<TenantRoleResultDto[]>` - Get all tenants for current user
- `createTenant(tenantDto: TenantEditDto): Promise<TenantResultDto>` - Create a new tenant, with current user as owner
- `getTenant(key): Promise<TenantRoleResultDto>` - Get a specific tenant for current user by tenant key
- `updateTenant(tenantKey, tenantDto: TenantEditDto): Promise<TenantResultDto>` - Update an existing tenant (requires Owner role)
- `deleteTenant(tenantKey): Promise<void>` - Delete a tenant (requires Owner role)

### TestControlClient

Test data management for functional testing (only available in test environments).

**User Management:**
- `createUser(): Promise<TestUserCredentials>` - Create a test user with auto-generated username
- `deleteUsers(): Promise<void>` - Deletes all test users from the system
- `createBulkUsers(usernames: string[]): Promise<TestUserCredentials[]>` - Create multiple test users in bulk with credentials
- `approveUser(username): Promise<void>` - Approve a test user

**Workspace Management:**
- `createWorkspaceForUser(username, request: WorkspaceCreateRequest): Promise<TenantResultDto>` - Create a workspace for a test user with specified role
- `assignUserToWorkspace(username, workspaceKey, assignment: UserRoleAssignment): Promise<void>` - Assign a user to an existing workspace with a specific role
- `bulkWorkspaceSetup(username, workspaces: WorkspaceSetupRequest[]): Promise<WorkspaceSetupResult[]>` - Create multiple workspaces for a user in a single request

**Transaction Seeding:**
- `seedTransactions(username, tenantKey, request: TransactionSeedRequest): Promise<TransactionResultDto[]>` - Seed test transactions in a workspace for a user

**Data Cleanup:**
- `deleteAllTestData(): Promise<void>` - Delete all test data including test users and test workspaces

**Error Testing:**
- `listErrors(): Promise<ErrorCodeInfo[]>` - List available error codes that can be generated for testing
- `returnError(code): Promise<void>` - Generate various error codes for testing purposes

### WeatherClient

Demo weather forecast endpoint.

- `getWeatherForecasts(): Promise<WeatherForecast[]>` - Retrieves weather forecasts for the next 5 days

### VersionClient

Application version information.

- `getVersion(): Promise<string>` - Retrieves the current application version

## Key DTOs

### Authentication

- **LoginRequest**: `{ username: string, password: string }`
- **SignUpRequest**: `{ username: string, email: string, password: string }`
- **LoginResponse**: `{ token: TokenPair, user: UserInfo }`
- **SessionResponse**: `{ user?: UserInfo }`
- **RefreshRequest**: `{ refreshToken: string }`
- **RefreshResponse**: `{ token: TokenPair }`
- **TokenPair**: `{ accessToken: string, refreshToken: string }`
- **UserInfo**: `{ id: string, name: string, email: string, roles: string[], claims: ClaimInfo[] }`

### Transactions

- **TransactionEditDto**: `{ date: Date, amount: number, payee: string }`
- **TransactionResultDto**: `{ key: string, date: Date, amount: number, payee: string }`

### Tenants/Workspaces

- **TenantEditDto**: `{ name: string, description: string }`
- **TenantResultDto**: `{ key: string, name: string, description: string, createdAt: Date }`
- **TenantRoleResultDto**: Extends TenantResultDto + `{ role: TenantRole }`
- **TenantRole** enum:
  - `Viewer = 1` - Read-only access
  - `Editor = 2` - Read and write access
  - `Owner = 3` - Full access including delete and user management

### Test Control

- **TestUserCredentials**: `{ id: string, username: string, email: string, password: string }`
- **WorkspaceCreateRequest**: `{ name: string, description: string, role: string }`
- **WorkspaceSetupRequest**: `{ name: string, description: string, role: string }`
- **WorkspaceSetupResult**: `{ key: string, name: string, role: string }`
- **UserRoleAssignment**: `{ role: string }`
- **TransactionSeedRequest**: `{ count: number, payeePrefix?: string }`
- **ErrorCodeInfo**: `{ code: string, description: string }`

### Other

- **WeatherForecast**: `{ key: string, date: Date, temperatureC: number, temperatureF: number, summary?: string }`
- **ProblemDetails**: `{ type?: string, title?: string, status?: number, detail?: string, instance?: string }`

## Usage Examples

### Authentication Flow

```typescript
import { AuthClient, LoginRequest } from './apiclient'

const authClient = new AuthClient()

// Login
const loginRequest = new LoginRequest({
  username: 'user@example.com',
  password: 'password123'
})
const response = await authClient.login(loginRequest)
// Store response.token.accessToken and response.token.refreshToken

// Get session
const session = await authClient.getSession()

// Refresh tokens
const refreshRequest = new RefreshRequest({
  refreshToken: storedRefreshToken
})
const refreshed = await authClient.refreshTokens(refreshRequest)

// Logout
await authClient.logout(refreshRequest)
```

### Transaction Management

```typescript
import { TransactionsClient, TransactionEditDto } from './apiclient'

const client = new TransactionsClient()
const tenantKey = 'your-tenant-key'

// Get all transactions
const transactions = await client.getTransactions(null, null, tenantKey)

// Get transactions with date range
const fromDate = new Date('2024-01-01')
const toDate = new Date('2024-12-31')
const filtered = await client.getTransactions(fromDate, toDate, tenantKey)

// Create transaction
const newTx = new TransactionEditDto({
  date: new Date(),
  amount: 100.50,
  payee: 'Coffee Shop'
})
const created = await client.createTransaction(tenantKey, newTx)

// Update transaction
await client.updateTransaction(created.key!, tenantKey, {
  ...newTx,
  amount: 150.00
})

// Delete transaction
await client.deleteTransaction(created.key!, tenantKey)
```

### Tenant Management

```typescript
import { TenantClient, TenantEditDto } from './apiclient'

const client = new TenantClient()

// Get all tenants for current user
const tenants = await client.getTenants()

// Create new tenant
const newTenant = new TenantEditDto({
  name: 'My Workspace',
  description: 'Personal finances'
})
const created = await client.createTenant(newTenant)

// Get specific tenant
const tenant = await client.getTenant(created.key!)

// Update tenant (requires Owner role)
await client.updateTenant(created.key!, {
  name: 'Updated Workspace',
  description: 'Updated description'
})

// Delete tenant (requires Owner role)
await client.deleteTenant(created.key!)
```

### Test Data Setup (Functional Tests)

```typescript
import { TestControlClient } from './apiclient'

const client = new TestControlClient()

// Create test user
const user = await client.createUser()
// Returns: { id, username, email, password }

// Create workspace for user
const workspace = await client.createWorkspaceForUser(user.username!, {
  name: '__TEST__MyWorkspace',
  description: 'Test workspace',
  role: 'Owner'
})

// Seed transactions
const transactions = await client.seedTransactions(
  user.username!,
  workspace.key!,
  { count: 10, payeePrefix: 'Test Payee' }
)

// Cleanup all test data
await client.deleteAllTestData()
```

## HTTP Status Codes

All clients handle these common status codes:

- **200 OK** - Successful GET/PUT request
- **201 Created** - Successful POST request that created a resource
- **204 No Content** - Successful DELETE or PUT with no response body
- **400 Bad Request** - Invalid request data (returns ProblemDetails)
- **401 Unauthorized** - Not authenticated (returns ProblemDetails)
- **403 Forbidden** - Authenticated but lacks permission (returns ProblemDetails)
- **404 Not Found** - Resource not found (returns ProblemDetails)
- **409 Conflict** - Conflict with existing data (returns ProblemDetails)
- **500 Internal Server Error** - Server error (returns ProblemDetails)

## Error Handling

All API calls return Promises that may throw `ApiException`:

```typescript
try {
  const result = await client.someMethod()
} catch (error) {
  if (ApiException.isApiException(error)) {
    console.error('API Error:', {
      status: error.status,
      message: error.message,
      detail: error.result?.detail
    })
  }
}
```

## Notes

- All client constructors accept optional `baseUrl` and `http` parameters
- Date parameters are automatically converted to ISO 8601 format
- The `tenantKey` parameter is required for tenant-scoped operations
- Test Control endpoints are only available in test/development environments
- All clients use the browser's native `fetch` API by default
