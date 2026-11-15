using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Controllers.Middleware;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TransactionSplitUpdateControllerTests : AuthenticatedTestBase
{
    [TestCase(TenantRole.Editor, 0, -70, -70, -30)]
    [TestCase(TenantRole.Owner, 1, -50, -50, -50)]
    public async Task UpdateSplit_AsWriter_ReturnsAndPersistsBalancedDetail(
        TenantRole role, int editedIndex, int amount, int expectedFirst, int expectedSecond)
    {
        SwitchToRole(role);
        var original = await CreateSplitTransactionAsync();

        var response = await _client.PutAsJsonAsync(
            SplitUrl(original.Key, original.Splits[editedIndex].Key),
            new SplitEditDto(amount, "  home : supplies ", "Edited memo"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Amount, Is.EqualTo(original.Amount));
        Assert.That(updated.Splits.Select(split => split.Amount), Is.EqualTo(new[] { expectedFirst, expectedSecond }));
        Assert.That(updated.Splits.Select(split => split.Key), Is.EqualTo(original.Splits.Select(split => split.Key)));
        Assert.That(updated.Splits[editedIndex].Category, Is.EqualTo("Home:Supplies"));
        Assert.That(updated.Splits[editedIndex].Memo, Is.EqualTo("Edited memo"));

        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(
            $"/api/tenant/{_testTenantKey}/transactions/{original.Key}");
        Assert.That(persisted!.Splits, Is.EqualTo(updated.Splits));
    }

    [Test]
    public async Task UpdateSplit_AsViewer_ReturnsForbidden()
    {
        SwitchToViewer();
        var response = await _client.PutAsJsonAsync(SplitUrl(Guid.NewGuid(), Guid.NewGuid()),
            new SplitEditDto(0m, null, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateSplit_Unauthenticated_ReturnsUnauthorized()
    {
        using var factory = new BaseTestWebApplicationFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        var response = await client.PutAsJsonAsync(SplitUrl(Guid.NewGuid(), Guid.NewGuid()),
            new SplitEditDto(0m, null, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UpdateSplit_WithoutTenantAccess_ReturnsForbidden()
    {
        var otherTenantKey = await CreateTestTenantAsync("Other workspace");
        using var client = _factory.CreateAuthenticatedClient(otherTenantKey);
        var response = await client.PutAsJsonAsync(SplitUrl(Guid.NewGuid(), Guid.NewGuid()),
            new SplitEditDto(0m, null, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase("Amount")]
    [TestCase("Category")]
    [TestCase("Memo")]
    [TestCase("SubCentAmount")]
    public async Task UpdateSplit_WithInvalidDto_ReturnsValidationProblemWithoutChanges(string field)
    {
        var original = await CreateSplitTransactionAsync();
        var dto = field switch
        {
            "Amount" => new SplitEditDto(1_000_000_000m, null, null),
            "Category" => new SplitEditDto(-40m, new string('x', 201), null),
            "Memo" => new SplitEditDto(-40m, null, new string('x', 501)),
            "SubCentAmount" => new SplitEditDto(-40.005m, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var response = await _client.PutAsJsonAsync(SplitUrl(original.Key, original.Splits[1].Key), dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Errors, Does.ContainKey(field == "SubCentAmount" ? "Amount" : field));
        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(
            $"/api/tenant/{_testTenantKey}/transactions/{original.Key}");
        Assert.That(persisted!.Splits, Is.EqualTo(original.Splits));
    }

    [Test]
    public async Task UpdateSplit_WithMissingTransaction_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync(SplitUrl(Guid.NewGuid(), Guid.NewGuid()),
            new SplitEditDto(0m, null, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task UpdateSplit_WithMissingOrUnrelatedSplit_ReturnsNotFound()
    {
        var original = await CreateSplitTransactionAsync();
        var other = await CreateSplitTransactionAsync();
        foreach (var splitKey in new[] { Guid.NewGuid(), other.Splits[0].Key })
        {
            var response = await _client.PutAsJsonAsync(SplitUrl(original.Key, splitKey),
                new SplitEditDto(0m, null, null));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.That(problem!.Extensions["resourceType"]?.ToString(), Is.EqualTo("Split"));
        }
    }

    [Test]
    public async Task UpdateSplit_WithOtherTenantTransaction_ReturnsNotFound()
    {
        var original = await CreateSplitTransactionAsync();
        var otherTenantKey = await CreateTestTenantAsync("Other workspace");
        using var client = _factory.CreateAuthenticatedClient(otherTenantKey);
        var response = await client.PutAsJsonAsync(
            $"/api/tenant/{otherTenantKey}/transactions/{original.Key}/splits/{original.Splits[0].Key}",
            new SplitEditDto(0m, null, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task UpdateSplit_WithConcurrentChange_ReturnsConflictProblem()
    {
        var handler = new CustomExceptionHandler(NullLogger<CustomExceptionHandler>.Instance);
        var context = new DefaultHttpContext { RequestServices = _factory.Services };
        using var body = new MemoryStream();
        context.Response.Body = body;

        var handled = await handler.TryHandleAsync(context,
            new DataConflictException(new InvalidOperationException("Private database details")), CancellationToken.None);

        Assert.That(handled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        body.Position = 0;
        var problem = await System.Text.Json.JsonSerializer.DeserializeAsync<ProblemDetails>(body);
        Assert.That(problem!.Status, Is.EqualTo(409));
        Assert.That(problem.Detail, Does.Contain("Reload"));
        Assert.That(problem.Detail, Does.Not.Contain("Private database details"));
    }

    [Test]
    public async Task UpdateSplit_OpenApi_DescribesRequestAndResponses()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var path = document.RootElement.GetProperty("paths").EnumerateObject().Single(property =>
            property.Name.Equals("/api/tenant/{tenantKey}/Transactions/{key}/splits/{splitKey}",
                StringComparison.OrdinalIgnoreCase));
        var operation = path.Value.GetProperty("put");
        var requestSchema = operation.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
        Assert.That(requestSchema, Does.EndWith("/SplitEditDto"));
        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "401", "403", "404", "409" })
        {
            Assert.That(responses.TryGetProperty(status, out _), Is.True, $"Missing {status} response");
        }
        var resultSchema = responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
        Assert.That(resultSchema, Does.EndWith("/TransactionDetailDto"));
    }

    private string SplitUrl(Guid transactionKey, Guid splitKey)
        => $"/api/tenant/{_testTenantKey}/transactions/{transactionKey}/splits/{splitKey}";

    private async Task<TransactionDetailDto> CreateSplitTransactionAsync()
    {
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions",
            new TransactionEditDto(new DateOnly(2026, 1, 1), -100m, "Split purchase",
                "Transaction memo", "Imported", "External ID", "Groceries"));
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();
        var splitResponse = await _client.PostAsJsonAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}/splits",
            new SplitEditDto(-40m, "Household", "Original split memo"));
        Assert.That(splitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await splitResponse.Content.ReadFromJsonAsync<TransactionDetailDto>())!;
    }
}
