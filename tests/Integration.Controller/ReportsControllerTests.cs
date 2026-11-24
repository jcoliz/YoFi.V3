using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
public class ReportsControllerTests : AuthenticatedTestBase
{
    [TestCase(TenantRole.Viewer)]
    [TestCase(TenantRole.Editor)]
    [TestCase(TenantRole.Owner)]
    public async Task GetAvailableYears_WithTenantAccess_ReturnsYearDiscoveryContract(TenantRole role)
    {
        var tenantKey = await CreateTestTenantAsync("Report years contract");
        using var client = CreateMultiTenantClient((tenantKey, role));
        var yearBeforeRequest = DateTimeOffset.UtcNow.Year;

        var response = await client.GetAsync($"/api/tenant/{tenantKey}/reports/years");
        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var result = JsonSerializer.Deserialize<ReportYearsDto>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
            Assert.That(document.RootElement.EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[] { "years", "defaultYear" }));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.DefaultYear, Is.InRange(yearBeforeRequest, DateTimeOffset.UtcNow.Year));
            Assert.That(result.Years, Is.EqualTo(new[] { result.DefaultYear }));
        }
    }

    [Test]
    public async Task GetAvailableYears_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new BaseTestWebApplicationFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/tenant/{_testTenantKey}/reports/years");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetAvailableYears_WithoutTenantAccess_ReturnsForbidden()
    {
        var unrelatedTenantKey = await CreateTestTenantAsync("Unrelated years tenant");
        using var client = CreateMultiTenantClient((unrelatedTenantKey, TenantRole.Viewer));

        var response = await client.GetAsync($"/api/tenant/{_testTenantKey}/reports/years");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetAvailableReports_AsViewer_ReturnsBuiltInCatalog()
    {
        SwitchToViewer();

        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/reports/available");
        var reports = await response.Content.ReadFromJsonAsync<ReportDefinitionDto[]>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reports, Has.Length.EqualTo(11));
            Assert.That(reports!.Select(report => report.Id), Does.Contain("income"));
            Assert.That(reports!.Select(report => report.Id), Does.Contain("all-detail"));
        }
    }

    [Test]
    public async Task GetReport_AsViewer_ReturnsTypedReportForRequestedYear()
    {
        SwitchToViewer();
        await AddIncomeSplitAsync(2034, "Controller Test Income", 125.50m);

        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/reports/income-detail?year=2034");
        var report = await response.Content.ReadFromJsonAsync<ReportResultDto>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(report, Is.Not.Null);
            Assert.That(report!.Id, Is.EqualTo("income-detail"));
            Assert.That(report.CategoryRoot, Is.EqualTo("Income"));
            Assert.That(report.Year, Is.EqualTo(2034));
            Assert.That(report.HasMonthColumns, Is.True);
            Assert.That(report.Rows.Single(row => row.CategoryPath == "Income:Controller Test Income").AnnualTotal,
                Is.EqualTo(125.50m));
            Assert.That(report.Rows.Single(row => row.CategoryPath == "Income:Controller Test Income").CategoryName,
                Is.EqualTo("Controller Test Income"));
            Assert.That(report.Totals.AnnualTotal, Is.EqualTo(125.50m));
        }
    }

    [Test]
    public async Task GetReport_AsViewer_AppliesDisplayOverrides()
    {
        var tenantKey = await CreateTestTenantAsync("Report display override tenant");
        using var client = CreateMultiTenantClient((tenantKey, TenantRole.Viewer));
        await AddIncomeSplitAsync(2034, "Controller Override", 125.50m, tenantKey);

        // Given: A Viewer can read reports and data exists in the selected tenant
        // When: Requesting effective depth and annual-only display settings
        var response = await client.GetAsync(
            $"/api/tenant/{tenantKey}/reports/income-detail?year=2034&displayDepth=1&showMonths=false");
        var report = await response.Content.ReadFromJsonAsync<ReportResultDto>();

        // Then: The controller forwards both overrides and the effective response is returned
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(report, Is.Not.Null);
            Assert.That(report!.DisplayDepth, Is.EqualTo(1));
            Assert.That(report.HasMonthColumns, Is.False);
            Assert.That(report.Rows.Select(row => row.CategoryPath),
                Is.EqualTo(new[] { "Income:Controller Override" }));
            Assert.That(report.Rows.All(row => row.MonthlyAmounts is null), Is.True);
            Assert.That(report.Totals.MonthlyAmounts, Is.Null);
            Assert.That(report.Totals.AnnualTotal, Is.EqualTo(125.50m));
        }
    }

    [Test]
    public async Task GetReport_DisplayDepthBelowOne_Returns400ProblemDetails()
    {
        SwitchToViewer();

        // Given: A valid report request
        // When: Supplying an invalid display depth
        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/reports/all?displayDepth=0");
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        // Then: Validation is returned through the application's existing ProblemDetails mapping
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Errors, Does.ContainKey("displayDepth"));
        }
    }

    [TestCase("displayDepth=not-a-depth")]
    [TestCase("showMonths=not-a-bool")]
    public async Task GetReport_NonNumericDisplayParameter_Returns400ProblemDetails(string query)
    {
        SwitchToViewer();

        // Given: A valid report route
        // When: Model binding cannot convert the display parameter
        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/reports/all?{query}");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        // Then: MVC model-binding validation returns 400 with the parameter in the errors
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Errors, Is.Not.Empty);
            Assert.That(problem.Detail, Is.Null);
            Assert.That(problem.Extensions.ContainsKey("traceId"), Is.True);
        }
    }

    [Test]
    public async Task GetReport_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new BaseTestWebApplicationFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();

        // Given: An unauthenticated request
        // When: Reading a report
        var response = await client.GetAsync($"/api/tenant/{_testTenantKey}/reports/all");

        // Then: Authentication rejects the request
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetReport_WithoutTenantAccess_ReturnsForbidden()
    {
        var unrelatedTenantKey = await CreateTestTenantAsync("Unrelated report tenant");
        using var client = CreateMultiTenantClient((unrelatedTenantKey, TenantRole.Viewer));

        // Given: A viewer with access only to another tenant
        // When: Reading the test tenant's report
        var response = await client.GetAsync($"/api/tenant/{_testTenantKey}/reports/all");

        // Then: Tenant authorization returns forbidden
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetAvailableReports_WithoutTenantAccess_ReturnsForbidden()
    {
        var unrelatedTenantKey = await CreateTestTenantAsync("Unrelated tenant");
        using var client = CreateMultiTenantClient((unrelatedTenantKey, TenantRole.Viewer));

        var response = await client.GetAsync($"/api/tenant/{_testTenantKey}/reports/available");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetReport_UnknownReportId_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/reports/not-a-built-in-report");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo((int)HttpStatusCode.NotFound));
        }
    }

    [Test]
    public async Task GetReport_YearOutsideCalendarRange_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/reports/all?year=10000");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo((int)HttpStatusCode.BadRequest));
        }
    }

    [Test]
    public async Task GetReport_NonNumericYear_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/reports/all?year=not-a-year");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Errors, Does.ContainKey("year"));
        }
    }

    private async Task AddIncomeSplitAsync(
        int year,
        string category,
        decimal amount,
        Guid? tenantKey = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await dbContext.Tenants.SingleAsync(item => item.Key == (tenantKey ?? _testTenantKey));
        dbContext.Transactions.Add(new Transaction
        {
            TenantId = tenant.Id,
            Date = new DateOnly(year, 6, 15),
            Payee = "Report controller integration test",
            Amount = amount,
            Splits =
            [
                new Split { Amount = amount, Category = $"Income:{category}", Order = 0 }
            ]
        });
        await dbContext.SaveChangesAsync();
    }
}
