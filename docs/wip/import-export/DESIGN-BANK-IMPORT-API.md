---
status: Approved
layer: API
parent: DESIGN-BANK-IMPORT.md
created: 2025-12-28
related_docs:
  - DESIGN-BANK-IMPORT.md
  - DESIGN-BANK-IMPORT-APPLICATION.md
  - DESIGN-BANK-IMPORT-DATABASE.md
  - PRD-BANK-IMPORT.md
  - VISUAL-DESIGN-BANK-IMPORT.md
  - MOCKUP-BANK-IMPORT.md
open_questions:
  - nswag_generic_dto_handling
---

# API Layer Design: Bank Import Feature

## Open Questions

### NSwag Generic DTO Handling

**Question:** Does NSwag properly generate TypeScript types for `PaginatedResultDto<ImportReviewTransactionDto>`?

**Context:** The `GET /api/import/review` endpoint returns `PaginatedResultDto<ImportReviewTransactionDto>`. NSwag needs to:
1. Generate proper TypeScript interface for the generic `PaginatedResultDto<T>` type
2. Correctly instantiate it with `ImportReviewTransactionDto` as the type parameter
3. Ensure the frontend can access strongly-typed `items` property

**Verification needed:**
- Run API client generation after implementing the endpoint
- Check generated TypeScript in [`apiclient.ts`](src/FrontEnd.Nuxt/app/utils/apiclient.ts)
- Verify TypeScript compilation succeeds
- Test that IntelliSense works correctly for `result.items[0].` (should show ImportReviewTransactionDto properties)

**If NSwag doesn't handle it correctly:**
- **Option 1:** Create a non-generic `ImportReviewPaginatedResultDto` class that inherits or wraps the generic
- **Option 2:** Configure NSwag to handle generic types properly (check [`nswag.json`](src/WireApiHost/nswag.json) settings)
- **Option 3:** Use concrete type in `[ProducesResponseType]` attribute but keep generic implementation

**Resolution:** TBD during implementation

## Overview

This document provides the complete API/Controller layer design for the Bank Import feature. The API layer exposes REST endpoints for uploading OFX files, reviewing pending imports, accepting transactions, and managing the import review workflow.

**Key components:**
- **[`ImportController`](src/Controllers/ImportController.cs)** - REST API endpoints for import workflow
- **Authorization** - Tenant-scoped access control via `[RequireTenantRole]`
- **File upload handling** - Multipart form data with validation
- **Logging** - Structured logging following [`docs/LOGGING-POLICY.md`](../../LOGGING-POLICY.md)
- **Error handling** - Standard ProblemDetails responses

**Layer responsibilities:**
- Accept and validate file uploads (extension, size, empty file checks)
- Delegate business logic to [`ImportReviewFeature`](src/Application/Import/Features/ImportReviewFeature.cs)
- Enforce authorization (Editor or Owner roles required)
- Log all operations with structured context
- Return standard HTTP responses with appropriate status codes

## ImportController Class

Location: `src/Controllers/ImportController.cs`

**Bank import workflow endpoints:**
1. Upload OFX/QFX file and parse transactions
2. Retrieve pending import review transactions
3. Accept selected transactions into main transaction table
4. Delete rejected or duplicate transactions from review
5. Clear all pending imports for the current session

**Security:**
- All operations scoped to authenticated user's current tenant via TenantContext middleware
- Users must have Editor or Owner roles to perform import operations
- File uploads validated for extension (.ofx, .qfx), size limits (configurable), and content validation through OFX parsing

```csharp
/// <summary>
/// Manages bank transaction import operations within a tenant workspace.
/// </summary>
/// <param name="importReviewFeature">Feature providing import review workflow operations.</param>
/// <param name="logger">Logger for diagnostic output.</param>
[Route("api/import")]
[ApiController]
[RequireTenantRole(TenantRole.Editor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class ImportController(
    ImportReviewFeature importReviewFeature,
    ILogger<ImportController> logger) : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".ofx", ".qfx"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    /// <summary>
    /// Uploads an OFX/QFX file, parses transactions, detects duplicates, and stores them for review.
    /// </summary>
    /// <param name="file">The OFX or QFX file to upload.</param>
    /// <returns>Import result containing statistics and all parsed transactions.</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> UploadFile(IFormFile file);

    /// <summary>
    /// Retrieves pending import review transactions for the current tenant with pagination support.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve (default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 50, max: 1000).</param>
    /// <returns>Paginated response containing transactions and pagination metadata.</returns>
    [HttpGet("review")]
    [ProducesResponseType(typeof(PaginatedResultDto<ImportReviewTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReview(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50);

    /// <summary>
    /// Completes the import review by accepting selected transactions and deleting all pending review transactions.
    /// </summary>
    /// <param name="keys">The collection of transaction keys to accept (import into main transaction table).</param>
    /// <returns>Result indicating the number of transactions accepted and rejected.</returns>
    [HttpPost("review/complete")]
    [ProducesResponseType(typeof(CompleteReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteReview([FromBody] IReadOnlyCollection<Guid> keys);

    /// <summary>
    /// Deletes all pending import review transactions for the current tenant.
    /// </summary>
    [HttpDelete("review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAllPendingReview();
}
```

### CompleteReview Endpoint Behavior

This endpoint performs two operations atomically:
1. Copies the specified transactions to the main Transaction table
2. Deletes ALL transactions from the ImportReviewTransaction table (selected and unselected)

This ensures the review workflow completes cleanly - selected transactions are imported, and the review table is cleared for the next import session.

## Endpoint Specifications

| Method | Path | Description | Request | Response | Status Codes |
|--------|------|-------------|---------|----------|--------------|
| POST | `/api/import/upload` | Upload OFX/QFX file for import | `multipart/form-data` with `file` field | [`ImportResultDto`](src/Application/Import/Dto/ImportResultDto.cs) | 200, 400, 401, 403 |
| GET | `/api/import/review` | Get pending import review transactions (paginated) | Query: `pageNumber` (default: 1), `pageSize` (default: 50, max: 1000) | [`PaginatedResultDto<ImportReviewTransactionDto>`](src/Application/Common/Dto/PaginatedResultDto.cs) | 200, 401, 403 |
| POST | `/api/import/review/complete` | Complete review: accept selected transactions and delete all | `IReadOnlyCollection<Guid>` (transaction keys to accept) | [`CompleteReviewResultDto`](src/Application/Import/Dto/CompleteReviewResultDto.cs) | 200, 400, 401, 403 |
| DELETE | `/api/import/review` | Delete all pending review transactions without accepting any | None | None | 204, 401, 403 |

### Endpoint Details

#### POST /api/import/upload

**Frontend Calling Pattern:**

This is a **SPA (Single Page Application)** - file uploads are handled via JavaScript, NOT traditional HTML form submissions:

```vue
<template>
  <form @submit.prevent="handleUpload">
    <input
      type="file"
      accept=".ofx,.qfx"
      @change="handleFileSelected"
      ref="fileInput"
    />
    <button type="submit" :disabled="!selectedFile">Upload</button>
  </form>
</template>

<script setup>
const selectedFile = ref<File | null>(null)

const handleFileSelected = (event: Event) => {
  const target = event.target as HTMLInputElement
  selectedFile.value = target.files?.[0] || null
}

const handleUpload = async () => {
  if (!selectedFile.value) return

  // Get authenticated fetch wrapper
  const authFetch = useAuthFetch()

  // Create API client with auth-aware fetch
  const importClient = new ImportClient(undefined, authFetch)

  // Call upload endpoint - Authorization header added automatically
  const result = await importClient.uploadFile(selectedFile.value)

  // Handle result (display transactions for review)
  console.log(`Imported ${result.importedCount} transactions`)
}
</script>
```

**Key points:**
- Form uses `@submit.prevent` to prevent default browser POST
- JavaScript handler calls NSwag-generated [`ImportClient`](src/FrontEnd.Nuxt/app/utils/apiclient.ts)
- Client uses [`useAuthFetch()`](src/FrontEnd.Nuxt/app/composables/useAuthFetch.ts) which automatically adds `Authorization: Bearer <token>` header
- No direct browser form submission - all uploads go through authenticated API client
- Controller's `[Authorize]` attribute works correctly because requests include auth token

**Request:**
- Content-Type: `multipart/form-data`
- Form field: `file` (IFormFile)
- File extensions: `.ofx`, `.qfx`
- Maximum size: 50 MB (configurable via `RequestSizeLimit`)

**Response (200 OK) - Summary statistics only:**
```json
{
  "importedCount": 150,
  "newCount": 120,
  "exactDuplicateCount": 20,
  "potentialDuplicateCount": 10
}
```

**Why no transaction list in response:**
- **Bandwidth efficiency**: Large imports (1,000-10,000 transactions) would waste bandwidth sending data that many users will reject
- **UX flow**: User is already on transaction review page (where upload happens). After upload, page refreshes display by calling `GET /api/import/review` to fetch updated reviewable transactions
- **Performance**: Upload completes faster without serializing large collections
- **Flexibility**: Review page can implement filtering, sorting, pagination independently

The upload response provides just enough information for the success message:
- "Import complete: 120 new transactions, 20 duplicates detected, 10 potential duplicates flagged"

Then the page automatically refreshes the transaction list to show the newly imported transactions.

**Validation errors (400):**
- File is null or empty
- Invalid file extension (not .ofx or .qfx)
- File size exceeds limit

#### GET /api/import/review

**Query Parameters:**
- `pageNumber` (optional, default: 1) - The page number to retrieve (1-based)
- `pageSize` (optional, default: 50, max: 1000) - Number of items per page

**Examples:**
- `GET /api/import/review` - Returns first 50 transactions
- `GET /api/import/review?pageNumber=2&pageSize=100` - Returns transactions 101-200
- `GET /api/import/review?pageSize=1000` - Returns first 1000 transactions (max page size)

**Validation:**
- `pageNumber < 1` → Defaults to 1
- `pageSize < 1` → Defaults to 50
- `pageSize > 1000` → Clamped to 1000 (prevents excessive data transfer)

**Response (200 OK) - [`PaginatedResultDto<ImportReviewTransactionDto>`](src/Application/Common/Dto/PaginatedResultDto.cs):**
```json
{
  "items": [
    {
      "key": "550e8400-e29b-41d4-a716-446655440000",
      "date": "2024-01-15",
      "payee": "Amazon",
      "amount": -50.00,
      "source": "Chase Checking - XXXX1234",
      "externalId": "FITID12345",
      "memo": "Online purchase",
      "duplicateStatus": "New",
      "duplicateOfKey": null,
      "importedAt": "2024-01-20T10:30:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 150,
  "totalPages": 3,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

**Pagination Metadata:**
- `items` - Collection of transactions for current page
- `pageNumber` - Current page number (1-based)
- `pageSize` - Number of items per page (as requested)
- `totalCount` - Total number of pending review transactions across all pages
- `totalPages` - Total number of pages available (calculated as `ceiling(totalCount / pageSize)`)
- `hasPreviousPage` - `true` if `pageNumber > 1`
- `hasNextPage` - `true` if `pageNumber < totalPages`

**Pagination Pattern:**
- Returns transactions ordered by date descending (newest first)
- Empty `items` array if page number exceeds available data
- Frontend can check `hasNextPage` to determine if more data exists
- Typical UX: "Load more" button, infinite scroll, or page number navigation
- Frontend can display "Showing 1-50 of 150" using metadata

#### POST /api/import/review/complete

**Purpose:** Completes the import review workflow by accepting selected transactions and clearing the review table.

**Request body** - Array of transaction keys to accept (import):
```json
[
  "550e8400-e29b-41d4-a716-446655440000",
  "660e8400-e29b-41d4-a716-446655440001"
]
```

**Behavior:**
1. Copies the specified transactions to the main Transaction table
2. Deletes ALL transactions from ImportReviewTransaction table (not just the selected ones)
3. Returns count of accepted transactions and rejected (not selected) transactions

**Example scenario:**
- Review table has 150 transactions total
- User selects 120 transactions to accept
- API copies 120 transactions to Transaction table
- API deletes all 150 transactions from review table
- Response: `{ "acceptedCount": 120, "rejectedCount": 30 }`

**Response (200 OK):**
```json
{
  "acceptedCount": 120,
  "rejectedCount": 30
}
```

**Why delete all transactions?**
- Matches UI behavior: "Import" button completes the review workflow
- Prevents orphaned unselected transactions from accumulating
- Clean slate for next import session
- User can explicitly reject transactions by unchecking them (they won't be imported but will be deleted)

**Note:** `rejectedCount` represents the number of transactions that were NOT selected (rejected). It equals the total transactions in review minus the accepted count.

**Validation errors (400):**
- Keys array is null or empty

#### DELETE /api/import/review

**Purpose:** Deletes all pending review transactions without accepting any. This is the "Delete All" functionality.

**Use case:** User wants to completely cancel/discard the current import without accepting any transactions.

**Response:** 204 No Content

## File Upload Security

### Extension Validation

**Allowed extensions:** `.ofx`, `.qfx`

**Implementation:**
```csharp
private static readonly string[] AllowedExtensions = [".ofx", ".qfx"];

var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!AllowedExtensions.Contains(extension))
{
    return Problem(
        title: "Invalid file type",
        detail: $"Only {string.Join(", ", AllowedExtensions)} files are allowed.",
        statusCode: StatusCodes.Status400BadRequest);
}
```

**Rationale:**
- Prevents upload of executable files, scripts, or other potentially dangerous content
- Case-insensitive comparison handles `.OFX`, `.Ofx`, etc.
- Clear error message guides users to correct file type

### File Size Limits

**Maximum size:** 50 MB (configurable)

**Implementation:**
```csharp
private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

[RequestSizeLimit(MaxFileSizeBytes)]
public async Task<IActionResult> UploadFile(IFormFile file)
{
    if (file.Length > MaxFileSizeBytes)
    {
        return Problem(
            title: "File too large",
            detail: $"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.",
            statusCode: StatusCodes.Status400BadRequest);
    }
}
```

**Rationale:**
- Prevents DoS attacks via extremely large file uploads
- 50 MB is sufficient for typical bank files (100-10,000 transactions)
- `[RequestSizeLimit]` provides framework-level protection
- Manual check provides clear error message to users

### Content Validation

**OFX Parser validation:**
- File content is validated by [`OfxParsingService`](src/Application/Import/Services/OfxParsingService.cs)
- Invalid OFX/XML structure throws exception during parsing
- Parsing exceptions are caught by [`CustomExceptionHandler`](src/Controllers/Middleware/CustomExceptionHandler.cs) and returned as 400 Bad Request

**No execution risk:**
- Files are parsed as data only (XML/SGML parsing)
- No code execution or script evaluation
- Read-only stream access (no file storage on disk)

### Stream Handling

**Direct stream processing (no buffering):**
```csharp
using var stream = file.OpenReadStream();
var result = await importReviewFeature.ImportFileAsync(stream, file.FileName);
```

**Benefits:**
- Memory-efficient (no intermediate buffering)
- Fast processing (direct read from upload stream)
- No temporary file storage (security/cleanup advantage)
- Automatic disposal via `using` statement

## Authorization

### Tenant Role Enforcement

**Controller-level authorization:**
```csharp
[RequireTenantRole(TenantRole.Editor)]
```

**Role requirements:**
- **Editor** - Can upload files, review imports, accept/delete transactions
- **Owner** - Can perform all import operations (Owner role implies Editor)
- **Viewer** - Cannot access import endpoints (read-only role)

**Enforcement mechanism:**
- `[RequireTenantRole]` - Custom authorization policy attribute that handles both authentication and authorization
- Authorization handler checks user is authenticated and has required role in current tenant
- Returns 401 Unauthorized if not authenticated, 403 Forbidden if authenticated but lacks required role

### Tenant Isolation

**Automatic tenant scoping:**
- All operations scoped to authenticated user's current tenant via [`ITenantProvider`](src/Entities/Tenancy/Providers/ITenantProvider.cs)
- Tenant context set by [`TenantContextMiddleware`](src/Controllers/Middleware/TenantContextMiddleware.cs) before controller execution
- No TenantId parameters in controller methods (implicit isolation)
- Impossible to access another tenant's pending imports

**Example:**
```csharp
// User A (Tenant 1) uploads file
POST /api/import/upload
→ Stored with TenantId = Tenant 1

// User B (Tenant 2) gets pending review
GET /api/import/review
→ Returns only Tenant 2 imports (cannot see User A's imports)
```

## Logging Pattern

All logging follows the project's [`docs/LOGGING-POLICY.md`](../../LOGGING-POLICY.md).

### LoggerMessage Methods

**Event ID assignments (1-9999, unique per class):**
- Event 1: `LogStarting()` - Debug level, no parameters
- Event 2: `LogOk()` - Information level, no parameters
- Event 3: `LogOkCount(int count)` - Information level, with count
- Event 4: `LogStartingCount(int count)` - Debug level, with count
- Event 5: `LogValidationError(string message)` - Warning level, with error message

### CallerMemberName Pattern

All log methods include `[CallerMemberName] string? location = null` as the last parameter:

```csharp
[LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
private partial void LogStarting([CallerMemberName] string? location = null);
```

**Benefits:**
- Automatically captures calling method name (e.g., "UploadFile", "GetPendingReview")
- No manual string constants needed
- Consistent location tracking across all logs
- Enables filtering by controller method in log aggregation tools

### Log Levels

**Debug level - "Starting" messages:**
```csharp
LogStarting(); // "{Location}: Starting"
LogStartingCount(keys.Length); // "{Location}: Starting {Count} items"
```

**Information level - "OK" messages:**
```csharp
LogOk(); // "{Location}: OK"
LogOkCount(result.ImportedCount); // "{Location}: OK {Count} items"
```

**Warning level - Validation errors:**
```csharp
LogValidationError("File is required and cannot be empty");
// "{Location}: Validation error {Message}"
```

### Structured Logging Context

**Automatic scope (via middleware):**
- UserId (authenticated user GUID)
- TenantId (current tenant GUID)
- TraceId (distributed tracing)
- SpanId (distributed tracing)

**Example log output (container/CI):**
```
DEBUG: UploadFile: Starting
INFO: UploadFile: OK 150 items
```

**Example structured log (Application Insights):**
```json
{
  "timestamp": "2024-01-20T10:30:00Z",
  "level": "Information",
  "message": "UploadFile: OK 150 items",
  "location": "UploadFile",
  "count": 150,
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "tenantId": "660e8400-e29b-41d4-a716-446655440001",
  "traceId": "abc123def456"
}
```

### Sensitive Data Considerations

**Never logged (any environment):**
- File contents (raw OFX data)
- Transaction amounts from import (financial PII)
- Payee names from import (financial PII)
- External IDs (FITID) (potential PII)

**Always safe to log:**
- Transaction counts (statistics)
- File names (user-provided, non-sensitive)
- Validation error messages (generic)
- UserId/TenantId (non-PII identifiers)

## Error Handling

### Standard ProblemDetails Responses

All errors return RFC 7807 ProblemDetails JSON:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Invalid file type",
  "status": 400,
  "detail": "Only .ofx, .qfx files are allowed.",
  "traceId": "00-abc123def456-xyz789-00"
}
```

### Status Codes

#### 400 Bad Request

**Validation errors:**
- File is null or empty
- Invalid file extension
- File size exceeds maximum
- No transaction keys provided (accept/delete operations)
- Invalid OFX/XML content (parsing failure)

**Example:**
```csharp
return Problem(
    title: "Invalid file upload",
    detail: "File is required and cannot be empty.",
    statusCode: StatusCodes.Status400BadRequest);
```

#### 401 Unauthorized

**Authentication required:**
- No valid JWT token provided
- Expired JWT token
- Invalid JWT signature

**Handled by:** ASP.NET Core authentication middleware (automatic)

#### 403 Forbidden

**Authorization failures:**
- User authenticated but lacks required tenant role (not Editor or Owner)
- User not a member of the current tenant

**Handled by:** `[RequireTenantRole]` authorization policy (automatic)

#### 500 Internal Server Error

**Unhandled exceptions:**
- Database connection failures
- Unexpected application errors
- Null reference exceptions

**Handled by:** [`CustomExceptionHandler`](src/Controllers/Middleware/CustomExceptionHandler.cs) middleware

**Response includes:**
- TraceId for log correlation
- Generic error message (no sensitive details exposed)
- Full exception logged internally

### Exception Handling Flow

```
Controller → Feature → Repository
    ↓           ↓          ↓
    Exception propagates up
    ↓
CustomExceptionHandler middleware
    ↓
Logs full exception with context
    ↓
Returns ProblemDetails with TraceId
```

**Example unhandled exception:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "00-abc123def456-xyz789-00"
}
```

User provides TraceId to support team → Support queries logs by TraceId → Full exception context available for debugging.

## Testing Considerations

**Integration tests (Controller + Feature + Repository):**
- File upload with valid OFX file (verify parsing and storage)
- File upload with invalid extension (verify 400 response)
- File upload with oversized file (verify 400 response)
- Get pending review (verify tenant isolation)
- Complete review (verify selected transactions copied to Transaction table AND all transactions deleted from ImportReviewTransaction)
- Delete all transactions (verify removal of all ImportReviewTransaction records without accepting any)
- Authorization enforcement (verify 403 for Viewer role)
- Verify CompleteReview atomically accepts selected AND deletes all transactions
- Test authorization enforcement (verify 403 for Viewer role)
- Test tenant isolation (User A cannot see User B's imports)

**Test example:**
```csharp
[Test]
public async Task UploadFile_ValidOfxFile_ReturnsImportResult()
{
    // Given: Authenticated user with Editor role

    // And: Valid OFX file content

    // When: File is uploaded

    // Then: 200 OK should be returned

    // And: Response should contain import result
}
```

## Implementation Checklist

- [ ] Create [`ImportController.cs`](src/Controllers/ImportController.cs)
- [ ] Add XML documentation comments to controller class and all methods
- [ ] Implement all four endpoints (upload, review, completeReview, deleteAll)
- [ ] Add LoggerMessage methods following logging policy
- [ ] Add `[ProducesResponseType]` attributes for all status codes
- [ ] Implement file upload validation (extension, size, empty checks)
- [ ] Add authorization attribute (`[RequireTenantRole]`)

## References

**Design Documents:**
- [`DESIGN-BANK-IMPORT.md`](DESIGN-BANK-IMPORT.md) - Overall feature architecture
- [`DESIGN-BANK-IMPORT-APPLICATION.md`](DESIGN-BANK-IMPORT-APPLICATION.md) - Application layer design (ImportReviewFeature)
- [`DESIGN-BANK-IMPORT-DATABASE.md`](DESIGN-BANK-IMPORT-DATABASE.md) - Database schema and entities
- [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - Product requirements

**Project Standards:**
- [`docs/LOGGING-POLICY.md`](../../LOGGING-POLICY.md) - Logging standards and patterns
- [`.roorules`](../../.roorules) - Project coding standards and patterns
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) - Clean Architecture principles

**Related Code:**
- [`TransactionsController.cs`](src/Controllers/TransactionsController.cs) - Reference controller implementation
- [`TenantContextMiddleware.cs`](src/Controllers/Middleware/TenantContextMiddleware.cs) - Tenant context setup
- [`CustomExceptionHandler.cs`](src/Controllers/Middleware/CustomExceptionHandler.cs) - Exception handling middleware
- [`RequireTenantRoleAttribute.cs`](src/Controllers/Tenancy/Authorization/RequireTenantRoleAttribute.cs) - Authorization policy
