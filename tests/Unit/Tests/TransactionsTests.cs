namespace YoFi.V3.Tests.Unit.Tests;

using NUnit.Framework;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

[TestFixture]
public class TransactionsTests
{
    private TransactionsFeature _transactionsFeature = null!;
    private InMemoryDataProvider _dataProvider = null!;
    private TestTenantProvider _tenantProvider = null!;
    private long _testTenantId;

    [SetUp]
    public void Setup()
    {
        _dataProvider = new InMemoryDataProvider();
        _tenantProvider = new TestTenantProvider();
        _testTenantId = _tenantProvider.CurrentTenant.Id;
        _transactionsFeature = new TransactionsFeature(_tenantProvider, _dataProvider);
    }

    #region GetTransactionsAsync Tests

    [Test]
    public async Task GetTransactionsAsync_NoFilters_ReturnsAllTransactionsForTenant()
    {
        // Arrange
        var transaction1 = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Payee1");
        var transaction2 = CreateTransaction(DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), 200m, "Payee2");
        _dataProvider.Add(transaction1);
        _dataProvider.Add(transaction2);

        // Act
        var result = await _transactionsFeature.GetTransactionsAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(t => t.Payee), Contains.Item("Payee1"));
        Assert.That(result.Select(t => t.Payee), Contains.Item("Payee2"));
    }

    [Test]
    public async Task GetTransactionsAsync_WithFromDate_ReturnsFilteredTransactions()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        _dataProvider.Add(CreateTransaction(today, 100m, "Today"));
        _dataProvider.Add(CreateTransaction(yesterday, 200m, "Yesterday"));
        _dataProvider.Add(CreateTransaction(twoDaysAgo, 300m, "TwoDaysAgo"));

        // Act
        var result = await _transactionsFeature.GetTransactionsAsync(fromDate: yesterday);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(t => t.Payee), Does.Not.Contain("TwoDaysAgo"));
    }

    [Test]
    public async Task GetTransactionsAsync_WithToDate_ReturnsFilteredTransactions()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        _dataProvider.Add(CreateTransaction(today, 100m, "Today"));
        _dataProvider.Add(CreateTransaction(yesterday, 200m, "Yesterday"));
        _dataProvider.Add(CreateTransaction(twoDaysAgo, 300m, "TwoDaysAgo"));

        // Act
        var result = await _transactionsFeature.GetTransactionsAsync(toDate: yesterday);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(t => t.Payee), Does.Not.Contain("Today"));
    }

    [Test]
    public async Task GetTransactionsAsync_WithDateRange_ReturnsFilteredTransactions()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);
        var threeDaysAgo = today.AddDays(-3);

        _dataProvider.Add(CreateTransaction(today, 100m, "Today"));
        _dataProvider.Add(CreateTransaction(yesterday, 200m, "Yesterday"));
        _dataProvider.Add(CreateTransaction(twoDaysAgo, 300m, "TwoDaysAgo"));
        _dataProvider.Add(CreateTransaction(threeDaysAgo, 400m, "ThreeDaysAgo"));

        // Act
        var result = await _transactionsFeature.GetTransactionsAsync(fromDate: twoDaysAgo, toDate: yesterday);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(t => t.Payee), Contains.Item("Yesterday"));
        Assert.That(result.Select(t => t.Payee), Contains.Item("TwoDaysAgo"));
    }

    [Test]
    public void GetTransactionsAsync_FromDateAfterToDate_ThrowsArgumentException()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.GetTransactionsAsync(fromDate: today, toDate: yesterday));

        Assert.That(ex!.ParamName, Is.EqualTo("fromDate"));
        Assert.That(ex.Message, Does.Contain("From date cannot be later than to date"));
    }

    [Test]
    public async Task GetTransactionsAsync_OnlyReturnsCurrentTenantTransactions()
    {
        // Arrange
        var otherTenantId = 999L;
        var currentTenantTransaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "CurrentTenant");
        var otherTenantTransaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 200m, "OtherTenant");
        otherTenantTransaction.TenantId = otherTenantId;

        _dataProvider.Add(currentTenantTransaction);
        _dataProvider.Add(otherTenantTransaction);

        // Act
        var result = await _transactionsFeature.GetTransactionsAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().Payee, Is.EqualTo("CurrentTenant"));
    }

    [Test]
    public async Task GetTransactionsAsync_ReturnsTransactionsOrderedByDateDescending()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        _dataProvider.Add(CreateTransaction(yesterday, 200m, "Middle"));
        _dataProvider.Add(CreateTransaction(twoDaysAgo, 300m, "Oldest"));
        _dataProvider.Add(CreateTransaction(today, 100m, "Newest"));

        // Act
        var result = await _transactionsFeature.GetTransactionsAsync();

        // Assert
        var resultList = result.ToList();
        Assert.That(resultList[0].Payee, Is.EqualTo("Newest"));
        Assert.That(resultList[1].Payee, Is.EqualTo("Middle"));
        Assert.That(resultList[2].Payee, Is.EqualTo("Oldest"));
    }

    #endregion

    #region GetTransactionByKeyAsync Tests

    [Test]
    public async Task GetTransactionByKeyAsync_ExistingKey_ReturnsTransaction()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        // Act
        var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Key, Is.EqualTo(transaction.Key));
        Assert.That(result.Payee, Is.EqualTo("TestPayee"));
        Assert.That(result.Amount, Is.EqualTo(100m));
    }

    [Test]
    public void GetTransactionByKeyAsync_NonExistentKey_ThrowsTransactionNotFoundException()
    {
        // Arrange
        var nonExistentKey = Guid.NewGuid();

        // Act & Assert
        var ex = Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _transactionsFeature.GetTransactionByKeyAsync(nonExistentKey));

        Assert.That(ex!.Message, Does.Contain(nonExistentKey.ToString()));
    }

    [Test]
    public async Task GetTransactionByKeyAsync_OnlyReturnsCurrentTenantTransaction()
    {
        // Arrange
        var otherTenantId = 999L;
        var otherTenantTransaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 200m, "OtherTenant");
        otherTenantTransaction.TenantId = otherTenantId;
        _dataProvider.Add(otherTenantTransaction);

        // Act & Assert
        Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _transactionsFeature.GetTransactionByKeyAsync(otherTenantTransaction.Key));
    }

    #endregion

    #region AddTransactionAsync Tests

    [Test]
    public async Task AddTransactionAsync_ValidTransaction_AddsToDatabase()
    {
        // Arrange
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 150m,
            Payee: "NewPayee"
        );

        // Act
        await _transactionsFeature.AddTransactionAsync(dto);

        // Assert
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Payee, Is.EqualTo("NewPayee"));
        Assert.That(transactions[0].Amount, Is.EqualTo(150m));
        Assert.That(transactions[0].TenantId, Is.EqualTo(_testTenantId));
    }

    [Test]
    public void AddTransactionAsync_ZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 0m,
            Payee: "TestPayee"
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("amount cannot be zero"));
    }

    [Test]
    public void AddTransactionAsync_EmptyPayee_ThrowsArgumentException()
    {
        // Arrange
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: ""
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("payee cannot be empty"));
    }

    [Test]
    public void AddTransactionAsync_WhitespacePayee_ThrowsArgumentException()
    {
        // Arrange
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "   "
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("payee cannot be empty"));
    }

    [Test]
    public void AddTransactionAsync_PayeeTooLong_ThrowsArgumentException()
    {
        // Arrange
        var longPayee = new string('A', 201);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: longPayee
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("cannot exceed 200 characters"));
    }

    [Test]
    public async Task AddTransactionAsync_PayeeExactly200Characters_Succeeds()
    {
        // Arrange
        var payee200 = new string('A', 200);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: payee200
        );

        // Act
        await _transactionsFeature.AddTransactionAsync(dto);

        // Assert
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Payee, Is.EqualTo(payee200));
    }

    [Test]
    public void AddTransactionAsync_DateOutOfRange_ThrowsArgumentException()
    {
        // Arrange - Date too far in the past (more than 50 years)
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-51)),
            Amount: 100m,
            Payee: "TestPayee"
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("must be within"));
    }

    [Test]
    public void AddTransactionAsync_DateTooFarInFuture_ThrowsArgumentException()
    {
        // Arrange - Date too far in the future (more than 5 years)
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(6)),
            Amount: 100m,
            Payee: "TestPayee"
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("must be within"));
    }

    [Test]
    public void AddTransactionAsync_NullPayee_ThrowsArgumentException()
    {
        // Arrange
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: null!
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("payee cannot be empty"));
    }

    [Test]
    public async Task AddTransactionAsync_NegativeAmount_Succeeds()
    {
        // Arrange - Negative amounts are allowed (credits/refunds)
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: -100m,
            Payee: "RefundPayee"
        );

        // Act
        await _transactionsFeature.AddTransactionAsync(dto);

        // Assert
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Amount, Is.EqualTo(-100m));
    }

    #endregion

    #region UpdateTransactionAsync Tests

    [Test]
    public async Task UpdateTransactionAsync_ExistingTransaction_UpdatesSuccessfully()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "OriginalPayee");
        _dataProvider.Add(transaction);

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            Amount: 250m,
            Payee: "UpdatedPayee"
        );

        // Act
        await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto);

        // Assert
        var updated = _dataProvider.Transactions.First(t => t.Key == transaction.Key);
        Assert.That(updated.Payee, Is.EqualTo("UpdatedPayee"));
        Assert.That(updated.Amount, Is.EqualTo(250m));
        Assert.That(updated.Date, Is.EqualTo(DateOnly.FromDateTime(DateTime.Now.AddDays(1))));
    }

    [Test]
    public void UpdateTransactionAsync_NonExistentTransaction_ThrowsTransactionNotFoundException()
    {
        // Arrange
        var nonExistentKey = Guid.NewGuid();
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "UpdatedPayee"
        );

        // Act & Assert
        Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(nonExistentKey, updateDto));
    }

    [Test]
    public void UpdateTransactionAsync_ZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 0m,
            Payee: "UpdatedPayee"
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("amount cannot be zero"));
    }

    [Test]
    public void UpdateTransactionAsync_EmptyPayee_ThrowsArgumentException()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: ""
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("payee cannot be empty"));
    }

    [Test]
    public async Task UpdateTransactionAsync_OtherTenantTransaction_ThrowsTransactionNotFoundException()
    {
        // Arrange
        var otherTenantId = 999L;
        var otherTenantTransaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "OtherTenant");
        otherTenantTransaction.TenantId = otherTenantId;
        _dataProvider.Add(otherTenantTransaction);

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 200m,
            Payee: "UpdatedPayee"
        );

        // Act & Assert
        Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(otherTenantTransaction.Key, updateDto));
    }

    [Test]
    public void UpdateTransactionAsync_DateOutOfRange_ThrowsArgumentException()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-51)),
            Amount: 100m,
            Payee: "UpdatedPayee"
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("must be within"));
    }

    [Test]
    public void UpdateTransactionAsync_PayeeTooLong_ThrowsArgumentException()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        var longPayee = new string('A', 201);
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: longPayee
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("cannot exceed 200 characters"));
    }

    [Test]
    public void UpdateTransactionAsync_WhitespacePayee_ThrowsArgumentException()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "   "
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("payee cannot be empty"));
    }

    [Test]
    public void UpdateTransactionAsync_NullPayee_ThrowsArgumentException()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: null!
        );

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("payee cannot be empty"));
    }

    [Test]
    public async Task UpdateTransactionAsync_NegativeAmount_Succeeds()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: -150m,
            Payee: "UpdatedPayee"
        );

        // Act
        await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto);

        // Assert
        var updated = _dataProvider.Transactions.First(t => t.Key == transaction.Key);
        Assert.That(updated.Amount, Is.EqualTo(-150m));
    }

    #endregion

    #region DeleteTransactionAsync Tests

    [Test]
    public async Task DeleteTransactionAsync_ExistingTransaction_RemovesSuccessfully()
    {
        // Arrange
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "TestPayee");
        _dataProvider.Add(transaction);

        // Act
        await _transactionsFeature.DeleteTransactionAsync(transaction.Key);

        // Assert
        Assert.That(_dataProvider.Transactions.ToList(), Is.Empty);
    }

    [Test]
    public void DeleteTransactionAsync_NonExistentTransaction_ThrowsTransactionNotFoundException()
    {
        // Arrange
        var nonExistentKey = Guid.NewGuid();

        // Act & Assert
        Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _transactionsFeature.DeleteTransactionAsync(nonExistentKey));
    }

    [Test]
    public async Task DeleteTransactionAsync_OtherTenantTransaction_ThrowsTransactionNotFoundException()
    {
        // Arrange
        var otherTenantId = 999L;
        var otherTenantTransaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "OtherTenant");
        otherTenantTransaction.TenantId = otherTenantId;
        _dataProvider.Add(otherTenantTransaction);

        // Act & Assert
        Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _transactionsFeature.DeleteTransactionAsync(otherTenantTransaction.Key));

        // Verify transaction still exists
        Assert.That(_dataProvider.Transactions.ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DeleteTransactionAsync_MultipleTransactions_OnlyDeletesSpecified()
    {
        // Arrange
        var transaction1 = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Payee1");
        var transaction2 = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 200m, "Payee2");
        _dataProvider.Add(transaction1);
        _dataProvider.Add(transaction2);

        // Act
        await _transactionsFeature.DeleteTransactionAsync(transaction1.Key);

        // Assert
        var remaining = _dataProvider.Transactions.ToList();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].Key, Is.EqualTo(transaction2.Key));
    }

    #endregion

    #region Helper Methods

    private Transaction CreateTransaction(DateOnly date, decimal amount, string payee)
    {
        return new Transaction
        {
            Date = date,
            Amount = amount,
            Payee = payee,
            TenantId = _testTenantId,
            Key = Guid.NewGuid()
        };
    }

    #endregion
}
