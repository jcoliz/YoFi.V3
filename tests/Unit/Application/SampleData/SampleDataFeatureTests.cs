using System.Collections.Immutable;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YoFi.V3.Application;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.SampleData;

[TestFixture]
public class SampleDataFeatureTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private MemoryCache _cache = null!;
    private SampleDataFeature _feature = null!;
    private DataImportService _importer = null!;
    private readonly List<IModel> _staged = [];
    private static SampleDataRequest Request => new("personal-finance", 1, new(2024, 1, 1), new(2024, 3, 31));

    [SetUp]
    public void SetUp()
    {
        _staged.Clear();
        _data = new Mock<IDataProvider>();
        _tenant = new TestTenantProvider();
        _tenant.CurrentTenant.Id = 42;
        _cache = new MemoryCache(new MemoryCacheOptions());
        var regex = new RegexValidationService();
        _importer = new DataImportService(_tenant, _data.Object, _cache, regex);
        _feature = new SampleDataFeature(new SampleScenarioLoader(), new SampleDataGenerator(), _importer, _tenant, _data.Object, regex);
        _data.Setup(d => d.Get<Transaction>()).Returns(new[]
        {
            new Transaction { TenantId = 42 },
            new Transaction { TenantId = 99 },
            new Transaction { TenantId = 99 }
        }.AsQueryable());
        _data.Setup(d => d.CountAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> q) => Task.FromResult(q.Count()));
        _data.Setup(d => d.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback((IEnumerable<IModel> records) => _staged.AddRange(records));
        _data.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public void DependencyRegistration_ResolvesCatalogAndAllUseCaseDependencies()
    {
        var services = new ServiceCollection().AddApplicationFeatures();
        services.AddSingleton(_data.Object);
        services.AddSingleton<YoFi.V3.Entities.Tenancy.Providers.ITenantProvider>(_tenant);
        services.AddSingleton(Mock.Of<YoFi.V3.Entities.Tenancy.Providers.ITenantRepository>());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        var feature = scope.ServiceProvider.GetRequiredService<SampleDataFeature>();
        Assert.That(feature.GetScenarios().Single().EntityTypes, Is.EqualTo(new[] { "Transactions", "PayeeMatchingRules" }));
    }

    [Test]
    public async Task Preview_ReturnsExactCountsResolvedInputsAndTenantScopedExistingCountWithoutWriting()
    {
        var result = await _feature.PreviewAsync(Request);
        var generated = new SampleDataGenerator().Generate(new SampleScenarioLoader().Scenarios[0], Request.StartDate!.Value, Request.EndDate!.Value);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Inputs, Is.EqualTo(Request));
            Assert.That(result.Counts.Transactions, Is.EqualTo(generated.Transactions.Length));
            Assert.That(result.Counts.PayeeMatchingRules, Is.EqualTo(generated.Rules.Length));
            Assert.That(result.Counts.Total, Is.EqualTo(generated.Transactions.Length + generated.Rules.Length));
            Assert.That(result.ExistingTransactionCount, Is.EqualTo(1));
            Assert.That(_staged, Is.Empty);
        }
        _data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Load_CommitsWholeSetOnceWithFreshKeysTenantSplitsAndRuleInitialization()
    {
        var preview = await _feature.PreviewAsync(Request);
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), "stale");
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(99), "other tenant");
        var before = DateTimeOffset.UtcNow;
        var result = await _feature.LoadAsync(Request);
        var transactions = _staged.OfType<Transaction>().ToArray();
        var rules = _staged.OfType<PayeeMatchingRule>().ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.EqualTo(preview.Counts));
            Assert.That(result.Updated.Total, Is.Zero);
            Assert.That(_staged.OfType<BaseTenantModel>().Select(t => t.TenantId), Is.All.EqualTo(42));
            Assert.That(_staged.OfType<BaseModel>().Select(t => t.Key), Is.Unique.And.All.Not.EqualTo(Guid.Empty));
            Assert.That(transactions, Has.Length.EqualTo(result.Created.Transactions));
            foreach (var transaction in transactions)
            {
                Assert.That(transaction.Splits, Has.Count.EqualTo(1));
                Assert.That(transaction.Splits.Single().Amount, Is.EqualTo(transaction.Amount));
                Assert.That(transaction.Splits.Single().Order, Is.Zero);
                Assert.That(transaction.Splits.Single().Category, Is.Not.Empty);
                Assert.That(transaction.ExternalId, Does.StartWith("sample:").And.Length.LessThanOrEqualTo(100));
            }
            Assert.That(rules, Has.Length.EqualTo(result.Created.PayeeMatchingRules));
            foreach (var rule in rules)
            {
                Assert.That(rule.CreatedAt, Is.InRange(before, DateTimeOffset.UtcNow));
                Assert.That(rule.ModifiedAt, Is.EqualTo(rule.CreatedAt));
                Assert.That(rule.MatchCount, Is.Zero);
                Assert.That(rule.LastUsedAt, Is.Null);
                Assert.That(rule.PayeeIsRegex, Is.False);
            }
            Assert.That(_cache.TryGetValue(PayeeMatchingRuleFeature.CacheKey(42), out _), Is.False);
            Assert.That(_cache.Get<string>(PayeeMatchingRuleFeature.CacheKey(99)), Is.EqualTo("other tenant"));
        }
        _data.Verify(d => d.AddRange(It.IsAny<IEnumerable<IModel>>()), Times.Once);
        _data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _data.Verify(d => d.RemoveRange(It.IsAny<IEnumerable<IModel>>()), Times.Never);
    }

    [Test]
    public async Task RepeatedLoads_CreateDuplicateCopiesWithStableExternalIdsButDifferentKeys()
    {
        await _feature.LoadAsync(Request);
        var firstTransactions = _staged.OfType<Transaction>().ToArray();
        var firstKeys = _staged.OfType<BaseModel>().Select(t => t.Key).ToHashSet();
        _staged.Clear();
        await _feature.LoadAsync(Request);
        Assert.That(_staged.OfType<Transaction>().Select(t => t.ExternalId), Is.EqualTo(firstTransactions.Select(t => t.ExternalId)));
        Assert.That(_staged.OfType<BaseModel>().Any(t => firstKeys.Contains(t.Key)), Is.False);
    }

    [TestCase(null, "2024-01-31")]
    [TestCase("2024-01-01", null)]
    [TestCase("2024-02-01", "2024-01-01")]
    [TestCase("2024-01-01", "2025-01-01")]
    [TestCase("0001-01-01", "0001-01-02")]
    [TestCase("1900-01-01", "1900-01-02")]
    [TestCase("9999-01-01", "9999-01-02")]
    public async Task InvalidDates_AreRejectedForEveryUseCase(string? start, string? end)
    {
        var request = Request with { StartDate = start == null ? null : DateOnly.Parse(start), EndDate = end == null ? null : DateOnly.Parse(end) };
        await Assert.ThrowsAsync<ValidationException>(() => _feature.PreviewAsync(request));
        await Assert.ThrowsAsync<ValidationException>(() => _feature.LoadAsync(request));
        Assert.Throws<ValidationException>(() => _feature.DownloadOfx(request));
        Assert.That(_staged, Is.Empty);
    }

    [TestCase("personal-finance", 999)]
    [TestCase("missing", 1)]
    [TestCase("../personal-finance.toml", 1)]
    public async Task UnavailableScenarioAndStaleRevision_AreRejectedForEveryUseCase(string scenario, int revision)
    {
        var request = Request with { ScenarioId = scenario, Revision = revision };
        await Assert.ThrowsAsync<ValidationException>(() => _feature.PreviewAsync(request));
        await Assert.ThrowsAsync<ValidationException>(() => _feature.LoadAsync(request));
        Assert.Throws<ValidationException>(() => _feature.DownloadOfx(request));
        Assert.That(_staged, Is.Empty);
    }

    [Test]
    public async Task FailedSave_PropagatesFailureAndDoesNotInvalidateCache()
    {
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), "existing");
        _data.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Failed commit"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _feature.LoadAsync(Request));
        Assert.That(error!.Message, Is.EqualTo("Failed commit"));
        Assert.That(_cache.Get<string>(PayeeMatchingRuleFeature.CacheKey(42)), Is.EqualTo("existing"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task InvalidInternallyGeneratedDtos_AreRejectedBeforeStagingAnyRecords(bool invalidTransaction)
    {
        var valid = new TransactionEditDto(new(2024, 1, 1), 1, "Store", null, null, null, "Food");
        var dataset = new SampleDataSet(
            [valid, invalidTransaction ? valid with { Amount = 0 } : valid],
            [new PayeeMatchingRuleEditDto("Store", false, invalidTransaction ? "Food" : "")]);
        await Assert.ThrowsAsync<ValidationException>(() => _importer.CreateAsync(dataset));
        Assert.That(_staged, Is.Empty);
        _data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CanceledLoad_DoesNotStageOrCommit()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => _feature.LoadAsync(Request, cancellation.Token));
        Assert.That(_staged, Is.Empty);
        _data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SharedCreatePath_UsesSameCategorySanitizationForRulesAndSplits()
    {
        await _importer.CreateAsync(new SampleDataSet(
            [new TransactionEditDto(new(2024, 1, 1), -5, "Store", null, null, null, " food :: groceries ")],
            [new PayeeMatchingRuleEditDto("Store", false, " food :: groceries ")]));
        Assert.That(_staged.OfType<Transaction>().Single().Splits.Single().Category, Is.EqualTo("Food:Groceries"));
        Assert.That(_staged.OfType<PayeeMatchingRule>().Single().Category, Is.EqualTo("Food:Groceries"));
    }

    [Test]
    public async Task Download_AndDirectLoadHaveIdenticalOccurrencesAndUsefulAttachmentMetadata()
    {
        var preview = await _feature.PreviewAsync(Request);
        var download = _feature.DownloadOfx(Request);
        using var stream = download.Stream;
        var parsed = await OfxParsingHelper.ParseAsync(stream, download.FileName);
        await _feature.LoadAsync(Request);
        var loaded = _staged.OfType<Transaction>().OrderBy(t => t.ExternalId, StringComparer.Ordinal).ToArray();
        var imported = parsed.Transactions.OrderBy(t => t.ExternalId, StringComparer.Ordinal).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(download.ContentType, Is.EqualTo("application/x-ofx"));
            Assert.That(download.FileName, Is.EqualTo("personal-finance-2024-01-01-2024-03-31.ofx"));
            Assert.That(parsed.Errors.Select(e => e.Message), Is.Empty);
            Assert.That(imported, Has.Length.EqualTo(preview.Counts.Transactions));
            Assert.That(imported.Select(t => (t.Date, t.Amount, t.Payee, t.Source, t.ExternalId)),
                Is.EqualTo(loaded.Select(t => (t.Date, t.Amount, t.Payee, t.Source, t.ExternalId))));
        }
    }
}
