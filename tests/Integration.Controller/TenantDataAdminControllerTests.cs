using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Dto;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TenantDataAdminControllerTests : AuthenticatedTestBase
{
    protected override TenantRole DefaultRole => TenantRole.Owner;
    private string DataUrl => $"/api/tenant/{_testTenantKey}/admin/data";
    private string ExportOptionsUrl => $"/api/tenant/{_testTenantKey}/admin/export/options";
    private string ExportUrl => $"/api/tenant/{_testTenantKey}/admin/export";
    private string ImportPreviewUrl => $"/api/tenant/{_testTenantKey}/admin/import/preview";
    private string ImportUrl => $"/api/tenant/{_testTenantKey}/admin/import";

    [SetUp]
    public void SetUp() => SwitchToOwner();

    [Test]
    public async Task DeleteAll_AsOwner_DeletesAllTenantDataAndPreservesWorkspaceMembershipAndOtherTenant()
    {
        var (tenantId, otherTenantId) = await SeedDataAsync();

        var response = await _client.SendAsync(DeleteRequest("Test Tenant"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<TenantDataDeletionResultDto>();
        Assert.That(result!.Deleted, Is.EqualTo(new TenantDataDeletionCountsDto(1, 1, 1, 1)));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db.Transactions.CountAsync(item => item.TenantId == tenantId), Is.Zero);
            Assert.That(await db.Splits.CountAsync(), Is.Zero);
            Assert.That(await db.PayeeMatchingRules.CountAsync(item => item.TenantId == tenantId), Is.Zero);
            Assert.That(await db.ImportReviewTransactions.CountAsync(item => item.TenantId == tenantId), Is.Zero);
            Assert.That(await db.Transactions.CountAsync(item => item.TenantId == otherTenantId), Is.EqualTo(1));
            Assert.That(await db.PayeeMatchingRules.CountAsync(item => item.TenantId == otherTenantId), Is.EqualTo(1));
            Assert.That(await db.ImportReviewTransactions.CountAsync(item => item.TenantId == otherTenantId), Is.EqualTo(1));
            Assert.That(await db.Tenants.CountAsync(item => item.Id == tenantId), Is.EqualTo(1));
            Assert.That(await db.UserTenantRoleAssignments.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
        }
    }

    [TestCase("test tenant")]
    [TestCase("Test Tenant ")]
    public async Task DeleteAll_WithNonExactConfirmation_Returns400AndDoesNotDelete(string confirmationName)
    {
        var (tenantId, _) = await SeedDataAsync();

        var response = await _client.SendAsync(DeleteRequest(confirmationName));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status, Is.EqualTo(400));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.That(await db.Transactions.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteAll_AsEditor_ReturnsForbidden()
    {
        SwitchToEditor();

        var response = await _client.SendAsync(DeleteRequest("Test Tenant"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteAll_WhenDatabaseDeleteFails_RollsBackAllData()
    {
        var (tenantId, _) = await SeedDataAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_tenant_data_transaction_delete() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Injected deletion failure';
            END;
            $$;
            CREATE TRIGGER reject_tenant_data_transaction_delete BEFORE DELETE ON "Transactions"
            FOR EACH ROW EXECUTE FUNCTION reject_tenant_data_transaction_delete();
            """);

        try
        {
            var response = await _client.SendAsync(DeleteRequest("Test Tenant"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(await db.Transactions.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
            Assert.That(await db.Splits.CountAsync(), Is.EqualTo(1));
            Assert.That(await db.PayeeMatchingRules.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
            Assert.That(await db.ImportReviewTransactions.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER reject_tenant_data_transaction_delete ON "Transactions";
                DROP FUNCTION reject_tenant_data_transaction_delete();
                """);
        }
    }

    [Test]
    public async Task GetExportOptions_AsOwner_ReturnsAvailableTypesAndCurrentTenantYears()
    {
        var (tenantId, otherTenantId) = await SeedDataAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Transactions.Where(item => item.TenantId == tenantId)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.Date, new DateOnly(2024, 1, 1)));
        await db.Transactions.Where(item => item.TenantId == otherTenantId)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.Date, new DateOnly(2026, 1, 1)));
        db.Transactions.Add(new Transaction
        {
            TenantId = tenantId,
            Date = new DateOnly(2025, 6, 15),
            Payee = "Second target transaction"
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync(ExportOptionsUrl);
        var options = await response.Content.ReadFromJsonAsync<TenantDataExportOptionsDto>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options!.DataTypes.Select(type => type.DataType),
                Is.EqualTo(new[] { "PayeeMatchingRules", "Transactions" }));
            Assert.That(options.DataTypes.Single(type => type.DataType == "Transactions").SupportsDateFilter, Is.True);
            Assert.That(options.DataTypes.Single(type => type.DataType == "PayeeMatchingRules").SupportsDateFilter, Is.False);
            Assert.That(options.Years, Is.EqualTo(new[] { 2025, 2024 }));
        }
    }

    [Test]
    public async Task Export_AsOwner_ReturnsSelectedNativeWorkbookWithFilteredTenantDataAndCounts()
    {
        var (tenantId, otherTenantId) = await SeedDataAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Transactions.Where(item => item.TenantId == tenantId)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.Date, new DateOnly(2024, 1, 1)));
        await db.Transactions.Where(item => item.TenantId == otherTenantId)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.Date, new DateOnly(2025, 3, 1)));
        var selectedTransaction = new Transaction
        {
            TenantId = tenantId,
            Date = new DateOnly(2025, 6, 15),
            Payee = "Selected target transaction",
            Amount = 25m,
            Splits = [new Split { Amount = 25m, Category = "Selected category", Order = 0 }]
        };
        db.Transactions.Add(selectedTransaction);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync(
            $"{ExportUrl}?types=Transactions&types=PayeeMatchingRules&startDate=2025-01-01&endDate=2025-12-31");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        Assert.That(response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName, Does.Contain("yofi-"));
        Assert.That(response.Headers.TryGetValues("X-YoFi-Export-Counts", out var countHeaders), Is.True);

        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        var counts = JsonSerializer.Deserialize<TenantDataExportCountDto[]>(countHeaders!.Single());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(workbook.Worksheets.Select(sheet => sheet.Name),
                Is.EqualTo(new[] { "Metadata", "PayeeMatchingRules", "Transactions", "Splits" }));
            Assert.That(workbook.Worksheet("Metadata").Cell(2, 2).GetString(), Is.EqualTo("YoFi.V3"));
            Assert.That(workbook.Worksheet("Transactions").LastRowUsed()!.RowNumber(), Is.EqualTo(2));
            Assert.That(workbook.Worksheet("Transactions").Cell(2, 1).GetString(),
                Is.EqualTo(selectedTransaction.Key.ToString("D")));
            Assert.That(workbook.Worksheet("Transactions").Cell(2, 8).GetString(), Is.EqualTo("Selected category"));
            Assert.That(workbook.Worksheet("Splits").LastRowUsed()!.RowNumber(), Is.EqualTo(1));
            Assert.That(workbook.Worksheet("PayeeMatchingRules").LastRowUsed()!.RowNumber(), Is.EqualTo(2));
            Assert.That(counts, Is.EqualTo(new[]
            {
                new TenantDataExportCountDto("PayeeMatchingRules", 1),
                new TenantDataExportCountDto("Transactions", 1),
                new TenantDataExportCountDto("Splits", 0)
            }));
        }
    }

    [Test]
    public async Task Export_AllData_IgnoresSelectionAndYearFilter()
    {
        var (tenantId, _) = await SeedDataAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transactions = await db.Transactions.Where(item => item.TenantId == tenantId).ToListAsync();
        transactions[0].Date = new DateOnly(2024, 1, 1);
        db.Transactions.Add(new Transaction
        {
            TenantId = tenantId,
            Date = new DateOnly(2025, 1, 1),
            Payee = "Second target transaction"
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync(
            $"{ExportUrl}?allData=true&startDate=1900-01-01&endDate=1900-12-31");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName, Does.Contain("yofi-complete-export-"));
            Assert.That(workbook.Worksheets.Select(sheet => sheet.Name),
                Is.EqualTo(new[] { "Metadata", "PayeeMatchingRules", "Transactions", "Splits" }));
            Assert.That(workbook.Worksheet("Transactions").LastRowUsed()!.RowNumber(), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Export_AllAvailableTypesWithoutDateFilter_UsesCompleteExportFileName()
    {
        await SeedDataAsync();

        var response = await _client.GetAsync(
            $"{ExportUrl}?types=PayeeMatchingRules&types=Transactions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(
            response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName,
            Does.Contain("yofi-complete-export-"));
    }

    [Test]
    public async Task Export_WithUnknownType_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync($"{ExportUrl}?types=Unknown");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task Export_AsEditor_ReturnsForbidden()
    {
        SwitchToEditor();

        var response = await _client.GetAsync(ExportOptionsUrl);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task OpenApi_DescribesExportAsBinaryAttachment()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var binarySchema = document.RootElement.GetProperty("paths")
            .GetProperty("/api/tenant/{tenantKey}/admin/export")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .GetProperty("schema");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(binarySchema.GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(binarySchema.GetProperty("format").GetString(), Is.EqualTo("binary"));
        }
    }

    [TestCase("/api/tenant/{tenantKey}/admin/import/preview")]
    [TestCase("/api/tenant/{tenantKey}/admin/import")]
    public async Task OpenApi_DescribesWorkbookAsBinaryMultipartUpload(string path)
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schema = document.RootElement.GetProperty("paths").GetProperty(path)
            .GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("multipart/form-data").GetProperty("schema");
        if (schema.TryGetProperty("$ref", out var schemaReference))
        {
            var schemaName = schemaReference.GetString()!.Split('/').Last();
            schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        }
        var properties = new Dictionary<string, JsonElement>();
        CollectProperties(schema);
        var file = properties["file"];
        if (file.TryGetProperty("$ref", out var fileReference))
        {
            var schemaName = fileReference.GetString()!.Split('/').Last();
            file = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        }

        Assert.That(file.TryGetProperty("type", out var fileType), Is.True, file.GetRawText());
        Assert.That(fileType.GetString(), Is.EqualTo("string"));
        Assert.That(file.TryGetProperty("format", out var fileFormat), Is.True, file.GetRawText());
        Assert.That(fileFormat.GetString(), Is.EqualTo("binary"));
        if (path.EndsWith("/import", StringComparison.Ordinal))
        {
            Assert.That(properties["expectedSha256"].GetProperty("type").GetString(), Is.EqualTo("string"));
        }

        void CollectProperties(JsonElement current)
        {
            if (current.TryGetProperty("properties", out var currentProperties))
            {
                foreach (var property in currentProperties.EnumerateObject())
                {
                    properties[property.Name] = property.Value;
                }
            }

            if (current.TryGetProperty("allOf", out var allOf))
            {
                foreach (var item in allOf.EnumerateArray())
                {
                    CollectProperties(item);
                }
            }
        }
    }

    [Test]
    public async Task PreviewImport_AsOwner_ReturnsDetectedFormatAndPreviewDigest()
    {
        var workbook = CreatePayeeWorkbook(["Name", "Category"], ["Coffee Shop", "Food"]);
        using var content = CreateImportForm(workbook);

        var response = await _client.PostAsync(ImportPreviewUrl, content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var preview = await response.Content.ReadFromJsonAsync<TenantDataImportPreviewDto>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview!.FormatId, Is.EqualTo("original-yofi"));
            Assert.That(preview.CanImport, Is.True);
            Assert.That(preview.Sha256, Has.Length.EqualTo(64));
            Assert.That(preview.Worksheets.Single().ValidRecordCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_AsOwner_PersistsPreviewedWorkbook()
    {
        var workbook = CreatePayeeWorkbook(["Name", "Category"], ["Coffee Shop", "Food"]);
        using var previewContent = CreateImportForm(workbook);
        var previewResponse = await _client.PostAsync(ImportPreviewUrl, previewContent);
        var preview = await previewResponse.Content.ReadFromJsonAsync<TenantDataImportPreviewDto>();

        using var importContent = CreateImportForm(workbook, preview!.Sha256);
        var response = await _client.PostAsync(ImportUrl, importContent);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<TenantDataImportResultDto>();
        Assert.That(result!.Worksheets.Single().Created, Is.EqualTo(1));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.Tenants.SingleAsync(item => item.Key == _testTenantKey);
        var rule = await db.PayeeMatchingRules.SingleAsync(item =>
            item.TenantId == tenant.Id && item.PayeePattern == "Coffee Shop");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rule.PayeePattern, Is.EqualTo("Coffee Shop"));
            Assert.That(rule.Category, Is.EqualTo("Food"));
        }
    }

    [Test]
    public async Task PreviewWithWorksheetErrorsSucceedsAndImportFailureReturnsProblemDetailsWithoutPreview()
    {
        var workbook = CreatePayeeWorkbook(["Name", "Category"], ["", "Food"]);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.Tenants.SingleAsync(item => item.Key == _testTenantKey);
        var initialRuleCount = await db.PayeeMatchingRules.CountAsync(item => item.TenantId == tenant.Id);
        using var previewContent = CreateImportForm(workbook);

        var previewResponse = await _client.PostAsync(ImportPreviewUrl, previewContent);

        Assert.That(previewResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var preview = await previewResponse.Content.ReadFromJsonAsync<TenantDataImportPreviewDto>();
        Assert.That(preview!.CanImport, Is.False);
        Assert.That(preview.Worksheets.Single().Errors, Is.Not.Empty);

        using var importContent = CreateImportForm(workbook, preview.Sha256);
        var importResponse = await _client.PostAsync(ImportUrl, importContent);

        Assert.That(importResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(importResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using var document = JsonDocument.Parse(await importResponse.Content.ReadAsStringAsync());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(document.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(400));
            Assert.That(document.RootElement.TryGetProperty("preview", out _), Is.False);
            Assert.That(document.RootElement.GetProperty("title").GetString(), Is.EqualTo("Import could not be completed."));
            var detail = document.RootElement.GetProperty("detail").GetString();
            Assert.That(detail, Does.Contain("Review the workbook preview for validation details"));
            Assert.That(document.RootElement.GetProperty("errors").GetProperty("file")[0].GetString(),
                Is.EqualTo(detail));
            Assert.That(document.RootElement.GetProperty("traceId").GetString(), Is.Not.Null.And.Not.Empty);
        }

        Assert.That(await db.PayeeMatchingRules.CountAsync(item => item.TenantId == tenant.Id),
            Is.EqualTo(initialRuleCount));
    }

    [Test]
    public async Task PreviewImport_WithoutFile_Returns400ProblemDetails()
    {
        using var content = new MultipartFormDataContent();

        var response = await _client.PostAsync(ImportPreviewUrl, content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
    }

    [Test]
    public async Task Import_WithoutPreviewDigest_Returns400ProblemDetails()
    {
        using var content = CreateImportForm(CreatePayeeWorkbook(["Name", "Category"], ["Coffee Shop", "Food"]));

        var response = await _client.PostAsync(ImportUrl, content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task PreviewImport_Unauthenticated_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();
        using var content = CreateImportForm(CreatePayeeWorkbook(["Name", "Category"], ["Coffee Shop", "Food"]));

        var response = await client.PostAsync(ImportPreviewUrl, content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task PreviewImport_MalformedTenantKey_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        using var content = CreateImportForm(CreatePayeeWorkbook(["Name", "Category"], ["Coffee Shop", "Food"]));

        var response = await client.PostAsync("/api/tenant/not-a-guid/admin/import/preview", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Import_AsEditor_ReturnsForbidden()
    {
        SwitchToEditor();
        using var content = CreateImportForm(
            CreatePayeeWorkbook(["Name", "Category"], ["Coffee Shop", "Food"]),
            new string('A', 64));

        var response = await _client.PostAsync(ImportUrl, content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task PreviewImport_AsEditor_ReturnsForbidden()
    {
        SwitchToEditor();
        using var content = CreateImportForm(CreatePayeeWorkbook(["Name", "Category"], ["Coffee Shop", "Food"]));

        var response = await _client.PostAsync(ImportPreviewUrl, content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    private async Task<(long TenantId, long OtherTenantId)> SeedDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.Tenants.SingleAsync(item => item.Key == _testTenantKey);

        var transactionIds = db.Transactions.Where(item => item.TenantId == tenant.Id).Select(item => item.Id);
        await db.Splits.Where(item => transactionIds.Contains(item.TransactionId)).ExecuteDeleteAsync();
        await db.Transactions.Where(item => item.TenantId == tenant.Id).ExecuteDeleteAsync();
        await db.PayeeMatchingRules.Where(item => item.TenantId == tenant.Id).ExecuteDeleteAsync();
        await db.ImportReviewTransactions.Where(item => item.TenantId == tenant.Id).ExecuteDeleteAsync();

        var otherTenant = new Tenant { Name = "Other Tenant", Description = "Untouched tenant" };
        db.Tenants.Add(otherTenant);
        await db.SaveChangesAsync();

        db.Transactions.AddRange(
            new Transaction
            {
                TenantId = tenant.Id,
                Payee = "Target transaction",
                Splits = [new Split { Category = "Target category", Amount = -10 }]
            },
            new Transaction { TenantId = otherTenant.Id, Payee = "Other transaction" });
        db.PayeeMatchingRules.AddRange(
            new PayeeMatchingRule { TenantId = tenant.Id, PayeePattern = "Target", Category = "Target" },
            new PayeeMatchingRule { TenantId = otherTenant.Id, PayeePattern = "Other", Category = "Other" });
        db.ImportReviewTransactions.AddRange(
            new ImportReviewTransaction { TenantId = tenant.Id, Payee = "Target pending import" },
            new ImportReviewTransaction { TenantId = otherTenant.Id, Payee = "Other pending import" });
        if (!await db.UserTenantRoleAssignments.AnyAsync(item =>
                item.TenantId == tenant.Id && item.UserId == "test-user-id"))
        {
            db.UserTenantRoleAssignments.Add(new UserTenantRoleAssignment
            {
                UserId = "test-user-id",
                TenantId = tenant.Id,
                Role = TenantRole.Owner
            });
        }
        await db.SaveChangesAsync();
        return (tenant.Id, otherTenant.Id);
    }

    private HttpRequestMessage DeleteRequest(string confirmationName)
        => new(HttpMethod.Delete, DataUrl)
        {
            Content = JsonContent.Create(new TenantDataDeletionRequest(confirmationName))
        };

    private static MultipartFormDataContent CreateImportForm(byte[] workbook, string? expectedSha256 = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(workbook), "file", "payees.xlsx");
        if (expectedSha256 is not null)
        {
            content.Add(new StringContent(expectedSha256), "expectedSha256");
        }

        return content;
    }

    private static byte[] CreatePayeeWorkbook(string[] headers, params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Payee");
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
        }

        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                worksheet.Cell(row + 2, column + 1).Value = rows[row][column];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
