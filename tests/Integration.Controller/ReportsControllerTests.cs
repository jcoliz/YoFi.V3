using System.Net;
using System.Net.Http.Json;
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

    private async Task AddIncomeSplitAsync(int year, string category, decimal amount)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await dbContext.Tenants.SingleAsync(item => item.Key == _testTenantKey);
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
