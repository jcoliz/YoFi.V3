using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ClosedXML.Excel;
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

    [Test]
    public async Task GetCategorySuggestionsAsync_CurrentTenantOnlyReturnsOrderedDistinctPrefixMatches()
    {
        // Given: Matching, duplicate, empty, hierarchical, and other-tenant split categories
        var otherTenant = new Tenant
        {
            Name = "Other Tenant",
            Description = "Category suggestion isolation test tenant"
        };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();

        var currentTransaction = new Transaction
        {
            Date = new(2026, 2, 1),
            Payee = "Category source",
            Amount = -100m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -40m, Category = "Groceries", Order = 0 },
                new Split { Amount = -30m, Category = "groceries", Order = 1 },
                new Split { Amount = -20m, Category = "Garden", Order = 2 },
                new Split { Amount = -10m, Category = "Home:Garden", Order = 3 }
            ]
        };
        var emptyCategoryTransaction = new Transaction
        {
            Date = new(2026, 2, 2),
            Payee = "Uncategorized source",
            Amount = -5m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -5m, Category = "", Order = 0 }
            ]
        };
        var otherTenantTransaction = new Transaction
        {
            Date = new(2026, 2, 3),
            Payee = "Other tenant source",
            Amount = -10m,
            TenantId = otherTenant.Id,
            Splits =
            [
                new Split { Amount = -10m, Category = "Gas", Order = 0 }
            ]
        };

        _context.Transactions.AddRange(
            currentTransaction,
            emptyCategoryTransaction,
            otherTenantTransaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // When: A sanitized hierarchical prefix is requested
        var result = await _feature.GetCategorySuggestionsAsync("home : g");

        // Then: The real provider returns only the matching tenant category
        Assert.That(result, Is.EqualTo(new[] { "Home:Garden" }));

        // And: Duplicate categories from the current tenant are de-duplicated case-insensitively
        var groceries = await _feature.GetCategorySuggestionsAsync("gRo");
        Assert.That(groceries, Is.EqualTo(new[] { "Groceries" }));

        // And: Empty and other-tenant categories are excluded
        Assert.That(groceries, Does.Not.Contain(""));
        Assert.That(groceries, Does.Not.Contain("Gas"));
    }

    [Test]
    public async Task AddSplitAsync_WithOneExistingSplit_RebalancesAndPreservesTransaction()
    {
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Maverick Markets",
            Amount = -100m,
            Memo = "Transaction memo",
            Source = "Imported account",
            ExternalId = "bank-transaction-1",
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split
                {
                    Amount = -100m,
                    Category = "Fuel",
                    Memo = "Original split memo",
                    Order = 0
                }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var originalSplitKey = transaction.Splits.Single().Key;
        _context.ChangeTracker.Clear();

        var result = await _feature.AddSplitAsync(
            transaction.Key,
            new SplitEditDto(-10m, "  snacks  ", "I was hungry"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Amount, Is.EqualTo(-100m));
            Assert.That(result.Memo, Is.EqualTo("Transaction memo"));
            Assert.That(result.Source, Is.EqualTo("Imported account"));
            Assert.That(result.ExternalId, Is.EqualTo("bank-transaction-1"));
            Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -90m, -10m }));
            Assert.That(result.Splits.Select(split => split.Category), Is.EqualTo(new[] { "Fuel", "Snacks" }));
            Assert.That(result.Splits.Select(split => split.Memo),
                Is.EqualTo(new[] { "Original split memo", "I was hungry" }));
            Assert.That(result.Splits.Select(split => split.Order), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(result.Splits[0].Key, Is.EqualTo(originalSplitKey));
        }

        _context.ChangeTracker.Clear();
        var persisted = await _context.Transactions.Include(t => t.Splits).SingleAsync();
        Assert.That(persisted.Splits.Sum(split => split.Amount), Is.EqualTo(persisted.Amount));
    }

    [Test]
    public async Task AddSplitAsync_WithMultipleExistingSplits_RebalancesFirstAndPreservesOthers()
    {
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Super Duper Mart",
            Amount = -250m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -125m, Category = "Groceries", Order = 0 },
                new Split { Amount = -50m, Category = "Supplies", Order = 1 },
                new Split
                {
                    Amount = -75m,
                    Category = "Healthcare:Prescriptions",
                    Memo = "Can't forget my meds",
                    Order = 2
                }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var originalSplits = transaction.Splits.OrderBy(split => split.Order).ToArray();
        _context.ChangeTracker.Clear();

        var result = await _feature.AddSplitAsync(
            transaction.Key,
            new SplitEditDto(-25m, "Dining Out", "Also had dinner there"));

        Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -100m, -50m, -75m, -25m }));
        Assert.That(result.Splits.Select(split => split.Category),
            Is.EqualTo(new[] { "Groceries", "Supplies", "Healthcare:Prescriptions", "Dining Out" }));
        Assert.That(result.Splits.Select(split => split.Memo),
            Is.EqualTo(new string?[] { null, null, "Can't forget my meds", "Also had dinner there" }));
        Assert.That(result.Splits.Select(split => split.Order), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        Assert.That(result.Splits[0].Key, Is.EqualTo(originalSplits[0].Key));
        Assert.That(result.Splits[1].Key, Is.EqualTo(originalSplits[1].Key));
        Assert.That(result.Splits[2].Key, Is.EqualTo(originalSplits[2].Key));
        Assert.That(result.Splits.Sum(split => split.Amount), Is.EqualTo(result.Amount));
    }

    [Test]
    public async Task AddSplitAsync_WithUnbalancedExistingSplits_RestoresBalance()
    {
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Previously unbalanced",
            Amount = -100m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -80m, Category = "Fuel", Order = 0 },
                new Split { Amount = -10m, Category = "Snacks", Order = 1 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _feature.AddSplitAsync(
            transaction.Key,
            new SplitEditDto(-10m, "Parking", null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -80m, -10m, -10m }));
            Assert.That(result.SplitsTotal, Is.EqualTo(-100m));
            Assert.That(result.IsBalanced, Is.True);
        }
    }

    [Test]
    public async Task AddSplitAsync_WithoutExistingSplit_ThrowsValidationException()
    {
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Missing split",
            Amount = -100m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.AddSplitAsync(transaction.Key, new SplitEditDto(-10m, "Fuel", null)));

        Assert.That(exception!.Message, Does.Contain("existing split"));
        Assert.That(await _context.Splits.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task AddSplitAsync_NonExistentTransaction_ThrowsTransactionNotFoundException()
    {
        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            _feature.AddSplitAsync(Guid.NewGuid(), new SplitEditDto(-10m, "Fuel", null)));
    }

    #region Navigation Property Loading Tests (Critical - Splits Bug)

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Replacement")]
    public async Task QuickEditTransactionAsync_WithMultipleSplits_PreservesAllocations(string? category)
    {
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Original",
            Memo = "Original memo",
            Amount = 100m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = 60m, Category = "Groceries", Order = 0 },
                new Split { Amount = 40m, Category = "Household", Order = 1 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var input = new TransactionQuickEditDto("Updated", "Updated memo", category);

        if (category == null)
        {
            var result = await _feature.QuickEditTransactionAsync(transaction.Key, input);
            Assert.That(result.Category, Is.Empty);
        }
        else
        {
            await Assert.ThrowsAsync<ValidationException>(() => _feature.QuickEditTransactionAsync(transaction.Key, input));
        }

        _context.ChangeTracker.Clear();
        var persisted = await _context.Transactions.Include(t => t.Splits).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(persisted.Payee, Is.EqualTo(category == null ? "Updated" : "Original"));
            Assert.That(persisted.Memo, Is.EqualTo(category == null ? "Updated memo" : "Original memo"));
            Assert.That(persisted.Splits.OrderBy(s => s.Order).Select(s => s.Category),
                Is.EqualTo(new[] { "Groceries", "Household" }));
            Assert.That(persisted.Splits.OrderBy(s => s.Order).Select(s => s.Amount),
                Is.EqualTo(new[] { 60m, 40m }));
            Assert.That(persisted.Amount, Is.EqualTo(100m));
        }
    }

    [TestCase(0, false, "")]
    [TestCase(1, false, "Groceries")]
    [TestCase(2, true, "")]
    [TestCase(3, true, "")]
    public async Task GetTransactionsAsync_ReportsMultipleSplitsAndOnlySingleSplitCategory(
        int splitCount, bool hasMultipleSplits, string category)
    {
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Split indicator",
            Amount = 100m,
            TenantId = _testTenant.Id
        };
        for (var order = 0; order < splitCount; order++)
        {
            transaction.Splits.Add(new Split
            {
                Amount = 100m / splitCount,
                Category = order == 0 ? "Groceries" : "Household",
                Order = order
            });
        }
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = (await _feature.GetTransactionsAsync()).Items.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Key, Is.EqualTo(transaction.Key));
            Assert.That(result.HasMultipleSplits, Is.EqualTo(hasMultipleSplits));
            Assert.That(result.Category, Is.EqualTo(category));
        }
    }

    [Test]
    public async Task GetTransactionsAsync_WithUnbalancedSplits_ProjectsBalanceStatus()
    {
        // Given: Transactions whose split totals are balanced and off by one cent
        var balanced = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Balanced",
            Amount = -100m,
            TenantId = _testTenant.Id,
            Splits = [new Split { Amount = -100m, Category = "Groceries", Order = 0 }]
        };
        var unbalanced = new Transaction
        {
            Date = new(2026, 1, 2),
            Payee = "Unbalanced",
            Amount = -100m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -90m, Category = "Groceries", Order = 0 },
                new Split { Amount = -9.99m, Category = "Household", Order = 1 }
            ]
        };
        _context.Transactions.AddRange(balanced, unbalanced);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // When: Getting the paginated transaction list
        var results = (await _feature.GetTransactionsAsync()).Items;

        // Then: Balance status is projected for every row
        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Single(result => result.Key == balanced.Key).IsBalanced, Is.True);
            Assert.That(results.Single(result => result.Key == unbalanced.Key).IsBalanced, Is.False);
        }
    }

    [Test]
    public async Task GetTransactionByKeyAsync_WithUnbalancedSingleSplit_ProjectsBalanceStatus()
    {
        // Given: A transaction with one split whose persisted amount does not match
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Unbalanced single split",
            Amount = -100m,
            TenantId = _testTenant.Id,
            Splits = [new Split { Amount = -99.99m, Category = "Groceries", Order = 0 }]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // When: Getting transaction details
        var result = await _feature.GetTransactionByKeyAsync(transaction.Key);

        // Then: The aggregate includes the split omitted from the display collection
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Splits, Is.Empty);
            Assert.That(result.SplitsTotal, Is.EqualTo(-99.99m));
            Assert.That(result.IsBalanced, Is.False);
        }
    }

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
        Assert.That(result.Splits, Is.Empty);
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
            Memo = "Household items",
            Order = 1
        };
        _context.Splits.AddRange(split2, split1);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // When: Getting transaction through Feature
        var result = await _feature.GetTransactionByKeyAsync(transaction.Key);

        // Then: Category is blank and the full allocations are returned in display order.
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Category, Is.Empty);
        Assert.That(result.Splits, Is.EqualTo(new[]
        {
            new TransactionSplitDto(split1.Key, 60m, "Groceries", null, 0),
            new TransactionSplitDto(split2.Key, 40m, "Household", "Household items", 1)
        }));
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
        Assert.That(result.Splits, Is.Empty);
    }

    [Test]
    public async Task UpdateTransactionAsync_WithSplitChange_ReturnsUpdatedBalanceStatus()
    {
        // Given: An unbalanced transaction with multiple persisted splits
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Amount change",
            Amount = -100m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -80m, Category = "Groceries", Order = 0 },
                new Split { Amount = -10m, Category = "Household", Order = 1 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // When: Updating the authoritative transaction amount
        var result = await _feature.UpdateTransactionAsync(
            transaction.Key,
            new TransactionEditDto(transaction.Date, -90m, transaction.Payee, null, null, null, null));

        // Then: The committed response contains the refreshed aggregate and status
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SplitsTotal, Is.EqualTo(-90m));
            Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -80m, -10m }));
            Assert.That(result.IsBalanced, Is.True);
        }
    }

    [TestCase(-10001, 0, false)]
    [TestCase(-9999, 0, false)]
    [TestCase(-10000, 0, false)]
    [TestCase(10001, 0, false)]
    [TestCase(9999, 0, false)]
    [TestCase(-10001, 4, false)]
    [TestCase(-10001, 4, true)]
    [TestCase(-10000, 4, true)]
    public async Task UpdateTransactionAsync_WithMultipleSplits_RebalancesFirstAndPreservesFields(
        int newAmountCents, int firstOrder, bool initiallyUnbalanced)
    {
        var originalAmount = newAmountCents < 0 ? -100m : 100m;
        var transaction = new Transaction
        {
            Date = new(2026, 1, 1),
            Payee = "Split transaction",
            Amount = originalAmount,
            Memo = "Transaction memo",
            Source = "Imported account",
            ExternalId = "bank-transaction-2290",
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split
                {
                    Amount = originalAmount * (initiallyUnbalanced ? 0.5m : 0.6m),
                    Category = "Groceries",
                    Memo = "First split memo",
                    Order = firstOrder
                },
                new Split
                {
                    Amount = originalAmount * 0.3m,
                    Category = "Household",
                    Memo = "Second split memo",
                    Order = firstOrder + 4
                },
                new Split
                {
                    Amount = originalAmount * 0.1m,
                    Category = "Supplies",
                    Memo = "Third split memo",
                    Order = firstOrder + 8
                }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var original = await _feature.GetTransactionByKeyAsync(transaction.Key);
        var newAmount = newAmountCents / 100m;
        var updateDto = new TransactionEditDto(
            original.Date, newAmount, original.Payee, original.Memo,
            original.Source, original.ExternalId, null);
        var expectedSplits = original.Splits.Select((split, index) => index == 0
            ? split with { Amount = newAmount - original.Splits.Skip(1).Sum(s => s.Amount) }
            : split).ToArray();

        var result = await _feature.UpdateTransactionAsync(transaction.Key, updateDto);
        var expectedDetail = original with
        {
            Amount = newAmount,
            SplitsTotal = newAmount,
            IsBalanced = true
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Splits, Is.EqualTo(expectedSplits));
            Assert.That(result with { Splits = original.Splits }, Is.EqualTo(expectedDetail));
            Assert.That(result.Splits.Sum(split => split.Amount), Is.EqualTo(newAmount));
        }

        _context.ChangeTracker.Clear();
        var persisted = await _feature.GetTransactionByKeyAsync(transaction.Key);
        Assert.That(persisted.Splits, Is.EqualTo(expectedSplits));
        Assert.That(persisted with { Splits = original.Splits }, Is.EqualTo(expectedDetail));
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("Groceries")]
    [TestCase("Replacement")]
    public async Task UpdateTransactionAsync_WithMultipleSplitsAndCategory_RejectsWithoutChanges(string category)
    {
        var created = await _feature.AddTransactionAsync(new TransactionEditDto(
            new DateOnly(2026, 1, 1), -100m, "Original payee", "Original memo", null, null, "Groceries"));
        var original = await _feature.AddSplitAsync(created.Key, new SplitEditDto(-40m, "Household", "Split memo"));
        _context.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.UpdateTransactionAsync(original.Key, new TransactionEditDto(
                original.Date, -100.01m, "Changed payee", "Changed memo", null, null, category)));

        Assert.That(exception!.Message, Does.Contain("Category cannot be changed"));
        var tracked = await _feature.GetTransactionByKeyAsync(original.Key);
        Assert.That(tracked with { Splits = original.Splits }, Is.EqualTo(original));
        Assert.That(tracked.Splits, Is.EqualTo(original.Splits));
        _context.ChangeTracker.Clear();
        var persisted = await _feature.GetTransactionByKeyAsync(original.Key);
        Assert.That(persisted with { Splits = original.Splits }, Is.EqualTo(original));
        Assert.That(persisted.Splits, Is.EqualTo(original.Splits));
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
        Assert.That(results.Items, Has.Count.EqualTo(2));
        Assert.That(results.Items.All(t => !string.IsNullOrEmpty(t.Category)), Is.True,
            "All transactions should have categories loaded from splits");

        var result1 = results.Items.First(t => t.Payee == "Payee 1");
        Assert.That(result1.Category, Is.EqualTo("Category 1"));

        var result2 = results.Items.First(t => t.Payee == "Payee 2");
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
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(t => t.Payee), Contains.Item("Payee1"));
        Assert.That(result.Items.Select(t => t.Payee), Contains.Item("Payee2"));
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
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(t => t.Payee), Does.Not.Contain("TwoDaysAgo"));
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
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(t => t.Payee), Does.Not.Contain("Today"));
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
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(t => t.Payee), Contains.Item("Yesterday"));
        Assert.That(result.Items.Select(t => t.Payee), Contains.Item("TwoDaysAgo"));
    }

    [Test]
    public async Task GetTransactionsAsync_FromDateAfterToDate_ThrowsValidationException()
    {
        // Given: Invalid date range
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);

        // When: From date is after to date
        // Then: Should throw ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(async () =>
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
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items.First().Payee, Is.EqualTo("CurrentTenant"));
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
        var resultList = result.Items.ToList();
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
    public async Task GetTransactionByKeyAsync_NonExistentKey_ThrowsTransactionNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();

        // When: Getting transaction by non-existent key
        // Then: Should throw TransactionNotFoundException
        var ex = await Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
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
        await Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
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
        Assert.That(transaction.Splits.First().Order, Is.Zero);
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
    public async Task UpdateTransactionAsync_NonExistentTransaction_ThrowsTransactionNotFoundException()
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
        await Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
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
    public async Task DeleteTransactionAsync_NonExistentTransaction_ThrowsTransactionNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();

        // When: Deleting non-existent transaction
        // Then: Should throw TransactionNotFoundException
        await Assert.ThrowsAsync<TransactionNotFoundException>(async () =>
            await _feature.DeleteTransactionAsync(nonExistentKey));
    }

    #endregion

    #region Upload Splits Tests

    [Test]
    public async Task UploadSplitsAsync_ValidWorkbook_AppendsRowsAndBalances()
    {
        // Given: A transaction with an existing first split and stable fields
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 2, 10),
            Payee = "Upload payee",
            Amount = -100.25m,
            Memo = "Transaction memo",
            Source = "Test bank",
            ExternalId = "upload-1",
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -60m, Category = "Existing", Memo = "Keep me", Order = 4 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var originalKey = transaction.Splits.Single().Key;
        _context.ChangeTracker.Clear();

        var workbook = SplitWorkbook(
            ("Category", "Amount", "Memo"),
            ("  groceries  ", "12.34", "  weekly buy  "),
            ("Dining: OUT", "-52.59", "Lunch"));

        // When: Uploading validated rows
        var result = await _feature.UploadSplitsAsync(transaction.Key, workbook);

        // Then: Rows append in source order and the first split absorbs the remainder
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Date, Is.EqualTo(transaction.Date));
            Assert.That(result.Payee, Is.EqualTo("Upload payee"));
            Assert.That(result.Amount, Is.EqualTo(-100.25m));
            Assert.That(result.Memo, Is.EqualTo("Transaction memo"));
            Assert.That(result.Source, Is.EqualTo("Test bank"));
            Assert.That(result.ExternalId, Is.EqualTo("upload-1"));
            Assert.That(result.Splits.Select(split => split.Amount),
                Is.EqualTo(new[] { -60m, 12.34m, -52.59m }));
            Assert.That(result.Splits.Select(split => split.Category),
                Is.EqualTo(new[] { "Existing", "Groceries", "Dining:OUT" }));
            Assert.That(result.Splits.Select(split => split.Memo),
                Is.EqualTo(new string?[] { "Keep me", "weekly buy", "Lunch" }));
            Assert.That(result.Splits.Select(split => split.Order), Is.EqualTo(new[] { 4, 5, 6 }));
            Assert.That(result.Splits[0].Key, Is.EqualTo(originalKey));
            Assert.That(result.SplitsTotal, Is.EqualTo(-100.25m));
            Assert.That(result.IsBalanced, Is.True);
        }

        _context.ChangeTracker.Clear();
        var persisted = await _context.Transactions.Include(item => item.Splits)
            .SingleAsync(item => item.Key == transaction.Key);
        Assert.That(persisted.Splits.OrderBy(split => split.Order).Select(split => split.Amount),
            Is.EqualTo(new[] { -60m, 12.34m, -52.59m }));
        Assert.That(persisted.Splits.Sum(split => split.Amount), Is.EqualTo(persisted.Amount));
    }

    [Test]
    public async Task UploadSplitsAsync_ImportedTotalBalancesWithoutFirstSplit_RemovesZeroAllocation()
    {
        // Given: One existing split that will be fully replaced by uploaded allocations
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 3, 1),
            Payee = "Exact balance",
            Amount = -100m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -100m, Category = "Old", Order = 7 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var originalKey = transaction.Splits.Single().Key;
        _context.ChangeTracker.Clear();

        var workbook = SplitWorkbook(
            ("Category", "Amount", "Memo"),
            ("Groceries", "-60", null),
            ("Fuel", "-40", null));

        // When: Uploading rows whose total equals the transaction amount
        var result = await _feature.UploadSplitsAsync(transaction.Key, workbook);

        // Then: The balancing split is removed and imported split orders are normalized
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -60m, -40m }));
            Assert.That(result.Splits.Select(split => split.Category), Is.EqualTo(new[] { "Groceries", "Fuel" }));
            Assert.That(result.Splits.Select(split => split.Order), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(result.Splits.Select(split => split.Key), Has.None.EqualTo(originalKey));
            Assert.That(result.IsBalanced, Is.True);
        }

        _context.ChangeTracker.Clear();
        var persisted = await _context.Splits
            .Where(split => split.TransactionId == transaction.Id)
            .OrderBy(split => split.Order)
            .ToListAsync();
        Assert.That(persisted.Select(split => split.Order), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(persisted.Any(split => split.Key == originalKey), Is.False);
        Assert.That(persisted.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task UploadSplitsAsync_ZeroRowsAndAdditionalWorksheet_ImportsOnlyFirstSheetNonZeroRows()
    {
        // Given: A transaction and a workbook with zero rows plus a second worksheet
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 3, 2),
            Payee = "Mixed sheets",
            Amount = -30m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -20m, Category = "Existing", Order = 2 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var workbook = SplitWorkbookWithSecondSheet(
            ("Category", "Amount", "Memo"),
            ("Ignored zero", "0", null),
            ("Only first", "-10", "Imported"));

        // When: Uploading the workbook
        var result = await _feature.UploadSplitsAsync(transaction.Key, workbook);

        // Then: Later worksheets and zero rows do not affect imported state or ordering
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -20m, -10m }));
            Assert.That(result.Splits.Select(split => split.Category),
                Is.EqualTo(new[] { "Existing", "Only First" }));
            Assert.That(result.Splits.Select(split => split.Order), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(result.Splits.Last().Memo, Is.EqualTo("Imported"));
            Assert.That(result.IsBalanced, Is.True);
        }
    }

    [Test]
    public async Task UploadSplitsAsync_MemoMatchesLaterWorksheet_ImportsOnlyMatchedSheet()
    {
        // Given: A transaction memo that matches a later workbook sheet
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 3, 3),
            Payee = "Memo matched upload",
            Amount = -100m,
            Memo = "  Paystub  ",
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -20m, Category = "Existing", Order = 1 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var workbook = SplitWorkbookWithMemoSheet(
            "  pAyStUb ",
            ("Category", "Amount", "Memo"),
            ("Salary", "-40", "Matched sheet"));

        // When: Uploading the workbook
        var result = await _feature.UploadSplitsAsync(transaction.Key, workbook);

        // Then: Only the memo-matched sheet contributes allocations
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Memo, Is.EqualTo("  Paystub  "));
            Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -60m, -40m }));
            Assert.That(result.Splits.Select(split => split.Category),
                Is.EqualTo(new[] { "Existing", "Salary" }));
            Assert.That(result.Splits.Last().Memo, Is.EqualTo("Matched sheet"));
            Assert.That(result.SplitsTotal, Is.EqualTo(-100m));
            Assert.That(result.IsBalanced, Is.True);
        }

        _context.ChangeTracker.Clear();
        var persisted = await _context.Transactions.Include(item => item.Splits)
            .SingleAsync(item => item.Key == transaction.Key);
        Assert.That(persisted.Splits.Sum(split => split.Amount), Is.EqualTo(persisted.Amount));
        Assert.That(persisted.Splits.Select(split => split.Category),
            Has.None.EqualTo("Ignored"));
    }

    [Test]
    public async Task UploadSplitsAsync_InvalidSecondRow_LeavesTransactionUnchanged()
    {
        // Given: A transaction and a workbook with two invalid second-row fields
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 3, 3),
            Payee = "Invalid upload",
            Amount = -50m,
            TenantId = _testTenant.Id,
            Splits =
            [
                new Split { Amount = -50m, Category = "Original", Memo = "Original memo", Order = 3 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var original = await _feature.GetTransactionByKeyAsync(transaction.Key);
        _context.ChangeTracker.Clear();

        var workbook = SplitWorkbook(
            ("Category", "Amount", "Memo"),
            ("Valid", "10", "Valid memo"),
            (new string('x', 201), "20", new string('m', 501)));

        // When: The workbook validation encounters an invalid second row
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.UploadSplitsAsync(transaction.Key, workbook));

        // Then: Validation is grouped and persisted state is unchanged
        Assert.That(exception!.Title, Is.EqualTo("The split workbook failed validation."));
        Assert.That(exception.Message, Is.EqualTo(
            "Correct the workbook errors and upload it again. No splits were changed."));
        Assert.That(exception.Errors.Keys, Contains.Item("Row 3: Category"));
        Assert.That(exception.Errors.Keys, Contains.Item("Row 3: Memo"));
        Assert.That(exception.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("Category cannot exceed"));
        Assert.That(exception.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("Memo cannot exceed"));

        _context.ChangeTracker.Clear();
        Assert.That(await _feature.GetTransactionByKeyAsync(transaction.Key), Is.EqualTo(original));
        Assert.That(await _context.Splits.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task UploadSplitsAsync_TransactionInAnotherTenant_ThrowsNotFoundWithoutChanges()
    {
        // Given: A transaction owned by another tenant
        var otherTenant = new Tenant { Name = "Other tenant", Description = "Upload isolation" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 3, 4),
            Payee = "Other tenant transaction",
            Amount = -50m,
            TenantId = otherTenant.Id,
            Splits =
            [
                new Split { Amount = -50m, Category = "Original", Order = 0 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var workbook = SplitWorkbook(
            ("Category", "Amount", "Memo"),
            ("Imported", "-25", null));
        _context.ChangeTracker.Clear();

        // When: The current tenant attempts to upload splits
        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            _feature.UploadSplitsAsync(transaction.Key, workbook));

        // Then: The other tenant's transaction remains unchanged
        _context.ChangeTracker.Clear();
        var persisted = await _context.Transactions.Include(item => item.Splits)
            .SingleAsync(item => item.Key == transaction.Key);
        Assert.That(persisted.Splits.Single().Amount, Is.EqualTo(-50m));
        Assert.That(persisted.Splits.Single().Category, Is.EqualTo("Original"));
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

    private static byte[] SplitWorkbook(
        (string Category, string Amount, string? Memo) headers,
        params (string Category, string Amount, string? Memo)[] rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Splits");
        worksheet.Cell(1, 1).Value = headers.Category;
        worksheet.Cell(1, 2).Value = headers.Amount;
        worksheet.Cell(1, 3).Value = headers.Memo;

        for (var index = 0; index < rows.Length; index++)
        {
            var rowNumber = index + 2;
            worksheet.Cell(rowNumber, 1).Value = rows[index].Category;
            worksheet.Cell(rowNumber, 2).Value = rows[index].Amount;
            if (rows[index].Memo != null)
            {
                worksheet.Cell(rowNumber, 3).Value = rows[index].Memo;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] SplitWorkbookWithSecondSheet(
        (string Category, string Amount, string? Memo) headers,
        params (string Category, string Amount, string? Memo)[] rows)
    {
        using var workbook = new XLWorkbook();
        var first = workbook.AddWorksheet("Splits");
        first.Cell(1, 1).Value = headers.Category;
        first.Cell(1, 2).Value = headers.Amount;
        first.Cell(1, 3).Value = headers.Memo;
        for (var index = 0; index < rows.Length; index++)
        {
            var rowNumber = index + 2;
            first.Cell(rowNumber, 1).Value = rows[index].Category;
            first.Cell(rowNumber, 2).Value = rows[index].Amount;
            if (rows[index].Memo != null)
            {
                first.Cell(rowNumber, 3).Value = rows[index].Memo;
            }
        }

        var second = workbook.AddWorksheet("Later");
        second.Cell(1, 1).Value = "Category";
        second.Cell(1, 2).Value = "Amount";
        second.Cell(2, 1).Value = "Later sheet";
        second.Cell(2, 2).Value = "-999";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] SplitWorkbookWithMemoSheet(
        string matchedSheetName,
        (string Category, string Amount, string? Memo) headers,
        params (string Category, string Amount, string? Memo)[] rows)
    {
        using var workbook = new XLWorkbook();
        var first = workbook.AddWorksheet("Splits");
        first.Cell(1, 1).Value = headers.Category;
        first.Cell(1, 2).Value = headers.Amount;
        first.Cell(1, 3).Value = headers.Memo;
        first.Cell(2, 1).Value = "Ignored";
        first.Cell(2, 2).Value = "-999";

        var matched = workbook.AddWorksheet(matchedSheetName);
        matched.Cell(1, 1).Value = headers.Category;
        matched.Cell(1, 2).Value = headers.Amount;
        matched.Cell(1, 3).Value = headers.Memo;

        for (var index = 0; index < rows.Length; index++)
        {
            var rowNumber = index + 2;
            matched.Cell(rowNumber, 1).Value = rows[index].Category;
            matched.Cell(rowNumber, 2).Value = rows[index].Amount;
            if (rows[index].Memo != null)
            {
                matched.Cell(rowNumber, 3).Value = rows[index].Memo;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

/// <summary>
/// Test implementation of ITenantProvider for testing.
/// </summary>
file class TestTenantProvider : ITenantProvider
{
    public required Tenant CurrentTenant { get; init; }
}
