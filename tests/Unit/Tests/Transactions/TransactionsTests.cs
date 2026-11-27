using System.Data;
using System.Linq.Expressions;
using DotNetNuxt.Abstractions;
using Moq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Validation;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application;

[TestFixture]
public class TransactionsTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private TransactionsFeature _feature = null!;
    private List<Transaction> _transactions = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _tenant = new TestTenantProvider();
        _tenant.CurrentTenant.Id = 42;
        _transactions = [];

        _data.Setup(data => data.GetIncluding<Transaction, ICollection<Split>>(
                It.IsAny<Expression<Func<Transaction, ICollection<Split>>>>()))
            .Returns(() => _transactions.AsQueryable());
        _data.Setup(data => data.SingleOrDefaultAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) => Task.FromResult(query.SingleOrDefault()));
        _data.Setup(data => data.CountAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) => Task.FromResult(query.Count()));
        _data.Setup(data => data.ToListNoTrackingAsync(It.IsAny<IQueryable<TransactionResultDto>>()))
            .Returns((IQueryable<TransactionResultDto> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _data.Setup(data => data.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<TransactionDetailDto>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IsolationLevel?>()))
            .Returns((Func<CancellationToken, Task<TransactionDetailDto>> operation,
                CancellationToken cancellationToken,
                IsolationLevel? _) => operation(cancellationToken));

        _feature = new TransactionsFeature(_tenant, _data.Object);
    }

    [Test]
    public async Task GetTransactionsAsync_PaginatesAndFiltersTenantTransactions()
    {
        _transactions.AddRange([
            CreateTransaction("Coffee Shop", -12.50m, new(2026, 1, 2)),
            CreateTransaction("Market", -30m, new(2026, 1, 1))
        ]);

        var result = await _feature.GetTransactionsAsync(pageNumber: 1, search: "Coffee");

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items.First().Payee, Is.EqualTo("Coffee Shop"));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAndDeleteTransactionAsync_LoadsAndRemovesTransaction()
    {
        var transaction = CreateTransaction("Test Payee", 25m, new(2026, 2, 1));
        _transactions.Add(transaction);

        var detail = await _feature.GetTransactionByKeyAsync(transaction.Key);
        await _feature.DeleteTransactionAsync(transaction.Key);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(detail.Payee, Is.EqualTo("Test Payee"));
            Assert.That(detail.Category, Is.EqualTo("Groceries"));
            _data.Verify(data => data.Remove(transaction), Times.Once);
            _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Test]
    public void GetTransactionByKeyAsync_MissingTransaction_Throws()
    {
        Assert.That(
            async () => await _feature.GetTransactionByKeyAsync(Guid.NewGuid()),
            Throws.InstanceOf<TransactionNotFoundException>());
    }

    [Test]
    public async Task AddTransactionAsync_AddsSanitizedSplit()
    {
        var dto = new TransactionEditDto(
            new(2026, 3, 1), -42m, "  Corner Store  ", "memo", "bank", "external", "  snacks  ");

        var result = await _feature.AddTransactionAsync(dto);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Amount, Is.EqualTo(-42m));
            Assert.That(result.Category, Is.EqualTo("Snacks"));
            _data.Verify(data => data.Add(It.IsAny<IModel>()), Times.Once);
            _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Test]
    public async Task AddTransactionsAsync_AddsBatchAndReturnsDetails()
    {
        var dtos = new[]
        {
            new TransactionEditDto(new(2026, 3, 1), -10m, "A", null, null, null, "food"),
            new TransactionEditDto(new(2026, 3, 2), -20m, "B", null, null, null, "home")
        };

        var result = await _feature.AddTransactionsAsync(dtos);

        Assert.That(result, Has.Count.EqualTo(2));
        _data.Verify(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()), Times.Once);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task QuickEditTransactionAsync_UpdatesSingleSplitFields()
    {
        var transaction = CreateTransaction("Old Payee", 100m, new(2026, 4, 1));
        _transactions.Add(transaction);

        var result = await _feature.QuickEditTransactionAsync(
            transaction.Key,
            new TransactionQuickEditDto("New Payee", "New memo", "  groceries  "));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Payee, Is.EqualTo("New Payee"));
            Assert.That(result.Memo, Is.EqualTo("New memo"));
            Assert.That(result.Category, Is.EqualTo("Groceries"));
        }
    }

    [Test]
    public async Task QuickEditTransactionAsync_MultipleSplitsRejectsCategory()
    {
        var transaction = CreateTransaction("Payee", 100m, new(2026, 4, 1));
        transaction.Splits.Add(new Split { Amount = 40m, Category = "Home", Order = 1 });
        transaction.Splits.First().Amount = 60m;
        _transactions.Add(transaction);

        Assert.That(
            async () => await _feature.QuickEditTransactionAsync(
                transaction.Key, new TransactionQuickEditDto("Payee", null, "Category")),
            Throws.InstanceOf<ValidationException>());
    }

    [Test]
    public async Task UpdateTransactionAsync_RebalancesAndUpdatesSingleSplit()
    {
        var transaction = CreateTransaction("Old", -100m, new(2026, 5, 1));
        _transactions.Add(transaction);

        var result = await _feature.UpdateTransactionAsync(
            transaction.Key,
            new TransactionEditDto(new(2026, 5, 2), -90m, "New", "memo", "source", "external", "  fuel  "));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Amount, Is.EqualTo(-90m));
            Assert.That(result.Category, Is.EqualTo("Fuel"));
            Assert.That(transaction.Splits.Single().Amount, Is.EqualTo(-90m));
        }
    }

    [Test]
    public async Task AddUpdateAndDeleteSplitAsync_MaintainBalances()
    {
        var transaction = CreateTransaction("Payee", -100m, new(2026, 6, 1));
        _transactions.Add(transaction);

        var added = await _feature.AddSplitAsync(
            transaction.Key, new SplitEditDto(-10m, "  snacks  ", "added"));
        Assert.That(added.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -90m, -10m }));

        var splitToUpdate = transaction.Splits.First(split => split.Category == "Snacks");
        var updated = await _feature.UpdateSplitAsync(
            transaction.Key, splitToUpdate.Key, new SplitEditDto(-20m, "  food  ", "updated"));
        Assert.That(updated.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -80m, -20m }));

        var deleted = await _feature.DeleteSplitAsync(transaction.Key, splitToUpdate.Key);
        Assert.That(deleted.Splits, Is.Empty);
        Assert.That(deleted.Category, Is.EqualTo("Groceries"));
    }

    [Test]
    public void SplitOperations_RejectMissingOrLastSplit()
    {
        var transaction = CreateTransaction("Payee", -100m, new(2026, 7, 1));
        transaction.Splits.Clear();
        _transactions.Add(transaction);

        Assert.That(
            async () => await _feature.AddSplitAsync(transaction.Key, new SplitEditDto(-10m, null, null)),
            Throws.InstanceOf<ValidationException>());

        transaction.Splits.Add(new Split { Amount = -100m, Category = "Food", Order = 0 });
        Assert.That(
            async () => await _feature.DeleteSplitAsync(transaction.Key, transaction.Splits.First().Key),
            Throws.InstanceOf<ValidationException>());
    }

    private static Transaction CreateTransaction(string payee, decimal amount, DateOnly date)
    {
        return new Transaction
        {
            Key = Guid.NewGuid(),
            TenantId = 42,
            Date = date,
            Payee = payee,
            Memo = "memo",
            Amount = amount,
            Splits =
            [
                new Split
                {
                    Key = Guid.NewGuid(),
                    Amount = amount,
                    Category = "Groceries",
                    Memo = "split memo",
                    Order = 0
                }
            ]
        };
    }
}
