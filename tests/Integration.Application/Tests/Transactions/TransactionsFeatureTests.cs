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





    #region Navigation Property Loading Tests (Critical - Splits Bug)

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
