using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>
/// Controller integration tests for Bank Import API endpoints.
/// </summary>
/// <remarks>
/// Tests HTTP-specific concerns: authentication, authorization, status codes, serialization, and request validation.
/// Business logic for import operations is tested in ImportReviewFeatureTests (Application Integration layer).
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

    #region POST /api/tenant/{tenantKey}/import/upload - HTTP Concerns

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

        // And: Response should deserialize to ImportReviewUploadDto
        var result = await response.Content.ReadFromJsonAsync<ImportReviewUploadDto>();
        Assert.That(result, Is.Not.Null);
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

    #endregion

    #region GET /api/tenant/{tenantKey}/import/review - HTTP Concerns

    [Test]
    public async Task GetPendingReview_AsEditor_Returns200OK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // When: User requests import review
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should deserialize to PaginatedResultDto
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ImportReviewTransactionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Metadata, Is.Not.Null);
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

    #endregion

    #region POST /api/tenant/{tenantKey}/import/review/complete - HTTP Concerns

    [Test]
    public async Task CompleteReview_AsEditor_Returns200OK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // When: User completes review
        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/review/complete", null);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should deserialize to ImportReviewCompleteDto
        var result = await response.Content.ReadFromJsonAsync<ImportReviewCompleteDto>();
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task CompleteReview_AsViewer_Returns403Forbidden()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // When: Viewer attempts to complete review
        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/review/complete", null);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CompleteReview_DifferentTenant_Returns403Forbidden()
    {
        // Given: User has Editor role for tenant A
        SwitchToEditor();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // When: User attempts to complete tenant B's review
        var response = await _client.PostAsync($"/api/tenant/{otherTenantKey}/import/review/complete", null);

        // Then: 403 Forbidden should be returned (tenant isolation)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region DELETE /api/tenant/{tenantKey}/import/review - HTTP Concerns

    [Test]
    public async Task DeleteAllPendingReview_AsEditor_Returns204NoContent()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // When: User deletes entire review queue
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/import/review");

        // Then: 204 No Content should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task DeleteAllPendingReview_AsViewer_Returns403Forbidden()
    {
        // Given: User has Viewer role for tenant
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

    #region POST /api/tenant/{tenantKey}/import/review/set-selection - HTTP Concerns

    [Test]
    public async Task SetSelection_EmptyKeys_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // When: User sends empty keys list
        var request = new SetSelectionRequest([], false);
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/import/review/set-selection", request);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain problem details
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task SetSelection_AsViewer_Returns403Forbidden()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // When: Viewer attempts to update selection
        var request = new SetSelectionRequest([Guid.NewGuid()], true);
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/import/review/set-selection", request);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region POST /api/tenant/{tenantKey}/import/review/select-all - HTTP Concerns

    [Test]
    public async Task SelectAll_AsEditor_Returns204NoContent()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // When: User selects all transactions
        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/review/select-all", null);

        // Then: 204 No Content should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task SelectAll_AsViewer_Returns403Forbidden()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // When: Viewer attempts to select all
        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/review/select-all", null);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region POST /api/tenant/{tenantKey}/import/review/deselect-all - HTTP Concerns

    [Test]
    public async Task DeselectAll_AsEditor_Returns204NoContent()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // When: User deselects all transactions
        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/review/deselect-all", null);

        // Then: 204 No Content should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task DeselectAll_AsViewer_Returns403Forbidden()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // When: Viewer attempts to deselect all
        var response = await _client.PostAsync($"/api/tenant/{_testTenantKey}/import/review/deselect-all", null);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region GET /api/tenant/{tenantKey}/import/review/summary - HTTP Concerns

    [Test]
    public async Task GetReviewSummary_AsEditor_Returns200OK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // When: User requests summary
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review/summary");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should deserialize to ImportReviewSummaryDto
        var result = await response.Content.ReadFromJsonAsync<ImportReviewSummaryDto>();
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task GetReviewSummary_AsViewer_Returns403Forbidden()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // When: Viewer attempts to get summary
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/import/review/summary");

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion
}
