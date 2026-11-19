using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>
/// Controller integration tests for transaction split workbook template and upload endpoints.
/// </summary>
[TestFixture]
public class TransactionSplitUploadControllerTests : AuthenticatedTestBase
{
    private const string TemplateUrl = "/api/transactions/splits/upload-template";
    private const int MaxWorkbookSizeBytes = 1 * 1024 * 1024;

    [SetUp]
    public void SetUp()
    {
        SwitchToEditor();
    }

    [Test]
    public async Task GetTemplate_AsAuthenticated_ReturnsXlsxAttachment()
    {
        // Given: An authenticated request
        // When: Requesting the reusable split template
        using var response = await _client.GetAsync(TemplateUrl);

        // Then: The endpoint returns an XLSX download
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        Assert.That(response.Content.Headers.ContentDisposition?.FileName,
            Is.EqualTo("yofi-transaction-splits-template.xlsx"));
        Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.Not.Empty);
    }

    [Test]
    public async Task GetTemplate_Unauthenticated_ReturnsUnauthorized()
    {
        // Given: An unauthenticated client
        using var factory = new BaseTestWebApplicationFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();

        // When: Requesting the template
        using var response = await client.GetAsync(TemplateUrl);

        // Then: Authentication is required
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UploadSplitWorkbook_AsEditor_AppendsRowsAndReturnsBalancedDetail()
    {
        // Given: A transaction and a valid first-sheet workbook
        var original = await CreateTransactionAsync();
        using var content = CreateUploadForm(CreateWorkbook(
            ("Groceries", "-40", "Weekly"),
            ("Dining", "-20", null)));

        // When: Uploading the workbook
        using var response = await _client.PostAsync(
            UploadUrl(original.Key),
            content);

        // Then: The response and persisted transaction contain the uploaded splits
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Splits.Select(split => split.Amount),
            Is.EqualTo(new[] { -40m, -40m, -20m }));
        Assert.That(updated.Splits.Select(split => split.Category),
            Is.EqualTo(new[] { "Groceries", "Groceries", "Dining" }));
        Assert.That(updated.Splits.Select(split => split.Order),
            Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(updated.IsBalanced, Is.True);

        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(
            TransactionUrl(original.Key));
        Assert.That(persisted!.Splits, Is.EqualTo(updated.Splits));
        Assert.That(persisted.SplitsTotal, Is.EqualTo(updated.SplitsTotal));
        Assert.That(persisted.IsBalanced, Is.True);
    }

    [Test]
    public async Task UploadSplitWorkbook_WithoutFile_Returns400ProblemDetails()
    {
        // Given: A transaction and multipart content with no file
        var original = await CreateTransactionAsync();
        using var content = new MultipartFormDataContent();

        // When: Uploading without a file
        using var response = await _client.PostAsync(UploadUrl(original.Key), content);

        // Then: HTTP validation rejects the missing workbook
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Errors, Is.Not.Empty);
    }

    [Test]
    public async Task UploadSplitWorkbook_WithWrongExtension_Returns400ProblemDetails()
    {
        // Given: A transaction and a non-XLSX filename
        var original = await CreateTransactionAsync();
        using var content = CreateUploadForm(CreateWorkbook(("Groceries", "-40", null)), "splits.csv");

        // When: Uploading the wrong format
        using var response = await _client.PostAsync(UploadUrl(original.Key), content);

        // Then: HTTP validation rejects the extension
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem!.Errors["file"].Single(),
            Is.EqualTo("Only .xlsx workbooks are supported."));
    }

    [Test]
    public async Task UploadSplitWorkbook_WhenOversized_Returns400ProblemDetails()
    {
        // Given: A transaction and a workbook body over 1 MiB
        var original = await CreateTransactionAsync();
        using var content = CreateUploadForm(
            new byte[MaxWorkbookSizeBytes + 1],
            "oversized.xlsx");

        // When: Uploading the oversized workbook
        using var response = await _client.PostAsync(UploadUrl(original.Key), content);

        // Then: HTTP validation rejects it before parsing
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem!.Errors["file"].Single(),
            Is.EqualTo("The workbook must not exceed 1 MB."));
    }

    [Test]
    public async Task UploadSplitWorkbook_WithMalformedWorkbook_ReturnsGrouped400ProblemDetails()
    {
        // Given: A transaction and unreadable workbook bytes
        var original = await CreateTransactionAsync();
        using var content = CreateUploadForm([1, 2, 3, 4], "malformed.xlsx");

        // When: Uploading unreadable workbook bytes
        using var response = await _client.PostAsync(UploadUrl(original.Key), content);

        // Then: The response carries grouped split workbook validation details
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Title, Is.EqualTo("The split workbook failed validation."));
        Assert.That(problem.Detail,
            Is.EqualTo("Correct the workbook errors and upload it again. No splits were changed."));
        Assert.That(problem.Errors.Keys, Has.Some.Contains("file"));
    }

    [Test]
    public async Task UploadSplitWorkbook_WithInvalidRow_ReturnsGrouped400WithoutChanges()
    {
        // Given: A transaction and a workbook with invalid category and memo
        var original = await CreateTransactionAsync();
        using var content = CreateUploadForm(CreateWorkbook(
            ("Valid", "-40", "Valid memo"),
            (new string('x', 201), "-20", new string('m', 501))));

        // When: Uploading the invalid workbook
        using var response = await _client.PostAsync(UploadUrl(original.Key), content);

        // Then: Both row issues are returned and the transaction is unchanged
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem!.Errors.Keys, Contains.Item("Row 3: Category"));
        Assert.That(problem.Errors.Keys, Contains.Item("Row 3: Memo"));

        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(
            TransactionUrl(original.Key));
        Assert.That(persisted!.Key, Is.EqualTo(original.Key));
        Assert.That(persisted.Amount, Is.EqualTo(original.Amount));
        Assert.That(persisted.Category, Is.EqualTo(original.Category));
        Assert.That(persisted.SplitsTotal, Is.EqualTo(original.SplitsTotal));
        Assert.That(persisted.IsBalanced, Is.True);
    }

    [Test]
    public async Task UploadSplitWorkbook_AsViewer_ReturnsForbidden()
    {
        // Given: Viewer access and a transaction
        var original = await CreateTransactionAsync();
        SwitchToViewer();
        using var content = CreateUploadForm(CreateWorkbook(("Groceries", "-40", null)));

        // When: A Viewer attempts the upload
        using var response = await _client.PostAsync(UploadUrl(original.Key), content);

        // Then: The Editor role is required
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UploadSplitWorkbook_Unauthenticated_ReturnsUnauthorized()
    {
        // Given: An unauthenticated client
        using var factory = new BaseTestWebApplicationFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        using var content = CreateUploadForm(CreateWorkbook(("Groceries", "-40", null)));

        // When: Uploading to a tenant route
        using var response = await client.PostAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{Guid.NewGuid()}/splits/upload",
            content);

        // Then: Authentication is required
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UploadSplitWorkbook_WithoutTenantAccess_ReturnsForbidden()
    {
        // Given: A transaction and a client authenticated only for another tenant
        var original = await CreateTransactionAsync();
        var otherTenantKey = await CreateTestTenantAsync("Other workspace");
        using var client = _factory.CreateAuthenticatedClient(otherTenantKey);
        using var content = CreateUploadForm(CreateWorkbook(("Groceries", "-40", null)));

        // When: Using the current tenant route with a client that lacks access
        using var response = await client.PostAsync(
            UploadUrl(original.Key),
            content);

        // Then: Tenant isolation returns forbidden
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UploadSplitWorkbook_WithTransactionInAnotherTenant_ReturnsNotFoundWithoutChanges()
    {
        // Given: A transaction owned by another tenant
        var otherTenantKey = await CreateTestTenantAsync("Other workspace");
        var otherTransaction = await CreateTransactionAsync(otherTenantKey);
        using var content = CreateUploadForm(CreateWorkbook(("Groceries", "-40", null)));

        // When: The current tenant attempts to upload
        using var response = await _client.PostAsync(
            UploadUrl(otherTransaction.Key),
            content);

        // Then: The transaction is not exposed across tenant boundaries
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem!.Status, Is.EqualTo(404));

        using var otherTenantClient = _factory.CreateAuthenticatedClient(
            otherTenantKey,
            TenantRole.Editor);
        var persisted = await otherTenantClient.GetFromJsonAsync<TransactionDetailDto>(
            $"/api/tenant/{otherTenantKey}/transactions/{otherTransaction.Key}");
        Assert.That(persisted!.Amount, Is.EqualTo(-100m));
        Assert.That(persisted.Category, Is.EqualTo("Groceries"));
        Assert.That(persisted.Splits, Is.Empty);
    }

    [Test]
    public async Task UploadSplitWorkbook_OpenApiDescribesTemplateAndUploadContracts()
    {
        // Given: An authenticated OpenAPI request
        using var response = await _client.GetAsync("/openapi/v1.json");

        // Then: Both endpoints expose their designed request and response contracts
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        var templatePath = paths.EnumerateObject().Single(property =>
            property.Name.Equals(TemplateUrl, StringComparison.OrdinalIgnoreCase));
        Assert.That(templatePath.Value.GetProperty("get").TryGetProperty("responses", out _), Is.True);

        var uploadPath = paths.EnumerateObject().Single(property =>
            property.Name.Equals("/api/tenant/{tenantKey}/Transactions/{key}/splits/upload",
                StringComparison.OrdinalIgnoreCase));
        var operation = uploadPath.Value.GetProperty("post");
        Assert.That(operation.GetProperty("requestBody").GetProperty("content")
            .TryGetProperty("multipart/form-data", out _), Is.True);
        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "401", "403", "404", "409" })
        {
            Assert.That(responses.TryGetProperty(status, out _), Is.True, $"Missing {status} response");
        }
    }

    private string TransactionUrl(Guid key)
        => $"/api/tenant/{_testTenantKey}/transactions/{key}";

    private string UploadUrl(Guid key)
        => $"{TransactionUrl(key)}/splits/upload";

    private async Task<TransactionDetailDto> CreateTransactionAsync(Guid? tenantKey = null)
    {
        var targetTenantKey = tenantKey ?? _testTenantKey;
        using var client = tenantKey is null
            ? null
            : _factory.CreateAuthenticatedClient(targetTenantKey, TenantRole.Editor);
        var activeClient = client ?? _client;
        var response = await activeClient.PostAsJsonAsync(
            $"/api/tenant/{targetTenantKey}/transactions",
            new TransactionEditDto(
                new DateOnly(2026, 1, 1),
                -100m,
                "Split purchase",
                "Transaction memo",
                "Imported",
                "External ID",
                "Groceries"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<TransactionDetailDto>())!;
    }

    private static MultipartFormDataContent CreateUploadForm(
        byte[] workbook,
        string fileName = "splits.xlsx")
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(workbook), "file", fileName);
        return content;
    }

    private static byte[] CreateWorkbook(params (string Category, string Amount, string? Memo)[] rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";
        worksheet.Cell(1, 3).Value = "Memo";
        for (var index = 0; index < rows.Length; index++)
        {
            var row = index + 2;
            worksheet.Cell(row, 1).Value = rows[index].Category;
            worksheet.Cell(row, 2).Value = rows[index].Amount;
            if (rows[index].Memo is not null)
            {
                worksheet.Cell(row, 3).Value = rows[index].Memo;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

}
