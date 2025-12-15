<#
.SYNOPSIS
    Generates markdown documentation from ApiClient.cs
.DESCRIPTION
    Extracts API client interfaces and data models from the auto-generated NSwag ApiClient.cs
    and creates a readable markdown reference document.
.PARAMETER SourceFile
    Path to the ApiClient.cs file (default: Api/ApiClient.cs)
.PARAMETER OutputFile
    Path to the output markdown file (default: Api/API-CLIENT-REFERENCE.md)
#>
param(
    [string]$SourceFile = "Api/ApiClient.cs",
    [string]$OutputFile = "Api/API-CLIENT-REFERENCE.md"
)

$ErrorActionPreference = "Stop"

Write-Host "Generating API documentation..." -ForegroundColor Cyan
Write-Host "Source: $SourceFile" -ForegroundColor Gray
Write-Host "Output: $OutputFile" -ForegroundColor Gray

# Read the source file
if (-not (Test-Path $SourceFile)) {
    Write-Error "Source file not found: $SourceFile"
    exit 1
}

$content = Get-Content $SourceFile -Raw

# Extract interfaces and their methods
$interfaces = @{}
$currentInterface = $null

$content -split "`n" | ForEach-Object {
    $line = $_.Trim()

    # Match interface declaration
    if ($line -match 'public partial interface (I\w+Client)') {
        $currentInterface = $Matches[1]
        $interfaces[$currentInterface] = @{
            Methods = @()
            Endpoints = @()
        }
    }

    # Match method declarations (non-cancellation token versions)
    if ($currentInterface -and $line -match 'Task(<[^>]+>)?\s+(\w+)\(([^)]*)\)' -and $line -notmatch 'CancellationToken') {
        $method = $line -replace '.*Task(<[^>]+>)?\s+', '' -replace '\s*;.*', ''
        $interfaces[$currentInterface].Methods += $method
    }

    # Match operation path comments
    if ($currentInterface -and $line -match '// Operation Path: "([^"]+)"') {
        $endpoint = $Matches[1]
        if ($interfaces[$currentInterface].Endpoints -notcontains $endpoint) {
            $interfaces[$currentInterface].Endpoints += $endpoint
        }
    }
}

# Extract data models
$models = @{}
$currentModel = $null
$inModel = $false

$content -split "`n" | ForEach-Object {
    $line = $_.Trim()

    # Match class/enum declarations
    if ($line -match 'public (partial )?(class|enum) (\w+)') {
        $currentModel = $Matches[3]
        $modelType = $Matches[2]
        $baseClass = if ($line -match ':\s*(\w+)') { $Matches[1] } else { $null }

        $models[$currentModel] = @{
            Type = $modelType
            BaseClass = $baseClass
            Properties = @()
        }
        $inModel = $true
    }

    # Match property declarations
    if ($inModel -and $currentModel -and $line -match '\[System\.Text\.Json\.Serialization\.JsonPropertyName\("([^"]+)"\)\]') {
        $jsonName = $Matches[1]
        # Next line should have the property
    }

    if ($inModel -and $currentModel -and $line -match 'public\s+([^{]+?)\s+(\w+)\s*\{\s*get;') {
        $propType = $Matches[1].Trim()
        $propName = $Matches[2]
        $models[$currentModel].Properties += "$propType $propName"
    }

    # Match enum values
    if ($inModel -and $currentModel -and $models[$currentModel].Type -eq "enum" -and $line -match '^(\w+)\s*=\s*(\d+),?') {
        $models[$currentModel].Properties += "$($Matches[1]) = $($Matches[2])"
    }

    # End of class/enum
    if ($line -eq '}' -and $inModel) {
        $inModel = $false
        $currentModel = $null
    }
}

# Generate markdown
$markdown = @"
# API Client Reference

**Auto-generated from:** [``ApiClient.cs``](ApiClient.cs)
**Purpose:** Quick reference for API client interfaces and data models

---

## Client Interfaces

"@

# Add interfaces
foreach ($interface in $interfaces.Keys | Sort-Object) {
    $info = $interfaces[$interface]

    # Determine interface description
    $description = switch -Regex ($interface) {
        'IAuthClient' { 'Authentication and user session management.' }
        'ITestControlClient' { 'Test control endpoints for setting up test data and users.' }
        'ITransactionsClient' { 'Transaction CRUD operations.' }
        'IVersionClient' { 'API version information.' }
        'IWeatherClient' { 'Weather forecast data (example endpoint).' }
        'ITenantClient' { 'Workspace/tenant management.' }
        default { "Client for $interface operations." }
    }

    $markdown += "`n### $interface`n`n$description`n`n"

    if ($info.Methods.Count -gt 0) {
        $markdown += "````csharp`n"
        foreach ($method in $info.Methods) {
            $markdown += "$method`n"
        }
        $markdown += "````n`n"
    }

    if ($info.Endpoints.Count -gt 0) {
        $markdown += "**Endpoints:**`n"
        foreach ($endpoint in $info.Endpoints) {
            # Format endpoint with method
            $method = if ($endpoint -match '^(GET|POST|PUT|DELETE)') { $Matches[1] } else { 'GET' }
            $markdown += "- ``$method $endpoint```n"
        }
        $markdown += "`n---`n"
    }
}

$markdown += "`n## Data Models`n`n"

# Group models by category
$authModels = @('LoginRequest', 'LoginResponse', 'SignUpRequest', 'SessionResponse', 'RefreshRequest', 'RefreshResponse', 'TokenPair', 'UserInfo', 'ClaimInfo')
$testModels = @('TestUserCredentials', 'WorkspaceCreateRequest', 'WorkspaceSetupRequest', 'WorkspaceSetupResult', 'UserRoleAssignment', 'TransactionSeedRequest', 'ErrorCodeInfo')
$transactionModels = @('TransactionEditDto', 'TransactionResultDto')
$tenantModels = @('TenantEditDto', 'TenantResultDto', 'TenantRoleResultDto', 'TenantRole')
$weatherModels = @('WeatherForecast', 'BaseModel')
$errorModels = @('ProblemDetails', 'ApiException')

$categories = @{
    'Authentication Models' = $authModels
    'Test Control Models' = $testModels
    'Transaction Models' = $transactionModels
    'Tenant/Workspace Models' = $tenantModels
    'Weather Models (Example)' = $weatherModels
    'Error Models' = $errorModels
}

foreach ($category in $categories.Keys) {
    $markdown += "### $category`n`n"

    foreach ($modelName in $categories[$category]) {
        if ($models.ContainsKey($modelName)) {
            $model = $models[$modelName]

            $markdown += "#### $modelName"
            if ($model.Type -eq 'enum') {
                $markdown += " (enum)"
            }
            $markdown += "`n````csharp`n"
            $markdown += "$($model.Type) $modelName"
            if ($model.BaseClass -and $model.BaseClass -ne 'Exception' -and $model.BaseClass -ne 'System.Exception') {
                $markdown += " : $($model.BaseClass)"
            }
            $markdown += "`n{`n"

            foreach ($prop in $model.Properties) {
                $markdown += "    $prop { get; set; }`n"
            }

            $markdown += "}`n````n`n"
        }
    }

    $markdown += "---`n`n"
}

# Add usage examples section
$markdown += @"
## Usage Examples

### Authentication
````csharp
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
````

### Test Control
````csharp
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
````

### Transactions
````csharp
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
````

### Tenants/Workspaces
````csharp
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
````

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

All error responses return ``ProblemDetails`` with error information.

---

## Notes

- All clients are generated from OpenAPI/Swagger specification using NSwag
- All async methods have overloads with ``CancellationToken`` parameter
- Date properties use ``DateFormatConverter`` for "yyyy-MM-dd" format
- ``Guid`` parameters are typically workspace/tenant keys or entity keys
- Test Control endpoints are only available in test environments
"@

# Write output
$markdown | Out-File -FilePath $OutputFile -Encoding UTF8 -NoNewline

Write-Host "`nDocumentation generated successfully!" -ForegroundColor Green
Write-Host "Output file: $OutputFile" -ForegroundColor Gray
