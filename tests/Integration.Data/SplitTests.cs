using Microsoft.EntityFrameworkCore;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Data;

/// <summary>
/// Integration tests for Split entity CRUD operations and Transaction-Split relationships.
/// </summary>
/// <remarks>
/// These tests verify EF Core configuration, database schema, relationships, indexes,
/// and data integrity constraints for the Split entity.
/// </remarks>
public class SplitTests
{
    private ApplicationDbContext _context;
    private DbContextOptions<ApplicationDbContext> _options;
    private Tenant _tenant1;
    private Tenant _tenant2;
    private Transaction _transaction1;
    private Transaction _transaction2;

    [SetUp]
    public async Task Setup()
    {
        // Use in-memory database for testing
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new ApplicationDbContext(_options);
        _context.Database.OpenConnection(); // Keep in-memory DB alive
        _context.Database.EnsureCreated();

        // Create test tenants
        _tenant1 = new Tenant { Name = "Tenant 1", Description = "First test tenant" };
        _tenant2 = new Tenant { Name = "Tenant 2", Description = "Second test tenant" };
        _context.Tenants.AddRange(_tenant1, _tenant2);
        await _context.SaveChangesAsync();

        // Create test transactions
        _transaction1 = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Test Payee 1",
            Amount = 100.00m,
            TenantId = _tenant1.Id
        };
        _transaction2 = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Test Payee 2",
            Amount = 200.00m,
            TenantId = _tenant2.Id
        };
        _context.Transactions.AddRange(_transaction1, _transaction2);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    #region Basic CRUD Operations

    [Test]
    public async Task Split_CanCreateWithAllRequiredFields()
    {
        // Given: A split with all required fields
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Split should be persisted with auto-generated Id and Key
        Assert.That(split.Id, Is.GreaterThan(0));
        Assert.That(split.Key, Is.Not.EqualTo(Guid.Empty));

        // And: All fields should be persisted correctly
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.TransactionId, Is.EqualTo(_transaction1.Id));
        Assert.That(retrieved.Amount, Is.EqualTo(50.00m));
        Assert.That(retrieved.Category, Is.EqualTo("Groceries"));
        Assert.That(retrieved.Order, Is.EqualTo(0));
    }

    [Test]
    public async Task Split_CanCreateWithOptionalMemo()
    {
        // Given: A split with optional memo field
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Memo = "Weekly grocery shopping",
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Memo should be persisted
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Memo, Is.EqualTo("Weekly grocery shopping"));
    }

    [Test]
    public async Task Split_CanCreateWithoutMemo()
    {
        // Given: A split without memo (most common case)
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
            // Memo intentionally not set
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Memo should be null
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Memo, Is.Null);
    }

    [Test]
    public async Task Split_CanReadByKey()
    {
        // Given: A split with a generated key
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();
        var key = split.Key;

        // When: Querying by Key
        var found = await _context.Splits
            .FirstOrDefaultAsync(s => s.Key == key);

        // Then: Should find the split
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Category, Is.EqualTo("Groceries"));
        Assert.That(found.Key, Is.EqualTo(key));
    }

    [Test]
    public async Task Split_CanUpdateAllProperties()
    {
        // Given: An existing split
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Memo = "Original memo",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // When: Updating all properties
        split.Amount = 75.00m;
        split.Category = "Food & Dining";
        split.Memo = "Updated memo";
        split.Order = 1;
        await _context.SaveChangesAsync();

        // Then: All changes should be persisted
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Amount, Is.EqualTo(75.00m));
        Assert.That(retrieved.Category, Is.EqualTo("Food & Dining"));
        Assert.That(retrieved.Memo, Is.EqualTo("Updated memo"));
        Assert.That(retrieved.Order, Is.EqualTo(1));
    }

    [Test]
    public async Task Split_CanClearMemo()
    {
        // Given: A split with a memo
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Memo = "Original memo",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // When: Clearing the memo
        split.Memo = null;
        await _context.SaveChangesAsync();

        // Then: Memo should be null
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Memo, Is.Null);
    }

    [Test]
    public async Task Split_CanDelete()
    {
        // Given: An existing split
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();
        var id = split.Id;

        // When: Deleting the split
        _context.Splits.Remove(split);
        await _context.SaveChangesAsync();

        // Then: Split should be removed from database
        var deleted = await _context.Splits.FindAsync(id);
        Assert.That(deleted, Is.Null);
    }

    #endregion

    #region Relationship Tests

    [Test]
    public async Task Split_TransactionWithSingleSplit()
    {
        // Given: A transaction with a single split (Alpha-1 common case)
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 100.00m,
            Category = "Groceries",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // When: Loading transaction with splits
        var transaction = await _context.Transactions
            .Include(t => t.Splits)
            .FirstAsync(t => t.Id == _transaction1.Id);

        // Then: Transaction should have one split
        Assert.That(transaction.Splits, Has.Count.EqualTo(1));
        Assert.That(transaction.Splits.First().Category, Is.EqualTo("Groceries"));
        Assert.That(transaction.Splits.First().Amount, Is.EqualTo(100.00m));
    }

    [Test]
    public async Task Split_TransactionWithMultipleSplits()
    {
        // Given: A transaction with multiple splits (foundation for Beta-2)
        var split1 = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 60.00m,
            Category = "Groceries",
            Order = 0
        };
        var split2 = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 40.00m,
            Category = "Household",
            Order = 1
        };
        _context.Splits.AddRange(split1, split2);
        await _context.SaveChangesAsync();

        // When: Loading transaction with splits
        var transaction = await _context.Transactions
            .Include(t => t.Splits)
            .FirstAsync(t => t.Id == _transaction1.Id);

        // Then: Transaction should have two splits
        Assert.That(transaction.Splits, Has.Count.EqualTo(2));
        Assert.That(transaction.Splits.Sum(s => s.Amount), Is.EqualTo(100.00m));
    }

    [Test]
    public async Task Split_CascadeDelete_DeletingTransactionDeletesAllSplits()
    {
        // Given: A transaction with multiple splits
        var split1 = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 60.00m,
            Category = "Groceries",
            Order = 0
        };
        var split2 = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 40.00m,
            Category = "Household",
            Order = 1
        };
        _context.Splits.AddRange(split1, split2);
        await _context.SaveChangesAsync();
        var split1Id = split1.Id;
        var split2Id = split2.Id;

        // When: Deleting the parent transaction
        _context.Transactions.Remove(_transaction1);
        await _context.SaveChangesAsync();

        // Then: All splits should be deleted (cascade delete)
        var deletedSplit1 = await _context.Splits.FindAsync(split1Id);
        var deletedSplit2 = await _context.Splits.FindAsync(split2Id);
        Assert.That(deletedSplit1, Is.Null);
        Assert.That(deletedSplit2, Is.Null);

        // And: Transaction should be deleted
        var deletedTransaction = await _context.Transactions.FindAsync(_transaction1.Id);
        Assert.That(deletedTransaction, Is.Null);
    }

    [Test]
    public async Task Split_ForeignKeyConstraint_CannotCreateSplitWithInvalidTransactionId()
    {
        // Given: A split with a non-existent TransactionId
        var split = new Split
        {
            TransactionId = 999999, // Non-existent transaction
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
        };

        _context.Splits.Add(split);

        // When/Then: Saving should fail due to foreign key constraint
        Assert.ThrowsAsync<DbUpdateException>(async () => await _context.SaveChangesAsync());
    }

    [Test]
    public async Task Split_NavigationPropertyToTransaction()
    {
        // Given: A split with transaction navigation property
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // When: Loading split with transaction navigation property
        var retrieved = await _context.Splits
            .Include(s => s.Transaction)
            .FirstAsync(s => s.Id == split.Id);

        // Then: Navigation property should be populated
        Assert.That(retrieved.Transaction, Is.Not.Null);
        Assert.That(retrieved.Transaction!.Id, Is.EqualTo(_transaction1.Id));
        Assert.That(retrieved.Transaction.Payee, Is.EqualTo("Test Payee 1"));
    }

    #endregion

    #region Index Tests

    [Test]
    public async Task Split_KeyIsUnique()
    {
        // Given: Multiple splits
        var split1 = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
        };
        var split2 = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Household",
            Order = 1
        };
        _context.Splits.AddRange(split1, split2);
        await _context.SaveChangesAsync();

        // When: Checking the keys
        var allKeys = await _context.Splits
            .Select(s => s.Key)
            .ToListAsync();

        // Then: All keys should be unique
        Assert.That(allKeys, Is.Unique);
        Assert.That(allKeys, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Split_CanQueryByTransactionId()
    {
        // Given: Splits for multiple transactions
        _context.Splits.AddRange(
            new Split { TransactionId = _transaction1.Id, Amount = 50.00m, Category = "Groceries", Order = 0 },
            new Split { TransactionId = _transaction1.Id, Amount = 50.00m, Category = "Household", Order = 1 },
            new Split { TransactionId = _transaction2.Id, Amount = 200.00m, Category = "Electronics", Order = 0 }
        );
        await _context.SaveChangesAsync();

        // When: Querying splits by TransactionId (uses index)
        var transaction1Splits = await _context.Splits
            .Where(s => s.TransactionId == _transaction1.Id)
            .ToListAsync();

        // Then: Should only return splits for transaction 1
        Assert.That(transaction1Splits, Has.Count.EqualTo(2));
        Assert.That(transaction1Splits.All(s => s.TransactionId == _transaction1.Id), Is.True);
    }

    [Test]
    public async Task Split_CanQueryByCategory()
    {
        // Given: Splits with different categories
        _context.Splits.AddRange(
            new Split { TransactionId = _transaction1.Id, Amount = 50.00m, Category = "Groceries", Order = 0 },
            new Split { TransactionId = _transaction1.Id, Amount = 50.00m, Category = "Household", Order = 1 },
            new Split { TransactionId = _transaction2.Id, Amount = 100.00m, Category = "Groceries", Order = 0 },
            new Split { TransactionId = _transaction2.Id, Amount = 100.00m, Category = "Entertainment", Order = 1 }
        );
        await _context.SaveChangesAsync();

        // When: Querying splits by Category (uses index)
        var grocerySplits = await _context.Splits
            .Where(s => s.Category == "Groceries")
            .ToListAsync();

        // Then: Should return all grocery splits
        Assert.That(grocerySplits, Has.Count.EqualTo(2));
        Assert.That(grocerySplits.All(s => s.Category == "Groceries"), Is.True);
    }

    [Test]
    public async Task Split_CanQueryByTransactionIdAndOrder()
    {
        // Given: Splits with different orders
        _context.Splits.AddRange(
            new Split { TransactionId = _transaction1.Id, Amount = 60.00m, Category = "Groceries", Order = 0 },
            new Split { TransactionId = _transaction1.Id, Amount = 40.00m, Category = "Household", Order = 1 },
            new Split { TransactionId = _transaction2.Id, Amount = 200.00m, Category = "Electronics", Order = 0 }
        );
        await _context.SaveChangesAsync();

        // When: Querying splits by TransactionId and Order (uses composite index)
        var splits = await _context.Splits
            .Where(s => s.TransactionId == _transaction1.Id)
            .OrderBy(s => s.Order)
            .ToListAsync();

        // Then: Should return splits in correct order
        Assert.That(splits, Has.Count.EqualTo(2));
        Assert.That(splits[0].Category, Is.EqualTo("Groceries"));
        Assert.That(splits[0].Order, Is.EqualTo(0));
        Assert.That(splits[1].Category, Is.EqualTo("Household"));
        Assert.That(splits[1].Order, Is.EqualTo(1));
    }

    [Test]
    public async Task Split_IndexOnTransactionIdExists()
    {
        // Given: Database with Split table

        // When: Checking for the TransactionId index
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name FROM sqlite_master
            WHERE type='index'
            AND tbl_name='YoFi.V3.Splits'
            AND name LIKE '%TransactionId%'";
        var indexName = await command.ExecuteScalarAsync();

        // Then: TransactionId index should exist
        Assert.That(indexName, Is.Not.Null);
        Assert.That(indexName!.ToString(), Does.Contain("TransactionId"));
    }

    [Test]
    public async Task Split_IndexOnCategoryExists()
    {
        // Given: Database with Split table

        // When: Checking for the Category index
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name FROM sqlite_master
            WHERE type='index'
            AND tbl_name='YoFi.V3.Splits'
            AND name LIKE '%Category%'";
        var indexName = await command.ExecuteScalarAsync();

        // Then: Category index should exist
        Assert.That(indexName, Is.Not.Null);
        Assert.That(indexName!.ToString(), Does.Contain("Category"));
    }

    [Test]
    public async Task Split_CompositeIndexOnTransactionIdOrderExists()
    {
        // Given: Database with Split table

        // When: Checking for the composite TransactionId+Order index
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name FROM sqlite_master
            WHERE type='index'
            AND tbl_name='YoFi.V3.Splits'
            AND name LIKE '%TransactionId%Order%'";
        var indexName = await command.ExecuteScalarAsync();

        // Then: Composite index should exist
        Assert.That(indexName, Is.Not.Null);
        Assert.That(indexName!.ToString(), Does.Contain("TransactionId"));
        Assert.That(indexName!.ToString(), Does.Contain("Order"));
    }

    #endregion

    #region Data Integrity Tests

    [Test]
    public async Task Split_CategoryEmptyStringForUncategorized()
    {
        // Given: A split with empty string category (uncategorized)
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = string.Empty,
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Empty string category should be allowed
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Category, Is.EqualTo(string.Empty));
        Assert.That(retrieved.Category, Is.Not.Null); // NOT NULL constraint verified
    }

    [Test]
    public async Task Split_CategoryMaxLengthIs100()
    {
        // Given: A split with Category exactly at max length (100 chars)
        var category100 = new string('A', 100);
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = category100,
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Category should be saved without truncation
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Category, Is.EqualTo(category100));
        Assert.That(retrieved.Category.Length, Is.EqualTo(100));
    }

    [Test]
    public async Task Split_MemoMaxLengthIs500()
    {
        // Given: A split with Memo exactly at max length (500 chars)
        var memo500 = new string('B', 500);
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Memo = memo500,
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Memo should be saved without truncation
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Memo, Is.EqualTo(memo500));
        Assert.That(retrieved.Memo!.Length, Is.EqualTo(500));
    }

    [Test]
    public async Task Split_AmountStoredWithCorrectPrecision()
    {
        // Given: A split with precise decimal amount
        var preciseAmount = 123.45m;
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = preciseAmount,
            Category = "Groceries",
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Amount precision should be preserved (decimal 18,2)
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Amount, Is.EqualTo(preciseAmount));
    }

    [Test]
    public async Task Split_AmountSupportsNegativeValues()
    {
        // Given: A split with negative amount (for credits/refunds)
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = -25.50m,
            Category = "Groceries",
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Negative amount should be preserved
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Amount, Is.EqualTo(-25.50m));
    }

    [Test]
    public async Task Split_OrderValuePersistsCorrectly()
    {
        // Given: Splits with various order values
        _context.Splits.AddRange(
            new Split { TransactionId = _transaction1.Id, Amount = 50.00m, Category = "Groceries", Order = 0 },
            new Split { TransactionId = _transaction1.Id, Amount = 30.00m, Category = "Household", Order = 1 },
            new Split { TransactionId = _transaction1.Id, Amount = 20.00m, Category = "Entertainment", Order = 2 }
        );
        await _context.SaveChangesAsync();

        // When: Retrieving splits ordered by Order
        var splits = await _context.Splits
            .Where(s => s.TransactionId == _transaction1.Id)
            .OrderBy(s => s.Order)
            .ToListAsync();

        // Then: Order values should persist correctly
        Assert.That(splits, Has.Count.EqualTo(3));
        Assert.That(splits[0].Order, Is.EqualTo(0));
        Assert.That(splits[1].Order, Is.EqualTo(1));
        Assert.That(splits[2].Order, Is.EqualTo(2));
    }

    [Test]
    public async Task Split_CategoryCanContainSpecialCharacters()
    {
        // Given: A split with category containing special characters
        var specialCategory = "Food & Dining: Groceries/Snacks (Weekly)";
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = specialCategory,
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Category with special characters should be preserved
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Category, Is.EqualTo(specialCategory));
    }

    [Test]
    public async Task Split_MemoCanContainMultilineText()
    {
        // Given: A split with multi-line memo
        var multilineMemo = "Line 1\nLine 2\nLine 3";
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Memo = multilineMemo,
            Order = 0
        };

        // When: Saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Multi-line memo should be preserved
        var retrieved = await _context.Splits.FindAsync(split.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Memo, Is.EqualTo(multilineMemo));
    }

    #endregion

    #region Transaction Property Tests

    [Test]
    public async Task Transaction_MemoPropertyPersists()
    {
        // Given: A transaction with memo (already added in previous migration)
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Test Payee",
            Amount = 100.00m,
            Memo = "Transaction-level memo",
            TenantId = _tenant1.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // When: Retrieving the transaction
        var retrieved = await _context.Transactions.FindAsync(transaction.Id);

        // Then: Memo should be persisted
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Memo, Is.EqualTo("Transaction-level memo"));
    }

    [Test]
    public async Task Transaction_SourcePropertyPersists()
    {
        // Given: A transaction with source (already added in previous migration)
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Test Payee",
            Amount = 100.00m,
            Source = "Chase Checking 1234",
            TenantId = _tenant1.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // When: Retrieving the transaction
        var retrieved = await _context.Transactions.FindAsync(transaction.Id);

        // Then: Source should be persisted
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Source, Is.EqualTo("Chase Checking 1234"));
    }

    #endregion

    #region Table Configuration Tests

    [Test]
    public async Task Split_HasCorrectTableName()
    {
        // Given: A split
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
        };

        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // When: Checking the table name in the database
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='YoFi.V3.Splits'";
        var tableName = await command.ExecuteScalarAsync();

        // Then: The table should exist with the correct name
        Assert.That(tableName, Is.EqualTo("YoFi.V3.Splits"));
    }

    [Test]
    public async Task Split_IdIsAutoGenerated_KeyIsPreset()
    {
        // Given: A split without Id explicitly set
        var split = new Split
        {
            TransactionId = _transaction1.Id,
            Amount = 50.00m,
            Category = "Groceries",
            Order = 0
        };

        Assert.That(split.Id, Is.EqualTo(0)); // Initially zero
        var initialKey = split.Key;
        Assert.That(initialKey, Is.Not.EqualTo(Guid.Empty)); // Key is auto-generated on construction

        // When: Adding and saving the split
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Then: Id should be auto-generated, Key should remain the same
        Assert.That(split.Id, Is.GreaterThan(0));
        Assert.That(split.Key, Is.EqualTo(initialKey)); // Key doesn't change
        Assert.That(split.Key, Is.Not.EqualTo(Guid.Empty));
    }

    #endregion
}
