using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>
/// Verifies transaction search query binding, response contracts, and authorization through HTTP.
/// </summary>
[TestFixture]
public class TransactionSearchControllerTests : AuthenticatedTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        _testTenantKey = await CreateTestTenantAsync();
        SwitchToViewer();
    }

    [TestCase(" Coffee Shop ", "Coffee Shop", 10)]
    [TestCase("c=food%_*'\\", "c=food%_*'\\", 10)]
    [TestCase("$500", "Amount match", 500)]
    [TestCase("+$500", "Amount match", 500)]
    [TestCase("$-500", "Amount match", -500)]
    public async Task GetTransactions_SearchQuery_BindsEncodedTermAndReturnsMatchingTransaction(
        string search, string payee, decimal amount)
    {
        var matching = new Transaction { Payee = payee, Amount = amount };
        await SeedTransactionsAsync(_testTenantKey,
            matching, new Transaction { Payee = "Unrelated", Amount = 12m });

        using var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?search={Uri.EscapeDataString(search)}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Select(item => item.Key), Is.EqualTo(new[] { matching.Key }));
        Assert.That(result.Items.Single().Amount, Is.EqualTo(amount));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(1));
    }

    [TestCase("")]
    [TestCase("?search=")]
    [TestCase("?search=%20%20%20")]
    public async Task GetTransactions_NoEffectiveSearch_PreservesUnfilteredResponse(string queryString)
    {
        var first = new Transaction { Payee = "First", Amount = 10m };
        var second = new Transaction { Payee = "Second", Amount = 20m };
        await SeedTransactionsAsync(_testTenantKey, first, second);

        using var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions{queryString}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Select(item => item.Key),
            Is.EquivalentTo(new[] { first.Key, second.Key }));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(2));
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(1));
    }

    [Test]
    public async Task GetTransactions_SearchAndDateQueries_BindTogetherWithInclusiveBounds()
    {
        var date = new DateOnly(2026, 10, 16);
        var matching = new Transaction { Payee = "Coffee", Amount = 10m, Date = date };
        await SeedTransactionsAsync(_testTenantKey,
            matching,
            new Transaction { Payee = "Coffee", Amount = 10m, Date = date.AddDays(-1) },
            new Transaction { Payee = "Coffee", Amount = 10m, Date = date.AddDays(1) },
            new Transaction { Payee = "Unrelated", Amount = 10m, Date = date });

        using var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?search=coffee&fromDate=2026-10-16&toDate=2026-10-16");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Select(item => item.Key), Is.EqualTo(new[] { matching.Key }));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetTransactions_AdvancedFilters_BindTogetherAndPreserveSignedAmount()
    {
        var uncategorizedMatch = new Transaction
        {
            Payee = "Cafe",
            Amount = 42m,
            Memo = "Work trip",
            Splits =
            [
                new Split { Amount = 40m, Category = "Food", Memo = null, Order = 0 },
                new Split { Amount = 2m, Category = string.Empty, Memo = "Transit", Order = 1 }
            ]
        };
        var categorizedMatch = new Transaction
        {
            Payee = "Cafe",
            Amount = 42m,
            Memo = "Work trip",
            Splits = [new Split { Amount = 42m, Category = "Food", Memo = "Transit", Order = 0 }]
        };
        await SeedTransactionsAsync(_testTenantKey, uncategorizedMatch, categorizedMatch);

        var query = "payee=cafe&category=%20food%20&memo=transit&amount=%2B42";
        using var uncategorizedResponse = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?{query}&uncategorizedOnly=true");

        Assert.That(uncategorizedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var uncategorizedResult = await uncategorizedResponse.Content
            .ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(uncategorizedResult, Is.Not.Null);
        Assert.That(uncategorizedResult!.Items.Select(item => item.Key),
            Is.EqualTo(new[] { uncategorizedMatch.Key }));
        Assert.That(uncategorizedResult.Metadata.TotalCount, Is.EqualTo(1));

        using var allResponse = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?{query}&uncategorizedOnly=false");

        Assert.That(allResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var allResult = await allResponse.Content.ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(allResult, Is.Not.Null);
        Assert.That(allResult!.Items.Select(item => item.Key),
            Is.EquivalentTo(new[] { uncategorizedMatch.Key, categorizedMatch.Key }));
        Assert.That(allResult.Metadata.TotalCount, Is.EqualTo(2));
    }

    [TestCase("amount=not-an-amount")]
    [TestCase("amount=79228162514264337593543950336")]
    public async Task GetTransactions_InvalidAdvancedAmount_Returns400WithProblemDetails(string query)
    {
        using var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Status, Is.EqualTo(400));
    }

    [TestCase("fromDate=2026-10-17&toDate=2026-10-16")]
    [TestCase("fromDate=not-a-date")]
    public async Task GetTransactions_SearchWithInvalidDates_Returns400WithProblemDetails(string dateQuery)
    {
        using var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?search=coffee&{dateQuery}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task GetTransactions_SearchAndPageNumber_ReturnsFilteredPaginationMetadata()
    {
        var matches = Enumerable.Range(0, PaginationHelper.ItemsPerPage + 1)
            .Select(index => new Transaction
            {
                Payee = $"Coffee {index}", Amount = 10m, Date = new DateOnly(2026, 10, 16)
            })
            .ToArray();
        await SeedTransactionsAsync(_testTenantKey,
            [.. matches, new Transaction { Payee = "Unrelated", Amount = 10m }]);

        using var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?search=coffee&pageNumber=2");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Select(item => item.Key), Is.EqualTo(new[] { matches[0].Key }));
        Assert.That(result.Metadata, Is.EqualTo(new PaginationMetadata(
            PageNumber: 2,
            PageSize: PaginationHelper.ItemsPerPage,
            TotalCount: PaginationHelper.ItemsPerPage + 1,
            TotalPages: 2,
            HasPreviousPage: true,
            HasNextPage: false,
            FirstItem: PaginationHelper.ItemsPerPage + 1,
            LastItem: PaginationHelper.ItemsPerPage + 1)));
    }

    [Test]
    public async Task GetTransactions_NoSearchMatches_ReturnsEmptySuccessfulResponse()
    {
        await SeedTransactionsAsync(_testTenantKey,
            new Transaction { Payee = "Unrelated", Amount = 10m });

        using var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?search=coffee");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Empty);
        Assert.That(result.Metadata.TotalCount, Is.Zero);
        Assert.That(result.Metadata.TotalPages, Is.Zero);
    }

    [Test]
    public async Task GetTransactions_SearchWithoutAuthentication_Returns401WithProblemDetails()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Anonymous";
                    options.DefaultChallengeScheme = "Anonymous";
                }).AddScheme<AuthenticationSchemeOptions, AnonymousAuthenticationHandler>(
                    "Anonymous", _ => { })));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?search=coffee");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task GetTransactions_SearchWithoutTenantAccess_Returns403WithProblemDetails()
    {
        var otherTenantKey = await CreateTestTenantAsync();

        using var response = await _client.GetAsync(
            $"/api/tenant/{otherTenantKey}/transactions?search=coffee");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task GetTransactions_SearchWithAccessToBothTenants_ReturnsOnlyRequestedTenantData()
    {
        var matching = new Transaction { Payee = "Coffee", Amount = 10m };
        await SeedTransactionsAsync(_testTenantKey, matching);
        var otherTenantKey = await CreateTestTenantAsync();
        await SeedTransactionsAsync(otherTenantKey,
            new Transaction { Payee = "Coffee", Amount = 20m });
        using var client = CreateMultiTenantClient(
            (_testTenantKey, TenantRole.Viewer),
            (otherTenantKey, TenantRole.Viewer));

        using var response = await client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions?search=coffee");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Select(item => item.Key), Is.EqualTo(new[] { matching.Key }));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(1));
    }

    private async Task SeedTransactionsAsync(Guid tenantKey, params Transaction[] transactions)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantId = await context.Tenants.Where(tenant => tenant.Key == tenantKey)
            .Select(tenant => tenant.Id).SingleAsync();
        foreach (var transaction in transactions)
        {
            transaction.TenantId = tenantId;
        }

        context.Transactions.AddRange(transactions);
        await context.SaveChangesAsync();
    }

    private sealed class AnonymousAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());
    }
}
