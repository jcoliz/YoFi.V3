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
            Payee: "NewPayee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // Act
        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // Assert
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Payee, Is.EqualTo("NewPayee"));
        Assert.That(transactions[0].Amount, Is.EqualTo(150m));
        Assert.That(transactions[0].TenantId, Is.EqualTo(_testTenantId));

        // And: Result should match the created transaction
        Assert.That(result.Key, Is.EqualTo(transactions[0].Key));
        Assert.That(result.Payee, Is.EqualTo("NewPayee"));
        Assert.That(result.Amount, Is.EqualTo(150m));
        Assert.That(result.Date, Is.EqualTo(dto.Date));
    }

    [Test]
    public void AddTransactionAsync_ZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 0m,
            Payee: "TestPayee",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "   ",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: longPayee,
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: payee200,
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // Act
        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // Assert
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Payee, Is.EqualTo(payee200));

        // And: Result should contain the same payee
        Assert.That(result.Payee, Is.EqualTo(payee200));
    }

    [Test]
    public void AddTransactionAsync_DateOutOfRange_ThrowsArgumentException()
    {
        // Arrange - Date too far in the past (more than 50 years)
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-51)),
            Amount: 100m,
            Payee: "TestPayee",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "TestPayee",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: null!,
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "RefundPayee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // Act
        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // Assert
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Amount, Is.EqualTo(-100m));

        // And: Result should contain the negative amount
        Assert.That(result.Amount, Is.EqualTo(-100m));
    }

    [Test]
    public async Task AddTransactionAsync_AllFieldsPopulated_CreatesTransactionWithAllFields()
    {
        // Given: A transaction with all fields populated

        // When: Transaction is created with all fields
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 150m,
            Payee: "Test Payee",
            Memo: "This is a test memo",
            Source: "Chase Checking 1234",
            ExternalId: "TXN20241220-ABC123"
        );

        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // Then: All fields should be persisted
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Memo, Is.EqualTo("This is a test memo"));
        Assert.That(transactions[0].Source, Is.EqualTo("Chase Checking 1234"));
        Assert.That(transactions[0].ExternalId, Is.EqualTo("TXN20241220-ABC123"));

        // And: Result should contain all fields
        Assert.That(result.Memo, Is.EqualTo("This is a test memo"));
        Assert.That(result.Source, Is.EqualTo("Chase Checking 1234"));
        Assert.That(result.ExternalId, Is.EqualTo("TXN20241220-ABC123"));
    }

    [Test]
    public async Task AddTransactionAsync_NullableFieldsNull_CreatesTransactionSuccessfully()
    {
        // Given: A transaction with only required fields (nullable fields are null)

        // When: Transaction is created with minimal fields
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 150m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // Then: Transaction should be created with null nullable fields
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Memo, Is.Null);
        Assert.That(transactions[0].Source, Is.Null);
        Assert.That(transactions[0].ExternalId, Is.Null);

        // And: Result should contain null values for nullable fields
        Assert.That(result.Memo, Is.Null);
        Assert.That(result.Source, Is.Null);
        Assert.That(result.ExternalId, Is.Null);
    }

    [Test]
    public void AddTransactionAsync_MemoTooLong_ThrowsArgumentException()
    {
        // Given: A transaction with memo exceeding max length

        // When: Memo exceeds 1000 characters
        var longMemo = new string('A', 1001);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: longMemo,
            Source: null,
            ExternalId: null
        );

        // Then: ArgumentException should be thrown
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("memo cannot exceed 1000 characters"));
    }

    [Test]
    public async Task AddTransactionAsync_MemoExactly1000Characters_Succeeds()
    {
        // Given: A transaction with memo at exactly max length

        // When: Memo is exactly 1000 characters
        var memo1000 = new string('A', 1000);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: memo1000,
            Source: null,
            ExternalId: null
        );

        // Then: Transaction should be created successfully
        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // And: Memo should be stored correctly
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions[0].Memo, Is.EqualTo(memo1000));
        Assert.That(result.Memo, Is.EqualTo(memo1000));
    }

    [Test]
    public void AddTransactionAsync_SourceTooLong_ThrowsArgumentException()
    {
        // Given: A transaction with source exceeding max length

        // When: Source exceeds 200 characters
        var longSource = new string('A', 201);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: longSource,
            ExternalId: null
        );

        // Then: ArgumentException should be thrown
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("source cannot exceed 200 characters"));
    }

    [Test]
    public async Task AddTransactionAsync_SourceExactly200Characters_Succeeds()
    {
        // Given: A transaction with source at exactly max length

        // When: Source is exactly 200 characters
        var source200 = new string('A', 200);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: source200,
            ExternalId: null
        );

        // Then: Transaction should be created successfully
        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // And: Source should be stored correctly
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions[0].Source, Is.EqualTo(source200));
        Assert.That(result.Source, Is.EqualTo(source200));
    }

    [Test]
    public void AddTransactionAsync_ExternalIdTooLong_ThrowsArgumentException()
    {
        // Given: A transaction with externalId exceeding max length

        // When: ExternalId exceeds 100 characters
        var longExternalId = new string('A', 101);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: longExternalId
        );

        // Then: ArgumentException should be thrown
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.AddTransactionAsync(dto));

        Assert.That(ex!.Message, Does.Contain("externalId cannot exceed 100 characters"));
    }

    [Test]
    public async Task AddTransactionAsync_ExternalIdExactly100Characters_Succeeds()
    {
        // Given: A transaction with externalId at exactly max length

        // When: ExternalId is exactly 100 characters
        var externalId100 = new string('A', 100);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: externalId100
        );

        // Then: Transaction should be created successfully
        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // And: ExternalId should be stored correctly
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions[0].ExternalId, Is.EqualTo(externalId100));
        Assert.That(result.ExternalId, Is.EqualTo(externalId100));
    }

    [Test]
    public async Task AddTransactionAsync_DuplicateExternalId_AllowsCreation()
    {
        // Given: An existing transaction with an ExternalId
        var existingDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "First Transaction",
            Memo: null,
            Source: "Chase Checking 1234",
            ExternalId: "DUPLICATE-ID"
        );
        await _transactionsFeature.AddTransactionAsync(existingDto);

        // When: A second transaction with the same ExternalId is created
        var duplicateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 200m,
            Payee: "Second Transaction",
            Memo: null,
            Source: "Chase Checking 1234",
            ExternalId: "DUPLICATE-ID"
        );

        // Then: Transaction should be created successfully (API allows duplicates)
        var result = await _transactionsFeature.AddTransactionAsync(duplicateDto);

        // And: Both transactions should exist
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(2));
        Assert.That(transactions.Count(t => t.ExternalId == "DUPLICATE-ID"), Is.EqualTo(2));
    }

    [Test]
    public async Task AddTransactionAsync_SameExternalIdDifferentTenants_AllowsCreation()
    {
        // Given: A transaction in current tenant with an ExternalId
        var currentTenantDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Current Tenant Transaction",
            Memo: null,
            Source: null,
            ExternalId: "SHARED-ID"
        );
        await _transactionsFeature.AddTransactionAsync(currentTenantDto);

        // And: A transaction in a different tenant with the same ExternalId
        var otherTenantTransaction = CreateTransaction(
            DateOnly.FromDateTime(DateTime.Now),
            200m,
            "Other Tenant Transaction"
        );
        otherTenantTransaction.TenantId = 999L;
        otherTenantTransaction.ExternalId = "SHARED-ID";
        _dataProvider.Add(otherTenantTransaction);

        // Then: Both transactions should coexist successfully
        var allTransactions = _dataProvider.Transactions.ToList();
        Assert.That(allTransactions.Count(t => t.ExternalId == "SHARED-ID"), Is.EqualTo(2));
    }

    [Test]
    public async Task AddTransactionAsync_NullExternalId_AllowsCreation()
    {
        // Given: A manually entered transaction without ExternalId

        // When: Transaction is created without ExternalId
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Manual Entry",
            Memo: "Manually entered transaction",
            Source: null,
            ExternalId: null
        );

        var result = await _transactionsFeature.AddTransactionAsync(dto);

        // Then: Transaction should be created successfully
        var transactions = _dataProvider.Transactions.ToList();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].ExternalId, Is.Null);
        Assert.That(result.ExternalId, Is.Null);
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
            Payee: "UpdatedPayee",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "UpdatedPayee",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "UpdatedPayee",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "UpdatedPayee",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "UpdatedPayee",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: longPayee,
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "   ",
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: null!,
            Memo: null,
            Source: null,
            ExternalId: null
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
            Payee: "UpdatedPayee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // Act
        await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto);

        // Assert
        var updated = _dataProvider.Transactions.First(t => t.Key == transaction.Key);
        Assert.That(updated.Amount, Is.EqualTo(-150m));
    }

    [Test]
    public async Task UpdateTransactionAsync_UpdatesAllFields_SuccessfullyUpdates()
    {
        // Given: An existing transaction with initial values
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Original Payee");
        transaction.Memo = "Original memo";
        transaction.Source = "Original Source";
        transaction.ExternalId = "ORIGINAL-ID";
        _dataProvider.Add(transaction);

        // When: All fields are updated
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            Amount: 250m,
            Payee: "Updated Payee",
            Memo: "Updated memo",
            Source: "Updated Source",
            ExternalId: "UPDATED-ID"
        );

        var result = await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto);

        // Then: All fields should be updated
        var updated = _dataProvider.Transactions.First(t => t.Key == transaction.Key);
        Assert.That(updated.Payee, Is.EqualTo("Updated Payee"));
        Assert.That(updated.Amount, Is.EqualTo(250m));
        Assert.That(updated.Date, Is.EqualTo(DateOnly.FromDateTime(DateTime.Now.AddDays(1))));
        Assert.That(updated.Memo, Is.EqualTo("Updated memo"));
        Assert.That(updated.Source, Is.EqualTo("Updated Source"));
        Assert.That(updated.ExternalId, Is.EqualTo("UPDATED-ID"));

        // And: Result should reflect updated values
        Assert.That(result.Payee, Is.EqualTo("Updated Payee"));
        Assert.That(result.Memo, Is.EqualTo("Updated memo"));
        Assert.That(result.Source, Is.EqualTo("Updated Source"));
        Assert.That(result.ExternalId, Is.EqualTo("UPDATED-ID"));
    }

    [Test]
    public async Task UpdateTransactionAsync_NullFields_ClearsValues()
    {
        // Given: An existing transaction with all fields populated
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Original Payee");
        transaction.Memo = "Original memo";
        transaction.Source = "Original Source";
        transaction.ExternalId = "ORIGINAL-ID";
        _dataProvider.Add(transaction);

        // When: Optional fields are set to null
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Updated Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        var result = await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto);

        // Then: Optional fields should be cleared
        var updated = _dataProvider.Transactions.First(t => t.Key == transaction.Key);
        Assert.That(updated.Memo, Is.Null);
        Assert.That(updated.Source, Is.Null);
        Assert.That(updated.ExternalId, Is.Null);

        // And: Result should reflect null values
        Assert.That(result.Memo, Is.Null);
        Assert.That(result.Source, Is.Null);
        Assert.That(result.ExternalId, Is.Null);
    }

    [Test]
    public void UpdateTransactionAsync_MemoTooLong_ThrowsArgumentException()
    {
        // Given: An existing transaction
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Test Payee");
        _dataProvider.Add(transaction);

        // When: Update with memo exceeding 1000 characters
        var longMemo = new string('A', 1001);
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Updated Payee",
            Memo: longMemo,
            Source: null,
            ExternalId: null
        );

        // Then: ArgumentException should be thrown
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("memo cannot exceed 1000 characters"));
    }

    [Test]
    public void UpdateTransactionAsync_SourceTooLong_ThrowsArgumentException()
    {
        // Given: An existing transaction
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Test Payee");
        _dataProvider.Add(transaction);

        // When: Update with source exceeding 200 characters
        var longSource = new string('A', 201);
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Updated Payee",
            Memo: null,
            Source: longSource,
            ExternalId: null
        );

        // Then: ArgumentException should be thrown
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("source cannot exceed 200 characters"));
    }

    [Test]
    public void UpdateTransactionAsync_ExternalIdTooLong_ThrowsArgumentException()
    {
        // Given: An existing transaction
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Test Payee");
        _dataProvider.Add(transaction);

        // When: Update with externalId exceeding 100 characters
        var longExternalId = new string('A', 101);
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "Updated Payee",
            Memo: null,
            Source: null,
            ExternalId: longExternalId
        );

        // Then: ArgumentException should be thrown
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _transactionsFeature.UpdateTransactionAsync(transaction.Key, updateDto));

        Assert.That(ex!.Message, Does.Contain("externalId cannot exceed 100 characters"));
    }

    #endregion

    #region GetTransactionByKeyAsync Tests - New Fields

    [Test]
    public async Task GetTransactionByKeyAsync_ReturnsAllFields()
    {
        // Given: A transaction with all fields populated
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Test Payee");
        transaction.Memo = "Test memo";
        transaction.Source = "Chase Checking 1234";
        transaction.ExternalId = "TXN-123";
        _dataProvider.Add(transaction);

        // When: Transaction is retrieved by key
        var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);

        // Then: All fields should be returned
        Assert.That(result.Key, Is.EqualTo(transaction.Key));
        Assert.That(result.Payee, Is.EqualTo("Test Payee"));
        Assert.That(result.Amount, Is.EqualTo(100m));
        Assert.That(result.Memo, Is.EqualTo("Test memo"));
        Assert.That(result.Source, Is.EqualTo("Chase Checking 1234"));
        Assert.That(result.ExternalId, Is.EqualTo("TXN-123"));
    }

    [Test]
    public async Task GetTransactionByKeyAsync_NullableFieldsNull_ReturnsNullValues()
    {
        // Given: A transaction with only required fields
        var transaction = CreateTransaction(DateOnly.FromDateTime(DateTime.Now), 100m, "Test Payee");
        _dataProvider.Add(transaction);

        // When: Transaction is retrieved by key
        var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);

        // Then: Nullable fields should be null
        Assert.That(result.Memo, Is.Null);
        Assert.That(result.Source, Is.Null);
        Assert.That(result.ExternalId, Is.Null);
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
