using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Integration tests for TransactionsFeature.
/// </summary>
/// <remarks>
/// Tests TransactionsFeature methods with real ApplicationDbContext
/// to verify IDataProvider usage and navigation property loading.
/// </remarks>
[TestFixture]
public class TransactionsFeatureTests : FeatureTestBase
{
    private TransactionsFeature _feature = null!;
    private ITenantProvider _tenantProvider = null!;
    private Tenant _testTenant = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();

        // Create test tenant
        _testTenant = new Tenant
        {
            Name = "Test Tenant",
            Description = "Test tenant for feature tests"
        };
        _context.Tenants.Add(_testTenant);
        await _context.SaveChangesAsync();

        // Create tenant provider
        _tenantProvider = new TestTenantProvider { CurrentTenant = _testTenant };

        // Create feature with real dependencies
        _feature = new TransactionsFeature(_tenantProvider, _dataProvider);
    }

    #region Navigation Property Loading Tests (Critical - Splits Bug)

    [Test]
    public async Task GetTransactionByKeyAsync_WithSingleSplit_LoadsSplitsCollection()
    {
        // Given: Transaction with split in database
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Test Payee",
            Amount = 100.00m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var split = new Split
        {
            TransactionId = transaction.Id,
            Amount = 100.00m,
            Category = "Groceries",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Clear tracking to simulate fresh query (like production)
        _context.ChangeTracker.Clear();

        // When: Getting transaction through TransactionsFeature
        var result = await _feature.GetTransactionByKeyAsync(transaction.Key);

        // Then: Transaction should be found
        Assert.That(result, Is.Not.Null);

        // And: Category should come from split
        Assert.That(result.Category, Is.EqualTo("Groceries"),
            "Category should be loaded from splits collection");
    }

    [Test]
    public async Task GetTransactionByKeyAsync_WithMultipleSplits_LoadsAllSplits()
    {
        // Given: Transaction with multiple splits
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Grocery Store",
            Amount = 100.00m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var split1 = new Split
        {
            TransactionId = transaction.Id,
            Amount = 60.00m,
            Category = "Groceries",
            Order = 0
        };
        var split2 = new Split
        {
            TransactionId = transaction.Id,
            Amount = 40.00m,
            Category = "Household",
            Order = 1
        };
        _context.Splits.AddRange(split1, split2);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // When: Getting transaction through Feature
        var result = await _feature.GetTransactionByKeyAsync(transaction.Key);

        // Then: Category should come from first split (Order = 0)
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Category, Is.EqualTo("Groceries"));
    }

    [Test]
    public async Task GetTransactionByKeyAsync_WithoutSplits_ReturnsEmptyCategory()
    {
        // Given: Transaction without splits (edge case)
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "No Split Transaction",
            Amount = 100.00m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // When: Getting transaction through Feature
        var result = await _feature.GetTransactionByKeyAsync(transaction.Key);

        // Then: Category should be empty string
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Category, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task UpdateTransactionAsync_WithSplits_LoadsSplitsForUpdate()
    {
        // Given: Transaction with split exists
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Original Payee",
            Amount = 100.00m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var split = new Split
        {
            TransactionId = transaction.Id,
            Amount = 100.00m,
            Category = "Original Category",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // And: Update DTO
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 150.00m,
            Payee: "Updated Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Updated Category"
        );

        // When: Updating transaction through Feature
        var result = await _feature.UpdateTransactionAsync(transaction.Key, updateDto);

        // Then: Update should succeed
        Assert.That(result, Is.Not.Null);

        // And: Splits should be accessible during update logic
        Assert.That(result.Category, Is.EqualTo("Updated Category"));

        // And: Verify split was updated in database
        _context.ChangeTracker.Clear();
        var updatedTransaction = await _context.Transactions
            .Include(t => t.Splits)
            .FirstAsync(t => t.Key == transaction.Key);
        var updatedSplit = updatedTransaction.Splits.FirstOrDefault(s => s.Order == 0);
        Assert.That(updatedSplit, Is.Not.Null);
        Assert.That(updatedSplit!.Category, Is.EqualTo("Updated Category"));
        Assert.That(updatedSplit.Amount, Is.EqualTo(150.00m));
    }

    [Test]
    public async Task QuickEditTransactionAsync_PreservesExistingSplits()
    {
        // Given: Transaction with split
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Original Payee",
            Amount = 100.00m,
            Memo = "Original Memo",
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var split = new Split
        {
            TransactionId = transaction.Id,
            Amount = 100.00m,
            Category = "Food",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // When: Quick editing transaction (payee, memo, category only)
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "New Payee",
            Memo: "New Memo",
            Category: "Dining"
        );
        var result = await _feature.QuickEditTransactionAsync(transaction.Key, quickEditDto);

        // Then: Splits should still be loaded and updated
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Category, Is.EqualTo("Dining"));
        Assert.That(result.Payee, Is.EqualTo("New Payee"));
        Assert.That(result.Memo, Is.EqualTo("New Memo"));

        // And: Amount and Date should be preserved
        Assert.That(result.Amount, Is.EqualTo(100.00m));
        Assert.That(result.Date, Is.EqualTo(transaction.Date));
    }

    [Test]
    public async Task GetTransactionsAsync_WithSplits_LoadsSplitsForAllTransactions()
    {
        // Given: Multiple transactions with splits
        var tx1 = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Payee 1",
            Amount = 100m,
            TenantId = _testTenant.Id
        };
        var tx2 = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
            Payee = "Payee 2",
            Amount = 200m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.AddRange(tx1, tx2);
        await _context.SaveChangesAsync();

        _context.Splits.Add(new Split { TransactionId = tx1.Id, Amount = 100m, Category = "Category 1", Order = 0 });
        _context.Splits.Add(new Split { TransactionId = tx2.Id, Amount = 200m, Category = "Category 2", Order = 0 });
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // When: Getting all transactions through Feature
        var results = await _feature.GetTransactionsAsync();

        // Then: All transactions should have categories loaded
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(t => !string.IsNullOrEmpty(t.Category)), Is.True,
            "All transactions should have categories loaded from splits");

        var result1 = results.First(t => t.Payee == "Payee 1");
        Assert.That(result1.Category, Is.EqualTo("Category 1"));

        var result2 = results.First(t => t.Payee == "Payee 2");
        Assert.That(result2.Category, Is.EqualTo("Category 2"));
    }

    #endregion

    #region Business Logic Tests (Feature Behavior)

    [Test]
    public async Task GetTransactionsAsync_NoFilters_ReturnsAllTransactionsForTenant()
    {
        // Given: Multiple transactions in database
        var transaction1 = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Payee1",
            Amount = 100m,
            TenantId = _testTenant.Id
        };
        var transaction2 = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
            Payee = "Payee2",
            Amount = 200m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.AddRange(transaction1, transaction2);
        await _context.SaveChangesAsync();

        // When: Getting transactions
        var result = await _feature.GetTransactionsAsync();

        // Then: All tenant transactions should be returned
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(t => t.Payee), Contains.Item("Payee1"));
        Assert.That(result.Select(t => t.Payee), Contains.Item("Payee2"));
    }

    [Test]
    public async Task GetTransactionsAsync_WithFromDate_ReturnsFilteredTransactions()
    {
        // Given: Transactions on different dates
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        _context.Transactions.Add(new Transaction { Date = today, Payee = "Today", Amount = 100m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = yesterday, Payee = "Yesterday", Amount = 200m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = twoDaysAgo, Payee = "TwoDaysAgo", Amount = 300m, TenantId = _testTenant.Id });
        await _context.SaveChangesAsync();

        // When: Filtering by from date
        var result = await _feature.GetTransactionsAsync(fromDate: yesterday);

        // Then: Only transactions from yesterday onwards should be returned
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(t => t.Payee), Does.Not.Contain("TwoDaysAgo"));
    }

    [Test]
    public async Task GetTransactionsAsync_WithToDate_ReturnsFilteredTransactions()
    {
        // Given: Transactions on different dates
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        _context.Transactions.Add(new Transaction { Date = today, Payee = "Today", Amount = 100m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = yesterday, Payee = "Yesterday", Amount = 200m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = twoDaysAgo, Payee = "TwoDaysAgo", Amount = 300m, TenantId = _testTenant.Id });
        await _context.SaveChangesAsync();

        // When: Filtering by to date
        var result = await _feature.GetTransactionsAsync(toDate: yesterday);

        // Then: Only transactions up to yesterday should be returned
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(t => t.Payee), Does.Not.Contain("Today"));
    }

    [Test]
    public async Task GetTransactionsAsync_WithDateRange_ReturnsFilteredTransactions()
    {
        // Given: Transactions across multiple dates
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);
        var threeDaysAgo = today.AddDays(-3);

        _context.Transactions.Add(new Transaction { Date = today, Payee = "Today", Amount = 100m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = yesterday, Payee = "Yesterday", Amount = 200m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = twoDaysAgo, Payee = "TwoDaysAgo", Amount = 300m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = threeDaysAgo, Payee = "ThreeDaysAgo", Amount = 400m, TenantId = _testTenant.Id });
        await _context.SaveChangesAsync();

        // When: Filtering by date range
        var result = await _feature.GetTransactionsAsync(fromDate: twoDaysAgo, toDate: yesterday);

        // Then: Only transactions in range should be returned
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(t => t.Payee), Contains.Item("Yesterday"));
        Assert.That(result.Select(t => t.Payee), Contains.Item("TwoDaysAgo"));
    }

    [Test]
    public void GetTransactionsAsync_FromDateAfterToDate_ThrowsValidationException()
    {
        // Given: Invalid date range
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);

        // When: From date is after to date
        // Then: Should throw ValidationException
        var ex = Assert.ThrowsAsync<ValidationException>(async () =>
            await _feature.GetTransactionsAsync(fromDate: today, toDate: yesterday));

        Assert.That(ex!.ParameterName, Is.EqualTo("fromDate"));
        Assert.That(ex.Message, Does.Contain("From date cannot be later than to date"));
    }

    [Test]
    public async Task GetTransactionsAsync_OnlyReturnsCurrentTenantTransactions()
    {
        // Given: Transactions for different tenants
        var otherTenant = new Tenant { Name = "Other Tenant", Description = "Other" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();

        var currentTenantTransaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "CurrentTenant",
            Amount = 100m,
            TenantId = _testTenant.Id
        };
        var otherTenantTransaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "OtherTenant",
            Amount = 200m,
            TenantId = otherTenant.Id
        };

        _context.Transactions.AddRange(currentTenantTransaction, otherTenantTransaction);
        await _context.SaveChangesAsync();

        // When: Getting transactions
        var result = await _feature.GetTransactionsAsync();

        // Then: Only current tenant transactions should be returned
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().Payee, Is.EqualTo("CurrentTenant"));
    }

    [Test]
    public async Task GetTransactionsAsync_ReturnsTransactionsOrderedByDateDescending()
    {
        // Given: Transactions in random order
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        _context.Transactions.Add(new Transaction { Date = yesterday, Payee = "Middle", Amount = 200m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = twoDaysAgo, Payee = "Oldest", Amount = 300m, TenantId = _testTenant.Id });
        _context.Transactions.Add(new Transaction { Date = today, Payee = "Newest", Amount = 100m, TenantId = _testTenant.Id });
        await _context.SaveChangesAsync();

        // When: Getting transactions
        var result = await _feature.GetTransactionsAsync();

        // Then: Should be ordered by date descending
        var resultList = result.ToList();
        Assert.That(resultList[0].Payee, Is.EqualTo("Newest"));
        Assert.That(resultList[1].Payee, Is.EqualTo("Middle"));
        Assert.That(resultList[2].Payee, Is.EqualTo("Oldest"));
    }

    [Test]
    public async Task GetTransactionByKeyAsync_ExistingKey_ReturnsTransaction()
    {
        // Given: Transaction in database
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "TestPayee",
            Amount = 100m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // When: Getting transaction by key
        var result = await _feature.GetTransactionByKeyAsync(transaction.Key);

        // Then: Transaction should be returned
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Key, Is.EqualTo(transaction.Key));
        Assert.That(result.Payee, Is.EqualTo("TestPayee"));
        Assert.That(result.Amount, Is.EqualTo(100m));
    }

    [Test]
    public void GetTransactionByKeyAsync_NonExistentKey_ThrowsTransactionNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();

        // When: Getting transaction by non-existent key
        // Then: Should throw TransactionNotFoundException
        var ex = Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _feature.GetTransactionByKeyAsync(nonExistentKey));

        Assert.That(ex!.Message, Does.Contain(nonExistentKey.ToString()));
    }

    [Test]
    public async Task GetTransactionByKeyAsync_OtherTenantTransaction_ThrowsTransactionNotFoundException()
    {
        // Given: Transaction for different tenant
        var otherTenant = new Tenant { Name = "Other Tenant", Description = "Other" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();

        var otherTenantTransaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "OtherTenant",
            Amount = 200m,
            TenantId = otherTenant.Id
        };
        _context.Transactions.Add(otherTenantTransaction);
        await _context.SaveChangesAsync();

        // When: Trying to get other tenant's transaction
        // Then: Should throw TransactionNotFoundException
        Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _feature.GetTransactionByKeyAsync(otherTenantTransaction.Key));
    }

    [Test]
    public async Task AddTransactionAsync_ValidTransaction_AddsToDatabase()
    {
        // Given: Valid transaction data
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 150m,
            Payee: "NewPayee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: Adding transaction
        var result = await _feature.AddTransactionAsync(dto);

        // Then: Transaction should be added to database
        var transactions = await _context.Transactions.ToListAsync();
        Assert.That(transactions, Has.Count.EqualTo(1));
        Assert.That(transactions[0].Payee, Is.EqualTo("NewPayee"));
        Assert.That(transactions[0].Amount, Is.EqualTo(150m));
        Assert.That(transactions[0].TenantId, Is.EqualTo(_testTenant.Id));

        // And: Result should match the created transaction
        Assert.That(result.Key, Is.EqualTo(transactions[0].Key));
        Assert.That(result.Payee, Is.EqualTo("NewPayee"));
        Assert.That(result.Amount, Is.EqualTo(150m));
    }

    [Test]
    public async Task AddTransactionAsync_WithCategory_AutoCreatesSplitWithSanitizedCategory()
    {
        // Given: Transaction data with category needing sanitization
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 50.00m,
            Payee: "Test Store",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "  food : groceries  "
        );

        // When: Adding transaction
        var result = await _feature.AddTransactionAsync(dto);

        // Then: Category should be sanitized
        Assert.That(result.Category, Is.EqualTo("Food:Groceries"));

        // And: Split should be created in database
        var transaction = await _context.Transactions
            .Include(t => t.Splits)
            .FirstAsync(t => t.Key == result.Key);
        Assert.That(transaction.Splits, Has.Count.EqualTo(1));
        Assert.That(transaction.Splits.First().Category, Is.EqualTo("Food:Groceries"));
        Assert.That(transaction.Splits.First().Amount, Is.EqualTo(50.00m));
        Assert.That(transaction.Splits.First().Order, Is.EqualTo(0));
    }

    [Test]
    public async Task AddTransactionAsync_WithoutCategory_AutoCreatesSplitWithEmptyCategory()
    {
        // Given: Transaction data without category
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 75.00m,
            Payee: "Store Without Category",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: Adding transaction
        var result = await _feature.AddTransactionAsync(dto);

        // Then: Category should be empty
        Assert.That(result.Category, Is.EqualTo(string.Empty));

        // And: Split should be created with empty category
        var transaction = await _context.Transactions
            .Include(t => t.Splits)
            .FirstAsync(t => t.Key == result.Key);
        Assert.That(transaction.Splits, Has.Count.EqualTo(1));
        Assert.That(transaction.Splits.First().Category, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task UpdateTransactionAsync_ExistingTransaction_UpdatesSuccessfully()
    {
        // Given: Existing transaction
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "OriginalPayee",
            Amount = 100m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            Amount: 250m,
            Payee: "UpdatedPayee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: Updating transaction
        var result = await _feature.UpdateTransactionAsync(transaction.Key, updateDto);

        // Then: Transaction should be updated
        Assert.That(result.Payee, Is.EqualTo("UpdatedPayee"));
        Assert.That(result.Amount, Is.EqualTo(250m));

        var updated = await _context.Transactions.FirstAsync(t => t.Key == transaction.Key);
        Assert.That(updated.Payee, Is.EqualTo("UpdatedPayee"));
        Assert.That(updated.Amount, Is.EqualTo(250m));
    }

    [Test]
    public void UpdateTransactionAsync_NonExistentTransaction_ThrowsTransactionNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: "UpdatedPayee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: Updating non-existent transaction
        // Then: Should throw TransactionNotFoundException
        Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _feature.UpdateTransactionAsync(nonExistentKey, updateDto));
    }

    [Test]
    public async Task DeleteTransactionAsync_ExistingTransaction_RemovesSuccessfully()
    {
        // Given: Existing transaction
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "TestPayee",
            Amount = 100m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // When: Deleting transaction
        await _feature.DeleteTransactionAsync(transaction.Key);

        // Then: Transaction should be removed
        var remaining = await _context.Transactions.ToListAsync();
        Assert.That(remaining, Is.Empty);
    }

    [Test]
    public void DeleteTransactionAsync_NonExistentTransaction_ThrowsTransactionNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();

        // When: Deleting non-existent transaction
        // Then: Should throw TransactionNotFoundException
        Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _feature.DeleteTransactionAsync(nonExistentKey));
    }

    #endregion

    #region Batch Operations Tests

    [Test]
    public async Task AddTransactionsAsync_MultipleTransactions_AddsAllToDatabase()
    {
        // Given: Multiple transaction DTOs
        var transactions = new List<TransactionEditDto>
        {
            new(DateOnly.FromDateTime(DateTime.Now), 100m, "Payee1", null, null, null, "Category1"),
            new(DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), 200m, "Payee2", null, null, null, "Category2"),
            new(DateOnly.FromDateTime(DateTime.Now.AddDays(-2)), 300m, "Payee3", null, null, null, null)
        };

        // When: Adding transactions in batch
        var results = await _feature.AddTransactionsAsync(transactions);

        // Then: All transactions should be added
        Assert.That(results, Has.Count.EqualTo(3));
        var dbTransactions = await _context.Transactions.ToListAsync();
        Assert.That(dbTransactions, Has.Count.EqualTo(3));

        // And: All should have splits created
        var allSplits = await _context.Splits.ToListAsync();
        Assert.That(allSplits, Has.Count.EqualTo(3));
    }

    #endregion
}

/// <summary>
/// Test implementation of ITenantProvider for testing.
/// </summary>
file class TestTenantProvider : ITenantProvider
{
    public required Tenant CurrentTenant { get; init; }
}
