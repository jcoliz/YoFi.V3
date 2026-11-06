using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Data;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>Tests sample-data HTTP contracts, tenant authorization, and production DI wiring.</summary>
[TestFixture]
public class SampleDataControllerTests : AuthenticatedTestBase
{
    protected override TenantRole DefaultRole => TenantRole.Owner;
    private string BaseUrl => $"/api/tenant/{_testTenantKey}/admin/sample-data";
    private static SampleDataRequest Request => new("personal-finance", 3, new(2026, 1, 1), new(2026, 1, 31));

    [SetUp]
    public void SetUp() => SwitchToOwner();

    [Test]
    public async Task Catalog_AsOwner_ReturnsScenarioRevisionAndEntityTypes()
    {
        var response = await _client.GetAsync($"{BaseUrl}/scenarios");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var catalog = await response.Content.ReadFromJsonAsync<SampleScenarioDto[]>();
        var scenario = catalog!.Single(s => s.Id == "personal-finance");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(scenario.Revision, Is.EqualTo(3));
            Assert.That(scenario.Name, Is.EqualTo("Personal Finance"));
            Assert.That(scenario.EntityTypes, Is.EqualTo(new[] { "Transactions", "PayeeMatchingRules" }));
        }
    }

    [Test]
    public async Task PreviewAndLoad_AsOwner_ReturnMatchingInputsAndCommittedCounts()
    {
        var previewResponse = await _client.PostAsJsonAsync($"{BaseUrl}/preview", Request);
        Assert.That(previewResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var preview = await previewResponse.Content.ReadFromJsonAsync<SampleDataPreviewDto>();
        Assert.That(preview!.Inputs, Is.EqualTo(Request));
        Assert.That(preview.Counts.Transactions, Is.Positive);
        Assert.That(preview.Counts.PayeeMatchingRules, Is.Positive);

        var loadResponse = await _client.PostAsJsonAsync($"{BaseUrl}/load", Request);
        Assert.That(loadResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loaded = await loadResponse.Content.ReadFromJsonAsync<SampleDataLoadResultDto>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded!.Inputs, Is.EqualTo(Request));
            Assert.That(loaded.Created, Is.EqualTo(preview.Counts));
            Assert.That(loaded.Updated.Total, Is.Zero);
        }
        var refreshedResponse = await _client.PostAsJsonAsync($"{BaseUrl}/preview", Request);
        var refreshed = await refreshedResponse.Content.ReadFromJsonAsync<SampleDataPreviewDto>();
        Assert.That(refreshed!.ExistingTransactionCount, Is.EqualTo(preview.ExistingTransactionCount + loaded!.Created.Transactions));
    }

    [Test]
    public async Task Ofx_AsOwner_ReturnsNamedParseableAttachmentAndPreservesDateOnlyInputs()
    {
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/ofx", Request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/x-ofx"));
        Assert.That(response.Content.Headers.ContentDisposition!.DispositionType, Is.EqualTo("attachment"));
        var filename = response.Content.Headers.ContentDisposition.FileNameStar
            ?? response.Content.Headers.ContentDisposition.FileName?.Trim('"');
        Assert.That(filename, Is.EqualTo("personal-finance-2026-01-01-2026-01-31.ofx"));
        using var stream = await response.Content.ReadAsStreamAsync();
        var parsed = await OfxParsingHelper.ParseAsync(stream, filename!);
        Assert.That(parsed.Errors.Select(e => e.Message), Is.Empty);
        Assert.That(parsed.Transactions, Is.Not.Empty);
        Assert.That(parsed.Transactions.Select(t => t.Date), Is.All.InRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
    }

    [TestCase("scenarios", "editor")]
    [TestCase("preview", "editor")]
    [TestCase("load", "editor")]
    [TestCase("ofx", "editor")]
    [TestCase("scenarios", "viewer")]
    [TestCase("preview", "viewer")]
    [TestCase("load", "viewer")]
    [TestCase("ofx", "viewer")]
    [TestCase("scenarios", "nonmember")]
    [TestCase("preview", "nonmember")]
    [TestCase("load", "nonmember")]
    [TestCase("ofx", "nonmember")]
    [TestCase("scenarios", "anonymous")]
    [TestCase("preview", "anonymous")]
    [TestCase("load", "anonymous")]
    [TestCase("ofx", "anonymous")]
    public async Task EveryEndpoint_RejectsUsersWithoutWorkspaceOwnerAccess(string endpoint, string role)
    {
        using var client = role switch
        {
            "editor" => _factory.CreateAuthenticatedClient(_testTenantKey, TenantRole.Editor),
            "viewer" => _factory.CreateAuthenticatedClient(_testTenantKey, TenantRole.Viewer),
            "nonmember" => _factory.CreateAuthenticatedClient(Guid.NewGuid(), TenantRole.Owner),
            _ => _factory.CreateClient()
        };
        var response = endpoint is "preview" or "load" or "ofx"
            ? await client.PostAsJsonAsync($"{BaseUrl}/{endpoint}", Request)
            : await client.GetAsync($"{BaseUrl}/{endpoint}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem!.Status, Is.EqualTo(403));
    }

    [TestCase("preview")]
    [TestCase("load")]
    [TestCase("ofx")]
    public async Task MissingDates_Return400ProblemDetailsWithoutDefaults(string endpoint)
    {
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/{endpoint}", new { scenarioId = "personal-finance", revision = 2 });
        await AssertBadRequest(response);
    }

    [TestCase("{}")]
    [TestCase("{\"scenarioId\":\"personal-finance\",\"revision\":1}")]
    [TestCase("{\"scenarioId\":\"personal-finance\",\"revision\":1,\"startDate\":\"2026-01-01\"}")]
    [TestCase("{\"scenarioId\":\"personal-finance\",\"revision\":1,\"startDate\":\"2026-01-01\",\"endDate\":\"not-a-date\"}")]
    [TestCase("{\"scenarioId\":\"personal-finance\",\"revision\":1,\"startDate\":\"2026-01-01T08:00:00.000Z\",\"endDate\":\"2026-01-31\"}")]
    public async Task MissingOrMalformedOfxDates_Return400ProblemDetails(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"{BaseUrl}/ofx", content);
        await AssertBadRequest(response);
    }

    [TestCase("preview", "missing", 1, "2026-01-01", "2026-01-31")]
    [TestCase("load", "missing", 1, "2026-01-01", "2026-01-31")]
    [TestCase("ofx", "missing", 1, "2026-01-01", "2026-01-31")]
    [TestCase("preview", "personal-finance", 999, "2026-01-01", "2026-01-31")]
    [TestCase("load", "personal-finance", 999, "2026-01-01", "2026-01-31")]
    [TestCase("ofx", "personal-finance", 999, "2026-01-01", "2026-01-31")]
    [TestCase("preview", "personal-finance", 1, "2026-02-01", "2026-01-31")]
    [TestCase("load", "personal-finance", 1, "2026-02-01", "2026-01-31")]
    [TestCase("ofx", "personal-finance", 1, "2026-02-01", "2026-01-31")]
    [TestCase("preview", "personal-finance", 1, "2026-01-01", "2027-01-01")]
    [TestCase("load", "personal-finance", 1, "2026-01-01", "2027-01-01")]
    [TestCase("ofx", "personal-finance", 1, "2026-01-01", "2027-01-01")]
    public async Task InvalidGenerationInputs_Return400ProblemDetails(string endpoint, string scenario, int revision, string start, string end)
    {
        var request = new SampleDataRequest(scenario, revision, DateOnly.Parse(start), DateOnly.Parse(end));
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/{endpoint}", request);
        await AssertBadRequest(response);
    }

    [TestCase("preview", "null")]
    [TestCase("load", "null")]
    [TestCase("ofx", "null")]
    [TestCase("preview", "{\"scenarioId\":\"personal-finance\",\"revision\":1,\"startDate\":\"invalid\",\"endDate\":\"2026-01-31\"}")]
    [TestCase("load", "{\"scenarioId\":\"personal-finance\",\"revision\":1,\"startDate\":\"invalid\",\"endDate\":\"2026-01-31\"}")]
    public async Task MalformedBody_Returns400ProblemDetails(string endpoint, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"{BaseUrl}/{endpoint}", content);
        await AssertBadRequest(response);
    }

    [Test]
    public async Task Load_TargetsRouteWorkspaceEvenWhenAnotherWorkspaceAppearsInPayload()
    {
        var otherKey = await CreateTestTenantAsync();
        using var client = CreateMultiTenantClient((_testTenantKey, TenantRole.Owner), (otherKey, TenantRole.Owner));
        var beforeResponse = await client.PostAsJsonAsync($"{BaseUrl}/preview", Request);
        var before = await beforeResponse.Content.ReadFromJsonAsync<SampleDataPreviewDto>();
        var response = await client.PostAsJsonAsync($"/api/tenant/{otherKey}/admin/sample-data/load", new
        {
            Request.ScenarioId, Request.Revision, Request.StartDate, Request.EndDate, tenantKey = _testTenantKey
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loaded = await response.Content.ReadFromJsonAsync<SampleDataLoadResultDto>();
        var previewResponse = await client.PostAsJsonAsync($"/api/tenant/{otherKey}/admin/sample-data/preview", Request);
        var preview = await previewResponse.Content.ReadFromJsonAsync<SampleDataPreviewDto>();
        Assert.That(preview!.ExistingTransactionCount, Is.EqualTo(loaded!.Created.Transactions));
        var afterResponse = await client.PostAsJsonAsync($"{BaseUrl}/preview", Request);
        var after = await afterResponse.Content.ReadFromJsonAsync<SampleDataPreviewDto>();
        Assert.That(after!.ExistingTransactionCount, Is.EqualTo(before!.ExistingTransactionCount));
    }

    [Test]
    public async Task MissingWorkspace_Returns403RatherThanFallingBack()
    {
        var missing = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedClient(missing, TenantRole.Owner);
        var response = await client.GetAsync($"/api/tenant/{missing}/admin/sample-data/scenarios");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task InvalidWorkspaceKey_Returns404()
    {
        var response = await _client.GetAsync("/api/tenant/not-a-guid/admin/sample-data/scenarios");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task OpenApi_DescribesAllEndpointsAndOfxResponse()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        const string prefix = "/api/tenant/{tenantKey}/admin/sample-data";
        foreach (var (suffix, method) in new[] { ("scenarios", "get"), ("preview", "post"), ("load", "post"), ("ofx", "post") })
        {
            var operation = paths.GetProperty($"{prefix}/{suffix}").GetProperty(method);
            Assert.That(operation.GetProperty("responses").TryGetProperty("200", out _), Is.True);
            Assert.That(operation.TryGetProperty("security", out _), Is.True);
        }
        var content = paths.GetProperty($"{prefix}/ofx").GetProperty("post").GetProperty("responses").GetProperty("200").GetProperty("content");
        Assert.That(content.TryGetProperty("application/x-ofx", out _), Is.True, content.GetRawText());
        Assert.That(content.GetProperty("application/x-ofx").GetProperty("schema").GetProperty("format").GetString(), Is.EqualTo("binary"));
    }

    [TestCase("LogStarting")]
    [TestCase("LogOk")]
    [TestCase("LogFailed")]
    [TestCase("LogOkDownload")]
    public void LogMethods_CaptureCallerMemberNameAsLocation(string methodName)
    {
        var methods = typeof(Controllers.SampleDataController).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(m => m.Name == methodName).ToArray();
        Assert.That(methods, Is.Not.Empty);
        foreach (var method in methods)
        {
            var location = method.GetParameters().Single(p => p.Name == "location");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(location.GetCustomAttribute<CallerMemberNameAttribute>(), Is.Not.Null);
                Assert.That(location.HasDefaultValue, Is.True);
                Assert.That(method.GetCustomAttribute<LoggerMessageAttribute>()!.Message, Does.StartWith("{Location}:"));
            }
        }
    }

    [Test]
    public async Task OperationLogs_IncludeInputsCountsAndFailureOutcomeButNoFinancialRecordsOrIdentity()
    {
        using var logs = new CapturingLoggerProvider();
        _factory.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/load", Request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loaded = await response.Content.ReadFromJsonAsync<SampleDataLoadResultDto>();
        var failure = await _client.PostAsJsonAsync($"{BaseUrl}/preview", Request with { Revision = 999 });
        await AssertBadRequest(failure);
        var entries = logs.Entries.Where(e => e.Category == typeof(Controllers.SampleDataController).FullName).ToArray();
        var success = entries.Single(e => e.EventId.Id == 2);
        var failed = entries.Single(e => e.EventId.Id == 3);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success.Message, Does.Contain("Load: OK")
                .And.Contain("Scenario=personal-finance")
                .And.Contain("Revision=3").And.Contain($"Transactions={loaded!.Created.Transactions}")
                .And.Contain($"Rules={loaded.Created.PayeeMatchingRules}"));
            Assert.That(failed.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(failed.Message, Does.Contain("Preview: Failed").And.Contain("Revision=999"));
            Assert.That(entries.Any(e => e.Message.Contains("Megacorp", StringComparison.Ordinal)), Is.False);
            Assert.That(entries.Any(e => e.Message.Contains(_testTenantKey.ToString(), StringComparison.Ordinal)), Is.False);
            Assert.That(entries.Any(e => e.Message.Contains("Actor=", StringComparison.Ordinal)
                || e.Message.Contains("test-user-id", StringComparison.Ordinal)), Is.False);
        }
    }

    [Test]
    public async Task FailedPersistence_Returns500ProblemDetailsAndLogsFailedLoad()
    {
        using var logs = new CapturingLoggerProvider();
        _factory.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_controller_samples BEFORE INSERT ON "YoFi.V3.Transactions"
            BEGIN
                SELECT RAISE(ABORT, 'Injected controller persistence failure');
            END;
            """);
        try
        {
            var response = await _client.PostAsJsonAsync($"{BaseUrl}/load", Request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That((await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status, Is.EqualTo(500));
            var entries = logs.Entries.Where(e => e.Category == typeof(Controllers.SampleDataController).FullName).ToArray();
            Assert.That(entries.Count(e => e.EventId.Id == 3 && e.Message.Contains("Load", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(entries.Any(e => e.EventId.Id == 2), Is.False);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_controller_samples;");
        }
    }

    [Test]
    public async Task Production_AllowsSampleLoadingThroughExistingDiAndAuthorization()
    {
        using var factory = new BaseTestWebApplicationFactory(environment: "Production");
        var key = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Tenants.Add(new Tenant { Key = key, Name = "Production samples", Description = "Controller test" });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateAuthenticatedClient(key, TenantRole.Owner);
        var response = await client.PostAsJsonAsync($"/api/tenant/{key}/admin/sample-data/load", Request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await response.Content.ReadFromJsonAsync<SampleDataLoadResultDto>())!.Created.Transactions, Is.Positive);
    }

    private static async Task AssertBadRequest(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(problem!.Status, Is.EqualTo(400));
            Assert.That(problem.Detail, Is.Not.Empty);
            Assert.That(problem.Extensions.ContainsKey("traceId"), Is.True);
        }
    }
}
