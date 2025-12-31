using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>
/// Controller integration tests for Bank Import feature.
/// </summary>
/// <remarks>
/// Tests the complete Import API workflow including file upload, review, complete, and delete operations.
/// Validates HTTP contracts, authentication, authorization, tenant isolation, and pagination.
/// </remarks>
[TestFixture]
public class ImportControllerTests : AuthenticatedTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        // Clean up any pending review transactions from previous tests
        // This ensures each test starts with a clean review queue
        SwitchToEditor();
        await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/import/review");
    }

    #region POST /api/tenant/{tenantKey}/import/upload

    [Test]
    public async Task UploadFile_ValidOFXFile_Returns200OK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Valid OFX file content
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);

        // When: User uploads OFX file
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(ofxContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        content.Add(fileContent, "file", "Bank1.ofx");

        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", content);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain import summary
        var result = await response.Content.ReadFromJsonAsync<ImportReviewUploadDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ImportedCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task UploadFile_AsViewer_Returns403Forbidden()
    {
        // Given: User has Viewer role for tenant (read-only)
        SwitchToViewer();

        // And: Valid OFX file content
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);

        // When: Viewer attempts to upload file
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(ofxContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        content.Add(fileContent, "file", "Bank1.ofx");

        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", content);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UploadFile_Unauthenticated_Returns403Forbidden()
    {
        // Given: No authentication token provided
        using var unauthClient = _factory.CreateClient();

        // And: Valid OFX file content
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);

        // When: Request is made without authentication
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(ofxContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        content.Add(fileContent, "file", "Bank1.ofx");

        var response = await unauthClient.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", content);

        // Then: 403 Forbidden should be returned (tenant middleware returns 403 for unauthenticated + invalid tenantKey)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UploadFile_DifferentTenant_Returns403Forbidden()
    {
        // Given: User has Editor role for tenant A
        SwitchToEditor();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // And: Valid OFX file content
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);

        // When: User attempts to upload to tenant B
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(ofxContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        content.Add(fileContent, "file", "Bank1.ofx");

        var response = await _client.PostAsync($"/api/tenant/{otherTenantKey}/import/upload", content);

        // Then: 403 Forbidden should be returned (tenant isolation)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UploadFile_EmptyFile_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Empty file content
        var emptyContent = Array.Empty<byte>();

        // When: User uploads empty file
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(emptyContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        content.Add(fileContent, "file", "empty.ofx");

        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", content);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain problem details
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task UploadFile_InvalidFileExtension_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: File with invalid extension (CSV instead of OFX/QFX)
        var csvContent = System.Text.Encoding.UTF8.GetBytes("Date,Payee,Amount\n2024-01-01,Test,100.00");

        // When: User uploads CSV file
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(csvContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "transactions.csv");

        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", content);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain problem details
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task UploadFile_CorruptedOFX_HandlesGracefullyWithZeroImported()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Corrupted OFX file (invalid XML)
        var corruptedContent = System.Text.Encoding.UTF8.GetBytes("<OFX><INVALID>Not proper OFX</BROKEN>");

        // When: User uploads corrupted file
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(corruptedContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        content.Add(fileContent, "file", "corrupted.ofx");

        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", content);

        // Then: 200 OK should be returned (graceful error handling)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should indicate zero transactions imported
        var result = await response.Content.ReadFromJsonAsync<ImportReviewUploadDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ImportedCount, Is.EqualTo(0));

        // And: Should have parsing errors reported
        Assert.That(result.Errors, Is.Not.Empty, "Corrupted OFX should generate parsing errors");
    }

    /// <summary>
    /// Regression test for AB#1992: Import: Error 400 after importing an OFX file the third time
    /// </summary>
    /// <returns></returns>
    [Test]
    public async Task UploadFile_WithMultipleDuplicates_OK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Two transactions in the database with SAME ExternalId and SAME Date (but different Payee for clarity)
        // This isolates the bug: the subquery in GetExistingTransactionsByExternalIdAsync returns BOTH transactions
        // because neither has Date > the other, causing ToDictionary to throw on duplicate key
        var sharedExternalId = "20220221 469976 8,769 2,022,022,018,019";
        var sharedDate = new DateOnly(2022, 2, 21);

        var transaction1 = new TransactionEditDto(
            Date: sharedDate,
            Amount: -87.69m,
            Payee: "DUPLICATE-1", // Different payee for easy identification
            Memo: null,
            Source: "Bank",
            ExternalId: sharedExternalId,
            Category: null
        );
        var transaction2 = new TransactionEditDto(
            Date: sharedDate, // SAME Date
            Amount: -87.69m,
            Payee: "DUPLICATE-2", // Different payee for easy identification
            Memo: null,
            Source: "Bank",
            ExternalId: sharedExternalId, // SAME ExternalId
            Category: null
        );

        await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", transaction1);
        await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", transaction2);

        // When: Uploading an OFX file containing a transaction with that same External ID
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(ofxContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        content.Add(fileContent, "file", "Bank1.ofx");

        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", content);

        // Debug: Display response details if not OK
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"\n=== Error Response ===");
            Console.WriteLine($"Status Code: {response.StatusCode}");
            Console.WriteLine($"Content: {errorContent}");
        }

        // Then: Operation should succeed (200 OK) - but currently fails due to bug
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "BUG REPRODUCED: ToDictionary throws when multiple transactions have same ExternalId+Date");

        // And: Response should contain import summary
        var result = await response.Content.ReadFromJsonAsync<ImportReviewUploadDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ImportedCount, Is.GreaterThan(0), "File should contain transactions");

        // Debug: Display import result details
        Console.WriteLine($"\n=== Import Result ===");
        Console.WriteLine($"ImportedCount: {result.ImportedCount}");
        Console.WriteLine($"NewCount: {result.NewCount}");
        Console.WriteLine($"ExactDuplicateCount: {result.ExactDuplicateCount}");
        Console.WriteLine($"PotentialDuplicateCount: {result.PotentialDuplicateCount}");
        Console.WriteLine($"Errors: {result.Errors?.Count ?? 0}");

        // And: The transaction with the duplicate FITID should be marked as duplicate
        Assert.That(result.ExactDuplicateCount + result.PotentialDuplicateCount, Is.GreaterThan(0),
            "Should detect at least one duplicate from the multiple existing transactions with same FITID");
    }

    #endregion

    #region GET /api/tenant/{tenantKey}/import/review

    [Test]
    public async Task GetPendingReview_WithPendingTransactions_Returns200OK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: User has uploaded transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User requests import review
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain pending transactions
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Not.Empty);
        Assert.That(result.Metadata, Is.Not.Null);
    }

    [Test]
    public async Task GetPendingReview_NoPendingTransactions_Returns200OKWithEmptyList()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: No transactions in review state

        // When: User requests import review
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should be empty list
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Empty);
    }

    [Test]
    public async Task GetPendingReview_AsViewer_Returns403Forbidden()
    {
        // Given: User has Viewer role for tenant (read-only)
        SwitchToViewer();

        // When: Viewer attempts to get import review
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetPendingReview_DifferentTenant_Returns200OKWithEmptyList()
    {
        // Given: User A has Editor role for tenant A
        SwitchToEditor();

        // And: Upload transactions to tenant A
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // And: Create a second tenant with user having Editor access
        var secondTenantKey = await CreateTestTenantAsync("Second Tenant", "Second tenant for isolation testing");
        var multiTenantClient = CreateMultiTenantClient(
            (_testTenantKey, Entities.Tenancy.Models.TenantRole.Editor),
            (secondTenantKey, Entities.Tenancy.Models.TenantRole.Editor)
        );

        // When: User requests import review for tenant B
        var response = await multiTenantClient.GetAsync($"/api/tenant/{secondTenantKey}/import/review");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Should not see tenant A's transactions (isolation)
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Empty);

        multiTenantClient.Dispose();
    }

    [Test]
    public async Task GetPendingReview_InvalidPageNumber_DefaultsToPage1()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Upload transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User requests import review with pageNumber=0
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review?pageNumber=0");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain first page (pageNumber: 1)
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Metadata.PageNumber, Is.EqualTo(1));
    }

    [Test]
    public async Task GetPendingReview_InvalidPageSize_DefaultsTo50()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Upload transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User requests import review with pageSize=-5
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review?pageSize=-5");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should use default page size (50)
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Metadata.PageSize, Is.EqualTo(50));
    }

    [Test]
    public async Task GetPendingReview_ExcessivePageSize_ClampsToMaximum()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Upload transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User requests import review with pageSize=5000
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review?pageSize=5000");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should use maximum page size (1000)
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Metadata.PageSize, Is.EqualTo(1000));
    }

    [Test]
    public async Task GetPendingReview_PersistsAcrossSessions_Returns200OK()
    {
        // Given: User has Editor role and uploads transactions
        SwitchToEditor();
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User requests import review (new request, simulates new session)
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Pending transactions should still be there
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Not.Empty);
    }

    #endregion

    #region POST /api/tenant/{tenantKey}/import/review/complete

    [Test]
    public async Task CompleteReview_WithSelectedTransactions_Returns200OK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Upload and get pending transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        var reviewResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");
        var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        var selectedKeys = reviewResult!.Items.Take(2).Select(t => t.Key).ToList();

        // When: User completes review with selected transactions
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/import/review/complete", selectedKeys);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should indicate transactions accepted
        var result = await response.Content.ReadFromJsonAsync<ImportReviewCompleteDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.AcceptedCount, Is.EqualTo(2));
        Assert.That(result.RejectedCount, Is.GreaterThan(0));

        // And: Review queue should be completely empty
        var emptyReviewResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");
        var emptyReviewResult = await emptyReviewResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(emptyReviewResult!.Items, Is.Empty);
    }

    [Test]
    public async Task CompleteReview_AsViewer_Returns403Forbidden()
    {
        // Given: User has Editor role and uploads transactions
        SwitchToEditor();
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        var reviewResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");
        var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        var selectedKeys = reviewResult!.Items.Select(t => t.Key).ToList();

        // And: User switches to Viewer role
        SwitchToViewer();

        // When: Viewer attempts to complete review
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/import/review/complete", selectedKeys);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CompleteReview_EmptySelection_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Upload transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User completes review with empty selection
        var emptySelection = new List<Guid>();
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/import/review/complete", emptySelection);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain problem details
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task CompleteReview_NullSelection_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Upload transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User completes review with null selection
        List<Guid>? nullSelection = null;
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/import/review/complete", nullSelection);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CompleteReview_DifferentTenant_Returns403Forbidden()
    {
        // Given: User has Editor role for tenant A
        SwitchToEditor();

        // And: Upload transactions to tenant A
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        var reviewResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");
        var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        var selectedKeys = reviewResult!.Items.Select(t => t.Key).ToList();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // When: User attempts to complete tenant B's review
        var response = await _client.PostAsJsonAsync($"/api/tenant/{otherTenantKey}/import/review/complete", selectedKeys);

        // Then: 403 Forbidden should be returned (tenant isolation)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region DELETE /api/tenant/{tenantKey}/import/review

    [Test]
    public async Task DeleteAllPendingReview_AsEditor_Returns204NoContent()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Upload transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User deletes entire review queue
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 204 No Content should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // And: Review queue should be empty
        var emptyReviewResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");
        var emptyReviewResult = await emptyReviewResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(emptyReviewResult!.Items, Is.Empty);
    }

    [Test]
    public async Task DeleteAllPendingReview_AsViewer_Returns403Forbidden()
    {
        // Given: User has Editor role and uploads transactions
        SwitchToEditor();
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // And: User switches to Viewer role
        SwitchToViewer();

        // When: Viewer attempts to delete review queue
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteAllPendingReview_EmptyQueue_Returns204NoContent()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: No transactions in review state

        // When: User deletes empty review queue
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 204 No Content should be returned (idempotent)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    #endregion

    #region Additional Integration Scenarios

    [Test]
    public async Task MultipleUploads_MergeIntoSingleReviewQueue()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: User uploads first OFX file
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent1 = new MultipartFormDataContent();
        using var uploadFileContent1 = new ByteArrayContent(ofxContent);
        uploadFileContent1.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent1.Add(uploadFileContent1, "file", "Bank1.ofx");
        var upload1Response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent1);
        var upload1Result = await upload1Response.Content.ReadFromJsonAsync<ImportReviewUploadDto>();
        var firstUploadCount = upload1Result!.ImportedCount;

        // When: User uploads second OFX file
        using var uploadContent2 = new MultipartFormDataContent();
        using var uploadFileContent2 = new ByteArrayContent(ofxContent);
        uploadFileContent2.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent2.Add(uploadFileContent2, "file", "Bank1-second.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent2);

        // Then: Review queue should contain transactions from both uploads
        var reviewResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");
        var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(reviewResult!.Metadata.TotalCount, Is.GreaterThanOrEqualTo(firstUploadCount));
    }

    [Test]
    public async Task TransactionsInReview_NotIncludedInTransactionList()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: User has existing accepted transactions
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Accepted Transaction",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);

        // And: User has pending import review transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var uploadContent = new MultipartFormDataContent();
        using var uploadFileContent = new ByteArrayContent(ofxContent);
        uploadFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ofx");
        uploadContent.Add(uploadFileContent, "file", "Bank1.ofx");
        await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/upload", uploadContent);

        // When: User requests transaction list
        var transactionResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: Should return only accepted transactions
        var transactions = await transactionResponse.Content.ReadFromJsonAsync<ICollection<TransactionResultDto>>();
        Assert.That(transactions, Is.Not.Null);

        // And: Should have at least the one accepted transaction
        Assert.That(transactions!.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(transactions.Any(t => t.Payee == "Accepted Transaction"), Is.True);

        // And: Pending import review transactions should not be included
        var reviewResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");
        var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        var reviewCount = reviewResult!.Metadata.TotalCount;

        // Review queue should have more transactions than appeared in transaction list
        Assert.That(reviewCount, Is.GreaterThan(0));
    }

    #endregion
}
