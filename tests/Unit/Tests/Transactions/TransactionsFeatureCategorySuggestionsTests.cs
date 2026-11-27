using DotNetNuxt.Abstractions;
using Moq;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application;

/// <summary>
/// Unit tests for transaction category suggestion lookup in <see cref="TransactionsFeature"/>.
/// </summary>
[TestFixture]
public class TransactionsFeatureCategorySuggestionsTests
{
    private Mock<IDataProvider> _dataProvider = null!;
    private TestTenantProvider _tenantProvider = null!;
    private TransactionsFeature _feature = null!;
    private List<Transaction> _transactions = null!;
    private List<Split> _splits = null!;

    [SetUp]
    public void SetUp()
    {
        _tenantProvider = new TestTenantProvider();
        _tenantProvider.CurrentTenant.Id = 42;
        _transactions = [];
        _splits = [];
        _dataProvider = new Mock<IDataProvider>();
        _dataProvider.Setup(data => data.Get<Transaction>())
            .Returns(() => _transactions.AsQueryable());
        _dataProvider.Setup(data => data.Get<Split>())
            .Returns(() => _splits.AsQueryable());
        _dataProvider.Setup(data =>
                data.ToListNoTrackingAsync(It.IsAny<IQueryable<string>>()))
            .Returns((IQueryable<string> query) => Task.FromResult(query.ToList()));

        _feature = new TransactionsFeature(_tenantProvider, _dataProvider.Object);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("::")]
    public async Task GetCategorySuggestionsAsync_BlankOrSanitizesToEmpty_ThrowsArgumentException(string? term)
    {
        // Given: A category term that is blank or normalizes to no category terms

        // When: Suggestions are requested

        // Then: The feature rejects the term
        Assert.That(
            async () => await _feature.GetCategorySuggestionsAsync(term!),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void GetCategorySuggestionsAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Given: A canceled lookup
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // When: Suggestions are requested

        // Then: Cancellation is observed before querying
        Assert.That(
            async () => await _feature.GetCategorySuggestionsAsync("food", cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task GetCategorySuggestionsAsync_FindsDistinctTenantPrefixMatchesInOrder()
    {
        // Given: Tenant splits with matching, duplicate, empty, and other-tenant categories
        var tenantTransaction = new Transaction { Id = 1, TenantId = 42 };
        var otherTenantTransaction = new Transaction { Id = 2, TenantId = 43 };
        _transactions.AddRange([tenantTransaction, otherTenantTransaction]);
        _splits.AddRange([
            new Split { TransactionId = 1, Category = "Groceries" },
            new Split { TransactionId = 1, Category = "groceries" },
            new Split { TransactionId = 1, Category = "Garden" },
            new Split { TransactionId = 1, Category = "" },
            new Split { TransactionId = 1, Category = "Entertainment" },
            new Split { TransactionId = 2, Category = "Gas" }
        ]);

        // When: Suggestions are requested for a case-insensitive prefix
        var result = await _feature.GetCategorySuggestionsAsync("  g  ");

        // Then: Only distinct tenant matches are returned and ordered deterministically
        Assert.That(result, Is.EqualTo(new[] { "Garden", "Groceries" }));
    }

    [Test]
    public async Task GetCategorySuggestionsAsync_LimitsResultsToTwenty()
    {
        // Given: More than twenty matching tenant categories
        var transaction = new Transaction { Id = 1, TenantId = 42 };
        _transactions.Add(transaction);
        for (var index = 0; index < 25; index++)
        {
            _splits.Add(new Split
            {
                TransactionId = 1,
                Category = $"Home {index:D2}"
            });
        }

        // When: Suggestions are requested
        var result = await _feature.GetCategorySuggestionsAsync("home");

        // Then: The response is bounded
        Assert.That(result, Has.Count.EqualTo(20));
    }

    [Test]
    public async Task GetCategorySuggestionsAsync_RejectsTermOverTwoHundredCharacters()
    {
        // Given: A term longer than the category field limit
        var term = new string('a', 201);

        // When: Suggestions are requested

        // Then: The feature rejects the term
        Assert.That(
            async () => await _feature.GetCategorySuggestionsAsync(term),
            Throws.ArgumentException);
    }
}
