using Moq;
using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Tests.Unit.Application.Import;

/// <summary>
/// Unit tests for ImportReviewFeature duplicate detection logic.
/// </summary>
[TestFixture]
public class ImportReviewFeatureTests
{
    [Test]
    public void DetectDuplicate_NewTransaction_ReturnsNew()
    {
        // Given: Import transaction with no matches in either table
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };
        var existingTransactions = new Dictionary<string, Transaction>();
        var pendingImports = new Dictionary<string, ImportReviewTransaction>();

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should be marked as New with no duplicate reference
        Assert.That(status, Is.EqualTo(DuplicateStatus.New));
        Assert.That(duplicateOfKey, Is.Null);
    }

    [Test]
    public void DetectDuplicate_ExactDuplicateInMainTable_ReturnsExactDuplicate()
    {
        // Given: Existing transaction in main table with same FITID and matching data
        var existingKey = Guid.NewGuid();
        var existingTransaction = new Transaction
        {
            Key = existingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };
        var existingTransactions = new Dictionary<string, Transaction>
        {
            ["FITID12345"] = existingTransaction
        };
        var pendingImports = new Dictionary<string, ImportReviewTransaction>();

        // And: Import transaction with same FITID and matching data
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should be marked as ExactDuplicate with reference to existing transaction
        Assert.That(status, Is.EqualTo(DuplicateStatus.ExactDuplicate));
        Assert.That(duplicateOfKey, Is.EqualTo(existingKey));
    }

    [Test]
    public void DetectDuplicate_PotentialDuplicateInMainTable_DifferentAmount_ReturnsPotentialDuplicate()
    {
        // Given: Existing transaction in main table with same FITID but different amount
        var existingKey = Guid.NewGuid();
        var existingTransaction = new Transaction
        {
            Key = existingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };
        var existingTransactions = new Dictionary<string, Transaction>
        {
            ["FITID12345"] = existingTransaction
        };
        var pendingImports = new Dictionary<string, ImportReviewTransaction>();

        // And: Import transaction with same FITID but different amount (bank correction?)
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 55.00m, // Different amount
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should be marked as PotentialDuplicate with reference for user review
        Assert.That(status, Is.EqualTo(DuplicateStatus.PotentialDuplicate));
        Assert.That(duplicateOfKey, Is.EqualTo(existingKey));
    }

    [Test]
    public void DetectDuplicate_PotentialDuplicateInMainTable_DifferentDate_ReturnsPotentialDuplicate()
    {
        // Given: Existing transaction in main table with same FITID but different date
        var existingKey = Guid.NewGuid();
        var existingTransaction = new Transaction
        {
            Key = existingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };
        var existingTransactions = new Dictionary<string, Transaction>
        {
            ["FITID12345"] = existingTransaction
        };
        var pendingImports = new Dictionary<string, ImportReviewTransaction>();

        // And: Import transaction with same FITID but different date
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 20), // Different date
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should be marked as PotentialDuplicate with reference for user review
        Assert.That(status, Is.EqualTo(DuplicateStatus.PotentialDuplicate));
        Assert.That(duplicateOfKey, Is.EqualTo(existingKey));
    }

    [Test]
    public void DetectDuplicate_PotentialDuplicateInMainTable_DifferentPayee_ReturnsPotentialDuplicate()
    {
        // Given: Existing transaction in main table with same FITID but different payee
        var existingKey = Guid.NewGuid();
        var existingTransaction = new Transaction
        {
            Key = existingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };
        var existingTransactions = new Dictionary<string, Transaction>
        {
            ["FITID12345"] = existingTransaction
        };
        var pendingImports = new Dictionary<string, ImportReviewTransaction>();

        // And: Import transaction with same FITID but different payee
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway Store #123", // Different payee
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should be marked as PotentialDuplicate with reference for user review
        Assert.That(status, Is.EqualTo(DuplicateStatus.PotentialDuplicate));
        Assert.That(duplicateOfKey, Is.EqualTo(existingKey));
    }

    [Test]
    public void DetectDuplicate_ExactDuplicateInPendingImports_ReturnsExactDuplicate()
    {
        // Given: Pending import transaction with same FITID and matching data
        var pendingKey = Guid.NewGuid();
        var pendingImport = new ImportReviewTransaction
        {
            Key = pendingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };
        var existingTransactions = new Dictionary<string, Transaction>();
        var pendingImports = new Dictionary<string, ImportReviewTransaction>
        {
            ["FITID12345"] = pendingImport
        };

        // And: Import transaction with same FITID and matching data
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should be marked as ExactDuplicate with reference to pending import
        Assert.That(status, Is.EqualTo(DuplicateStatus.ExactDuplicate));
        Assert.That(duplicateOfKey, Is.EqualTo(pendingKey));
    }

    [Test]
    public void DetectDuplicate_PotentialDuplicateInPendingImports_ReturnsPotentialDuplicate()
    {
        // Given: Pending import transaction with same FITID but different amount
        var pendingKey = Guid.NewGuid();
        var pendingImport = new ImportReviewTransaction
        {
            Key = pendingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };
        var existingTransactions = new Dictionary<string, Transaction>();
        var pendingImports = new Dictionary<string, ImportReviewTransaction>
        {
            ["FITID12345"] = pendingImport
        };

        // And: Import transaction with same FITID but different amount
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 55.00m, // Different amount
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should be marked as PotentialDuplicate with reference to pending import
        Assert.That(status, Is.EqualTo(DuplicateStatus.PotentialDuplicate));
        Assert.That(duplicateOfKey, Is.EqualTo(pendingKey));
    }

    [Test]
    public void DetectDuplicate_MainTableTakesPrecedenceOverPendingImports_ReturnsMainTableMatch()
    {
        // Given: Both main table and pending imports have matching FITID
        var existingKey = Guid.NewGuid();
        var pendingKey = Guid.NewGuid();

        var existingTransaction = new Transaction
        {
            Key = existingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };
        var pendingImport = new ImportReviewTransaction
        {
            Key = pendingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };

        var existingTransactions = new Dictionary<string, Transaction>
        {
            ["FITID12345"] = existingTransaction
        };
        var pendingImports = new Dictionary<string, ImportReviewTransaction>
        {
            ["FITID12345"] = pendingImport
        };

        // And: Import transaction with matching FITID
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should match against main table (takes precedence)
        Assert.That(status, Is.EqualTo(DuplicateStatus.ExactDuplicate));
        Assert.That(duplicateOfKey, Is.EqualTo(existingKey));
    }

    [Test]
    public void DetectDuplicate_CaseInsensitiveExternalIdMatch_ReturnsExactDuplicate()
    {
        // Given: Existing transaction with uppercase ExternalId
        var existingKey = Guid.NewGuid();
        var existingTransaction = new Transaction
        {
            Key = existingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = "FITID12345"
        };
        var existingTransactions = new Dictionary<string, Transaction>(StringComparer.OrdinalIgnoreCase)
        {
            ["FITID12345"] = existingTransaction
        };
        var pendingImports = new Dictionary<string, ImportReviewTransaction>(StringComparer.OrdinalIgnoreCase);

        // And: Import transaction with lowercase ExternalId
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "fitid12345", // Lowercase
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should match case-insensitively
        Assert.That(status, Is.EqualTo(DuplicateStatus.ExactDuplicate));
        Assert.That(duplicateOfKey, Is.EqualTo(existingKey));
    }

    [Test]
    public void DetectDuplicate_NullExternalId_ThrowsArgumentException()
    {
        // Given: Import transaction with null ExternalId (should never happen due to upstream filtering)
        var existingTransactions = new Dictionary<string, Transaction>();
        var pendingImports = new Dictionary<string, ImportReviewTransaction>();

        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = null!, // Should never happen
            Source = "Test Bank"
        };

        // When/Then: DetectDuplicate should throw ArgumentException
        var ex = Assert.Throws<ArgumentException>(() =>
            ImportReviewFeature.DetectDuplicate(importDto, existingTransactions, pendingImports));

        // And: Exception should indicate this is a bug
        Assert.That(ex.Message, Does.Contain("ExternalId cannot be null or empty"));
        Assert.That(ex.Message, Does.Contain("bug in upstream"));
        Assert.That(ex.ParamName, Is.EqualTo("importDto"));
    }

    [Test]
    public void DetectDuplicate_EmptyExternalId_ThrowsArgumentException()
    {
        // Given: Import transaction with empty ExternalId (should never happen due to upstream filtering)
        var existingTransactions = new Dictionary<string, Transaction>();
        var pendingImports = new Dictionary<string, ImportReviewTransaction>();

        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "", // Should never happen
            Source = "Test Bank"
        };

        // When/Then: DetectDuplicate should throw ArgumentException
        var ex = Assert.Throws<ArgumentException>(() =>
            ImportReviewFeature.DetectDuplicate(importDto, existingTransactions, pendingImports));

        // And: Exception should indicate this is a bug
        Assert.That(ex.Message, Does.Contain("ExternalId cannot be null or empty"));
        Assert.That(ex.Message, Does.Contain("bug in upstream"));
        Assert.That(ex.ParamName, Is.EqualTo("importDto"));
    }

    [Test]
    public void DetectDuplicate_ExistingTransactionHasNullExternalId_DoesNotMatch()
    {
        // Given: Existing transaction with null ExternalId
        var existingKey = Guid.NewGuid();
        var existingTransaction = new Transaction
        {
            Key = existingKey,
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            ExternalId = null // No FITID
        };
        var existingTransactions = new Dictionary<string, Transaction>();
        var pendingImports = new Dictionary<string, ImportReviewTransaction>();

        // And: Import transaction with valid ExternalId
        var importDto = new TransactionImportDto
        {
            Date = new DateOnly(2024, 1, 15),
            Payee = "Safeway",
            Amount = 50.00m,
            Memo = "Groceries",
            ExternalId = "FITID12345",
            Source = "Test Bank"
        };

        // When: DetectDuplicate is called
        var (status, duplicateOfKey) = ImportReviewFeature.DetectDuplicate(
            importDto,
            existingTransactions,
            pendingImports);

        // Then: Should be marked as New (cannot match when existing has null ExternalId)
        Assert.That(status, Is.EqualTo(DuplicateStatus.New));
        Assert.That(duplicateOfKey, Is.Null);
    }

    #region SeedTestDataAsync Tests

    [Test]
    public async Task SeedTestDataAsync_DefaultSelectedCount_AllTransactionsSelected()
    {
        // Given: Mock dependencies and feature configured for 5 transactions
        var (feature, dataProvider) = CreateFeatureWithMocks();
        var count = 5;

        // When: SeedTestDataAsync is called without selectedCount parameter
        var result = await feature.SeedTestDataAsync(count);

        // Then: Should return the count
        Assert.That(result, Is.EqualTo(5));

        // And: Should add transactions to data provider
        dataProvider.Verify(dp => dp.AddRange(It.Is<IEnumerable<ImportReviewTransaction>>(
            transactions => transactions.Count() == 5)), Times.Once);

        // And: Should save changes
        dataProvider.Verify(dp => dp.SaveChangesAsync(default), Times.Once);

        // And: All transactions should be selected
        dataProvider.Verify(dp => dp.AddRange(It.Is<IEnumerable<ImportReviewTransaction>>(
            transactions => transactions.All(t => t.IsSelected))), Times.Once);
    }

    [Test]
    public async Task SeedTestDataAsync_PartialSelection_OnlySpecifiedCountSelected()
    {
        // Given: Mock dependencies and feature configured for 10 transactions with 3 selected
        var (feature, dataProvider) = CreateFeatureWithMocks();
        var count = 10;
        var selectedCount = 3;

        // When: SeedTestDataAsync is called with selectedCount = 3
        var result = await feature.SeedTestDataAsync(count, selectedCount);

        // Then: Should return the count
        Assert.That(result, Is.EqualTo(10));

        // And: Should add transactions where exactly 3 are selected
        dataProvider.Verify(dp => dp.AddRange(It.Is<IEnumerable<ImportReviewTransaction>>(
            transactions => transactions.Count(t => t.IsSelected) == 3)), Times.Once);

        // And: Should add transactions where exactly 7 are not selected
        dataProvider.Verify(dp => dp.AddRange(It.Is<IEnumerable<ImportReviewTransaction>>(
            transactions => transactions.Count(t => !t.IsSelected) == 7)), Times.Once);
    }

    [Test]
    public async Task SeedTestDataAsync_ZeroSelected_NoTransactionsSelected()
    {
        // Given: Mock dependencies and feature configured for 5 transactions with 0 selected
        var (feature, dataProvider) = CreateFeatureWithMocks();
        var count = 5;
        var selectedCount = 0;

        // When: SeedTestDataAsync is called with selectedCount = 0
        var result = await feature.SeedTestDataAsync(count, selectedCount);

        // Then: Should return the count
        Assert.That(result, Is.EqualTo(5));

        // And: No transactions should be selected
        dataProvider.Verify(dp => dp.AddRange(It.Is<IEnumerable<ImportReviewTransaction>>(
            transactions => transactions.All(t => !t.IsSelected))), Times.Once);
    }

    [Test]
    public async Task SeedTestDataAsync_ZeroCount_NoTransactionsCreated()
    {
        // Given: Mock dependencies and feature configured for 0 transactions
        var (feature, dataProvider) = CreateFeatureWithMocks();
        var count = 0;

        // When: SeedTestDataAsync is called with count = 0
        var result = await feature.SeedTestDataAsync(count);

        // Then: Should return 0
        Assert.That(result, Is.Zero);

        // And: Should add empty collection
        dataProvider.Verify(dp => dp.AddRange(It.Is<IEnumerable<ImportReviewTransaction>>(
            transactions => !transactions.Any())), Times.Once);

        // And: Should still save changes
        dataProvider.Verify(dp => dp.SaveChangesAsync(default), Times.Once);
    }

    [Test]
    public async Task SeedTestDataAsync_SelectedCountGreaterThanCount_ThrowsArgumentOutOfRangeException()
    {
        // Given: Mock dependencies and feature configured for invalid selectedCount
        var (feature, _) = CreateFeatureWithMocks();
        var count = 5;
        var selectedCount = 10;

        // When/Then: SeedTestDataAsync should throw ArgumentOutOfRangeException
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await feature.SeedTestDataAsync(count, selectedCount));

        // And: Exception should indicate valid range
        Assert.That(ex!.ParamName, Is.EqualTo("selectedCount"));
        Assert.That(ex.Message, Does.Contain("must be between 0 and 5"));
        Assert.That(ex.Message, Does.Contain("but was 10"));
    }

    [Test]
    public async Task SeedTestDataAsync_NegativeSelectedCount_ThrowsArgumentOutOfRangeException()
    {
        // Given: Mock dependencies and feature configured for negative selectedCount
        var (feature, _) = CreateFeatureWithMocks();
        var count = 5;
        var selectedCount = -1;

        // When/Then: SeedTestDataAsync should throw ArgumentOutOfRangeException
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await feature.SeedTestDataAsync(count, selectedCount));

        // And: Exception should indicate valid range
        Assert.That(ex!.ParamName, Is.EqualTo("selectedCount"));
        Assert.That(ex.Message, Does.Contain("must be between 0 and 5"));
        Assert.That(ex.Message, Does.Contain("but was -1"));
    }

    [Test]
    public async Task SeedTestDataAsync_GeneratesDeterministicData()
    {
        // Given: Mock dependencies and feature configured for 3 transactions
        var (feature, dataProvider) = CreateFeatureWithMocks();
        var count = 3;
        var capturedTransactions = new List<ImportReviewTransaction>();

        dataProvider.Setup(dp => dp.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback<IEnumerable<IModel>>(models =>
                capturedTransactions.AddRange(models.Cast<ImportReviewTransaction>()));

        // When: SeedTestDataAsync is called
        await feature.SeedTestDataAsync(count);

        // Then: Should generate deterministic data based on index
        Assert.That(capturedTransactions, Has.Count.EqualTo(3));

        // And: First transaction should have predictable values
        var firstTransaction = capturedTransactions[0];
        Assert.That(firstTransaction.Payee, Is.EqualTo("Test Import 1"));
        Assert.That(firstTransaction.Amount, Is.EqualTo(15.00m)); // 10 + (1 * 5)
        Assert.That(firstTransaction.ExternalId, Is.EqualTo("FITID-TEST000000000001"));
        Assert.That(firstTransaction.Memo, Is.EqualTo("Test import transaction 1"));
        Assert.That(firstTransaction.Source, Is.EqualTo("OFX"));
        Assert.That(firstTransaction.DuplicateStatus, Is.EqualTo(DuplicateStatus.New));
        Assert.That(firstTransaction.DuplicateOfKey, Is.Null);

        // And: Second transaction should have predictable values
        var secondTransaction = capturedTransactions[1];
        Assert.That(secondTransaction.Payee, Is.EqualTo("Test Import 2"));
        Assert.That(secondTransaction.Amount, Is.EqualTo(20.00m)); // 10 + (2 * 5)
        Assert.That(secondTransaction.ExternalId, Is.EqualTo("FITID-TEST000000000002"));

        // And: Third transaction should have predictable values
        var thirdTransaction = capturedTransactions[2];
        Assert.That(thirdTransaction.Payee, Is.EqualTo("Test Import 3"));
        Assert.That(thirdTransaction.Amount, Is.EqualTo(25.00m)); // 10 + (3 * 5)
        Assert.That(thirdTransaction.ExternalId, Is.EqualTo("FITID-TEST000000000003"));
    }

    [TestCase(2040, 7, 1)]
    [TestCase(2040, 3, 1)]
    [TestCase(2040, 1, 1)]
    public async Task SeedTestDataAsync_GeneratesDeterministicDates(int year, int month, int day)
    {
        // Given: Mock dependencies and feature configured for transactions
        var now = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        var (feature, dataProvider) = CreateFeatureWithMocks(utcNow: now);
        var count = 32; // Test date cycling (more than 30 days)
        var capturedTransactions = new List<ImportReviewTransaction>();

        dataProvider.Setup(dp => dp.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback<IEnumerable<IModel>>(models =>
                capturedTransactions.AddRange(models.Cast<ImportReviewTransaction>()));

        // When: SeedTestDataAsync is called
        await feature.SeedTestDataAsync(count);

        // Then: Dates should cycle from the provider's UTC date minus 30 days
        var baseDate = new DateOnly(year, month, day).AddDays(-30);
        Assert.That(capturedTransactions.Select(t => t.Date),
            Is.EqualTo(Enumerable.Range(1, count).Select(i => baseDate.AddDays(i % 30))));

        var baseDateRange = capturedTransactions.Select(t => t.Date).Distinct().OrderBy(d => d).ToList();

        // And: Date range should span 30 days or less
        var dateSpan = (baseDateRange.Max().ToDateTime(TimeOnly.MinValue) - baseDateRange.Min().ToDateTime(TimeOnly.MinValue)).Days;
        Assert.That(dateSpan, Is.LessThanOrEqualTo(30));

        // And: Transaction 1 and transaction 31 should have same date offset (i % 30)
        Assert.That(capturedTransactions[0].Date, Is.EqualTo(capturedTransactions[30].Date));
    }

    [Test]
    public async Task SeedTestDataAsync_UsesCurrentTenantId()
    {
        // Given: Mock dependencies with specific tenant ID
        var tenantId = 42L;
        var (feature, dataProvider) = CreateFeatureWithMocks(tenantId: tenantId);
        var count = 3;
        var capturedTransactions = new List<ImportReviewTransaction>();

        dataProvider.Setup(dp => dp.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback<IEnumerable<IModel>>(models =>
                capturedTransactions.AddRange(models.Cast<ImportReviewTransaction>()));

        // When: SeedTestDataAsync is called
        await feature.SeedTestDataAsync(count);

        // Then: All transactions should have the current tenant ID
        Assert.That(capturedTransactions, Has.Count.EqualTo(3));
        Assert.That(capturedTransactions.All(t => t.TenantId == tenantId), Is.True);
    }

    [Test]
    public async Task SeedTestDataAsync_SelectionOrderMatchesCreationOrder()
    {
        // Given: Mock dependencies and feature configured for 5 transactions with 3 selected
        var (feature, dataProvider) = CreateFeatureWithMocks();
        var count = 5;
        var selectedCount = 3;
        var capturedTransactions = new List<ImportReviewTransaction>();

        dataProvider.Setup(dp => dp.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback<IEnumerable<IModel>>(models =>
                capturedTransactions.AddRange(models.Cast<ImportReviewTransaction>()));

        // When: SeedTestDataAsync is called with selectedCount = 3
        await feature.SeedTestDataAsync(count, selectedCount);

        // Then: First 3 transactions should be selected
        Assert.That(capturedTransactions[0].IsSelected, Is.True);
        Assert.That(capturedTransactions[1].IsSelected, Is.True);
        Assert.That(capturedTransactions[2].IsSelected, Is.True);

        // And: Remaining transactions should not be selected
        Assert.That(capturedTransactions[3].IsSelected, Is.False);
        Assert.That(capturedTransactions[4].IsSelected, Is.False);
    }

    [Test]
    public async Task ImportFileAsync_EmptyStreamReturnsZeroCounts()
    {
        var (feature, _) = CreateFeatureWithMocks();
        using var stream = new MemoryStream();

        var result = await feature.ImportFileAsync(stream, "empty.ofx");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ImportedCount, Is.Zero);
            Assert.That(result.NewCount, Is.Zero);
            Assert.That(result.ExactDuplicateCount, Is.Zero);
            Assert.That(result.PotentialDuplicateCount, Is.Zero);
            Assert.That(result.Errors, Is.Empty);
        }
    }

    [Test]
    public async Task ImportFileAsync_StoresNewExactAndPotentialDuplicatesWithMatchedCategory()
    {
        var (feature, dataProvider) = CreateFeatureWithMocks(tenantId: 42);
        var transactions = new[]
        {
            new TransactionEditDto(new(2025, 1, 2), -10m, "Exact Store", null, "Bank", "exact", null),
            new TransactionEditDto(new(2025, 1, 3), -20m, "Potential Store", null, "Bank", "potential", null),
            new TransactionEditDto(new(2025, 1, 4), -30m, "New Store", null, "Bank", "new", null)
        };
        using var stream = SampleOfxWriter.Write(transactions, new(2025, 1, 1), new(2025, 1, 31));

        var existing = new[]
        {
            new Transaction
            {
                TenantId = 42,
                Date = new(2025, 1, 2),
                Amount = -10m,
                Payee = "Exact Store",
                ExternalId = "exact",
                Key = Guid.Parse("11111111-1111-1111-1111-111111111111")
            },
            new Transaction
            {
                TenantId = 42,
                Date = new(2025, 1, 2),
                Amount = -99m,
                Payee = "Different",
                ExternalId = "potential",
                Key = Guid.Parse("22222222-2222-2222-2222-222222222222")
            }
        };
        dataProvider.Setup(dp => dp.Get<Transaction>()).Returns(existing.AsQueryable());
        dataProvider.Setup(dp => dp.Get<ImportReviewTransaction>()).Returns(Array.Empty<ImportReviewTransaction>().AsQueryable());
        dataProvider.Setup(dp => dp.ToListNoTrackingAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) => Task.FromResult(query.ToList()));
        dataProvider.Setup(dp => dp.ToListNoTrackingAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()))
            .Returns((IQueryable<ImportReviewTransaction> query) => Task.FromResult(query.ToList()));

        var staged = new List<ImportReviewTransaction>();
        dataProvider.Setup(dp => dp.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback((IEnumerable<IModel> models) => staged.AddRange(models.Cast<ImportReviewTransaction>()));

        var result = await feature.ImportFileAsync(stream, "transactions.ofx");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ImportedCount, Is.EqualTo(3));
            Assert.That(result.NewCount, Is.EqualTo(1));
            Assert.That(result.ExactDuplicateCount, Is.EqualTo(1));
            Assert.That(result.PotentialDuplicateCount, Is.EqualTo(1));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(staged, Has.Count.EqualTo(3));
            Assert.That(staged.Count(t => t.DuplicateStatus == DuplicateStatus.New), Is.EqualTo(1));
            Assert.That(staged.Count(t => t.DuplicateStatus == DuplicateStatus.ExactDuplicate), Is.EqualTo(1));
            Assert.That(staged.Count(t => t.DuplicateStatus == DuplicateStatus.PotentialDuplicate), Is.EqualTo(1));
            Assert.That(staged.All(t => t.TenantId == 42), Is.True);
        }
        dataProvider.Verify(dp => dp.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ImportFileAsync_InvalidOfxReturnsParsingErrorWithoutStaging()
    {
        var (feature, dataProvider) = CreateFeatureWithMocks();
        using var stream = new MemoryStream("not an OFX document"u8.ToArray());

        var result = await feature.ImportFileAsync(stream, "invalid.ofx");

        Assert.That(result.ImportedCount, Is.Zero);
        Assert.That(result.Errors, Is.Not.Empty);
        dataProvider.Verify(dp => dp.AddRange(It.IsAny<IEnumerable<IModel>>()), Times.Never);
        dataProvider.Verify(dp => dp.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CompleteReviewAsync_AcceptsSelectedAndDeletesAll()
    {
        var tenant = new YoFi.V3.Entities.Tenancy.Models.Tenant
        {
            Id = 42,
            Name = "Tenant",
            Key = Guid.NewGuid()
        };
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(provider => provider.CurrentTenant).Returns(tenant);
        var dataProvider = new Mock<IDataProvider>();
        var selected = new[]
        {
            new ImportReviewTransaction
            {
                TenantId = 42,
                Key = Guid.NewGuid(),
                Date = new(2025, 1, 2),
                Payee = "Coffee",
                Amount = -5m,
                Category = "Food",
                Source = "Bank",
                ExternalId = "fitid-1",
                Memo = "memo",
                IsSelected = true
            }
        };
        dataProvider.Setup(dp => dp.Get<ImportReviewTransaction>()).Returns(selected.AsQueryable());
        dataProvider.SetupSequence(dp => dp.CountAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()))
            .ReturnsAsync(3)
            .ReturnsAsync(1);
        dataProvider.Setup(dp => dp.ToListAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()))
            .Returns((IQueryable<ImportReviewTransaction> query) => Task.FromResult(query.ToList()));
        dataProvider.Setup(dp => dp.BulkDeleteAsync(It.IsAny<IQueryable<ImportReviewTransaction>>())).ReturnsAsync(3);

        dataProvider.Setup(dp => dp.AddRange(It.IsAny<IEnumerable<IModel>>()));
        dataProvider.Setup(dp => dp.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var transactionsFeature = new TransactionsFeature(tenantProvider.Object, dataProvider.Object);

        var matching = new Mock<YoFi.V3.Application.Services.IPayeeMatchingService>();
        var time = new Mock<TimeProvider>();
        time.Setup(item => item.GetUtcNow()).Returns(new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));
        var feature = new ImportReviewFeature(tenantProvider.Object, dataProvider.Object,
            transactionsFeature, matching.Object, time.Object);

        var result = await feature.CompleteReviewAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.AcceptedCount, Is.EqualTo(1));
            Assert.That(result.RejectedCount, Is.EqualTo(2));
        }
        dataProvider.Verify(dp => dp.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        dataProvider.Verify(dp => dp.BulkDeleteAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()), Times.Once);
    }

    [Test]
    public async Task CompleteReviewAsync_NoSelectionAcceptsNoneAndDeletesAll()
    {
        var (feature, dataProvider) = CreateFeatureWithMocks(tenantId: 42);
        dataProvider.Setup(dp => dp.Get<ImportReviewTransaction>()).Returns(Array.Empty<ImportReviewTransaction>().AsQueryable());
        dataProvider.SetupSequence(dp => dp.CountAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()))
            .ReturnsAsync(2)
            .ReturnsAsync(2);
        dataProvider.Setup(dp => dp.ToListAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()))
            .ReturnsAsync([]);
        dataProvider.Setup(dp => dp.BulkDeleteAsync(It.IsAny<IQueryable<ImportReviewTransaction>>())).ReturnsAsync(2);

        var result = await feature.CompleteReviewAsync();

        Assert.That(result.AcceptedCount, Is.Zero);
        Assert.That(result.RejectedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GetPendingReviewAsync_NormalizesPageAndMapsRows()
    {
        var (feature, dataProvider) = CreateFeatureWithMocks(tenantId: 42);
        var records = new[]
        {
            new ImportReviewTransaction
            {
                TenantId = 42,
                Key = Guid.NewGuid(),
                Date = new(2025, 1, 2),
                Payee = "Coffee",
                Category = "Food",
                Amount = -5m,
                DuplicateStatus = DuplicateStatus.New,
                IsSelected = true
            }
        };
        dataProvider.Setup(dp => dp.Get<ImportReviewTransaction>()).Returns(records.AsQueryable());
        dataProvider.Setup(dp => dp.CountAsync(It.IsAny<IQueryable<ImportReviewTransaction>>())).ReturnsAsync(1);
        dataProvider.Setup(dp => dp.ToListNoTrackingAsync(It.IsAny<IQueryable<ImportReviewTransactionDto>>()))
            .Returns((IQueryable<ImportReviewTransactionDto> query) => Task.FromResult(query.ToList()));

        var result = await feature.GetPendingReviewAsync(0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items.Single().Payee, Is.EqualTo("Coffee"));
            Assert.That(result.Metadata.PageNumber, Is.EqualTo(1));
            Assert.That(result.Metadata.TotalCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task SetSelectionSelectAllAndDeselectAllAsyncBulkUpdateTenantQuery()
    {
        var (feature, dataProvider) = CreateFeatureWithMocks(tenantId: 42);
        dataProvider.Setup(dp => dp.Get<ImportReviewTransaction>()).Returns(Array.Empty<ImportReviewTransaction>().AsQueryable());
        dataProvider.Setup(dp => dp.BulkUpdateAsync(
                It.IsAny<IQueryable<ImportReviewTransaction>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<ImportReviewTransaction, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        await feature.SetSelectionAsync([Guid.NewGuid()], true);
        await feature.SelectAllAsync();
        await feature.DeselectAllAsync();

        dataProvider.Verify(dp => dp.BulkUpdateAsync(
            It.IsAny<IQueryable<ImportReviewTransaction>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<ImportReviewTransaction, bool>>>(),
            true,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task DeleteAllAsyncReturnsCountAndBulkDeletesTenantRows()
    {
        var (feature, dataProvider) = CreateFeatureWithMocks(tenantId: 42);
        dataProvider.Setup(dp => dp.Get<ImportReviewTransaction>()).Returns(Array.Empty<ImportReviewTransaction>().AsQueryable());
        dataProvider.Setup(dp => dp.CountAsync(It.IsAny<IQueryable<ImportReviewTransaction>>())).ReturnsAsync(4);
        dataProvider.Setup(dp => dp.BulkDeleteAsync(It.IsAny<IQueryable<ImportReviewTransaction>>())).ReturnsAsync(4);

        var count = await feature.DeleteAllAsync();

        Assert.That(count, Is.EqualTo(4));
        dataProvider.Verify(dp => dp.BulkDeleteAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()), Times.Once);
    }

    [Test]
    public async Task GetSummaryAsyncCountsAllStatusesAndSelection()
    {
        var (feature, dataProvider) = CreateFeatureWithMocks(tenantId: 42);
        dataProvider.Setup(dp => dp.Get<ImportReviewTransaction>()).Returns(Array.Empty<ImportReviewTransaction>().AsQueryable());
        dataProvider.SetupSequence(dp => dp.CountAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()))
            .ReturnsAsync(5)
            .ReturnsAsync(3)
            .ReturnsAsync(4)
            .ReturnsAsync(2)
            .ReturnsAsync(1);

        var result = await feature.GetSummaryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TotalCount, Is.EqualTo(5));
            Assert.That(result.SelectedCount, Is.EqualTo(3));
            Assert.That(result.NewCount, Is.EqualTo(4));
            Assert.That(result.ExactDuplicateCount, Is.EqualTo(2));
            Assert.That(result.PotentialDuplicateCount, Is.EqualTo(1));
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates an ImportReviewFeature instance with mocked dependencies.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to use. If null, uses a default value.</param>
    /// <returns>A tuple containing the feature instance and the mock data provider.</returns>
    private static (ImportReviewFeature Feature, Mock<IDataProvider> DataProvider) CreateFeatureWithMocks(
        long? tenantId = null, DateTimeOffset? utcNow = null)
    {
        var actualTenantId = tenantId ?? 1L;
        var tenant = new YoFi.V3.Entities.Tenancy.Models.Tenant { Id = actualTenantId, Name = "Test Tenant", Key = Guid.NewGuid() };

        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(tp => tp.CurrentTenant).Returns(tenant);

        var mockDataProvider = new Mock<IDataProvider>();
        mockDataProvider.Setup(dp => dp.SaveChangesAsync(default)).ReturnsAsync(1);

        var mockTransactionsFeature = new Mock<TransactionsFeature>(
            Mock.Of<ITenantProvider>(),
            Mock.Of<IDataProvider>());
        var mockPayeeMatchingService = new Mock<YoFi.V3.Application.Services.IPayeeMatchingService>();
        mockPayeeMatchingService
            .Setup(pms => pms.ApplyMatchingRulesAsync(It.IsAny<IReadOnlyCollection<YoFi.V3.Application.Services.IMatchableTransaction>>()))
            .ReturnsAsync((IReadOnlyCollection<YoFi.V3.Application.Services.IMatchableTransaction> transactions) =>
                transactions.Select(t => (string?)null).ToList());

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(time => time.GetUtcNow())
            .Returns(utcNow ?? new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));

        var feature = new ImportReviewFeature(
            mockTenantProvider.Object,
            mockDataProvider.Object,
            mockTransactionsFeature.Object,
            mockPayeeMatchingService.Object,
            timeProvider.Object);

        return (feature, mockDataProvider);
    }

    #endregion
}
