using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TransactionSplitDeleteControllerTests : AuthenticatedTestBase
{
    [TestCase(TenantRole.Editor, 0)]
    [TestCase(TenantRole.Owner, 1)]
    public async Task DeleteSplit_AsWriter_ReturnsAndPersistsSingleCategory(TenantRole role, int deletedIndex)
    {
        SwitchToRole(role);
        var original = await CreateSplitTransactionAsync();
        var survivor = original.Splits[1 - deletedIndex];

        var response = await _client.DeleteAsync(SplitUrl(original.Key, original.Splits[deletedIndex].Key));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated!.Splits, Is.Empty);
        Assert.That(updated.Category, Is.EqualTo(survivor.Category));
        Assert.That(updated with { Category = original.Category, Splits = original.Splits }, Is.EqualTo(original));
        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(TransactionUrl(original.Key));
        Assert.That(persisted!.Splits, Is.Empty);
        Assert.That(persisted with { Splits = updated.Splits }, Is.EqualTo(updated));

        var lastResponse = await _client.DeleteAsync(SplitUrl(original.Key, survivor.Key));
        Assert.That(lastResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await lastResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem!.Errors, Does.ContainKey("splitKey"));
        var afterRejection = (await _client.GetFromJsonAsync<TransactionDetailDto>(TransactionUrl(original.Key)))!;
        Assert.That(afterRejection.Splits, Is.Empty);
        Assert.That(afterRejection with { Splits = updated.Splits }, Is.EqualTo(updated));
    }

    [Test]
    public async Task DeleteSplit_FromThreeSplits_ReturnsBalancedOrderedSurvivors()
    {
        var original = await CreateSplitTransactionAsync();
        var addResponse = await _client.PostAsJsonAsync($"{TransactionUrl(original.Key)}/splits",
            new SplitEditDto(-10.01m, "Supplies", "Third memo"));
        Assert.That(addResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var three = (await addResponse.Content.ReadFromJsonAsync<TransactionDetailDto>())!;
        var expected = new[]
        {
            three.Splits[0] with { Amount = -89.99m },
            three.Splits[2] with { Order = 1 }
        };

        var response = await _client.DeleteAsync(SplitUrl(original.Key, three.Splits[1].Key));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated!.Splits, Is.EqualTo(expected));
        Assert.That(updated.Splits.Sum(s => s.Amount), Is.EqualTo(-100m));
        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(TransactionUrl(original.Key));
        Assert.That(persisted!.Splits, Is.EqualTo(expected));
    }

    [Test]
    public async Task DeleteSplit_AsViewer_ReturnsForbidden()
    {
        var original = await CreateSplitTransactionAsync();
        SwitchToViewer();
        var response = await _client.DeleteAsync(SplitUrl(original.Key, original.Splits[0].Key));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(TransactionUrl(original.Key));
        Assert.That(persisted!.Splits, Is.EqualTo(original.Splits));
    }

    [Test]
    public async Task DeleteSplit_Unauthenticated_ReturnsUnauthorized()
    {
        using var factory = new BaseTestWebApplicationFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        var response = await client.DeleteAsync(SplitUrl(Guid.NewGuid(), Guid.NewGuid()));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DeleteSplit_WithoutTenantAccess_ReturnsForbidden()
    {
        var otherTenant = await CreateTestTenantAsync("Other workspace");
        using var client = _factory.CreateAuthenticatedClient(otherTenant);
        var response = await client.DeleteAsync(SplitUrl(Guid.NewGuid(), Guid.NewGuid()));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteSplit_MissingOrUnrelatedKeys_ReturnsNotFound()
    {
        var original = await CreateSplitTransactionAsync();
        var other = await CreateSplitTransactionAsync();
        var missingTransaction = await _client.DeleteAsync(SplitUrl(Guid.NewGuid(), original.Splits[0].Key));
        Assert.That(missingTransaction.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        foreach (var key in new[] { Guid.NewGuid(), other.Splits[0].Key })
        {
            var response = await _client.DeleteAsync(SplitUrl(original.Key, key));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.That(problem!.Extensions["resourceType"]?.ToString(), Is.EqualTo("Split"));
        }
        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(TransactionUrl(original.Key));
        Assert.That(persisted!.Splits, Is.EqualTo(original.Splits));
    }

    [Test]
    public async Task DeleteSplit_OtherTenantTransaction_ReturnsNotFound()
    {
        var original = await CreateSplitTransactionAsync();
        var otherTenant = await CreateTestTenantAsync("Other workspace");
        using var client = _factory.CreateAuthenticatedClient(otherTenant);
        var response = await client.DeleteAsync(
            $"/api/tenant/{otherTenant}/transactions/{original.Key}/splits/{original.Splits[0].Key}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(TransactionUrl(original.Key));
        Assert.That(persisted!.Splits, Is.EqualTo(original.Splits));
    }

    [Test]
    public async Task DeleteSplit_OpenApi_DescribesDetailAndErrorResponsesWithoutBody()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));
        var path = document.RootElement.GetProperty("paths").EnumerateObject().Single(property =>
            property.Name.Equals("/api/tenant/{tenantKey}/Transactions/{key}/splits/{splitKey}",
                StringComparison.OrdinalIgnoreCase));
        var operation = path.Value.GetProperty("delete");
        Assert.That(operation.TryGetProperty("requestBody", out _), Is.False);
        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "401", "403", "404", "409" })
        {
            Assert.That(responses.TryGetProperty(status, out _), Is.True, $"Missing {status} response");
        }
        var schema = responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
        Assert.That(schema, Does.EndWith("/TransactionDetailDto"));
    }

    private string TransactionUrl(Guid key) => $"/api/tenant/{_testTenantKey}/transactions/{key}";

    private string SplitUrl(Guid key, Guid splitKey) => $"{TransactionUrl(key)}/splits/{splitKey}";

    private async Task<TransactionDetailDto> CreateSplitTransactionAsync()
    {
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions",
            new TransactionEditDto(new DateOnly(2026, 1, 1), -100m, "Split purchase",
                "Transaction memo", "Imported", "External ID", "Groceries"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = (await response.Content.ReadFromJsonAsync<TransactionDetailDto>())!;
        var splitResponse = await _client.PostAsJsonAsync($"{TransactionUrl(created.Key)}/splits",
            new SplitEditDto(-40m, "Household", "Split memo"));
        Assert.That(splitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await splitResponse.Content.ReadFromJsonAsync<TransactionDetailDto>())!;
    }
}
