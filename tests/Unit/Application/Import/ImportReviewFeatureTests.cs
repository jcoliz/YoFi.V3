using Moq;
using NUnit.Framework;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
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
        Assert.That(result, Is.EqualTo(0));

        // And: Should add empty collection
        dataProvider.Verify(dp => dp.AddRange(It.Is<IEnumerable<ImportReviewTransaction>>(
            transactions => !transactions.Any())), Times.Once);

        // And: Should still save changes
        dataProvider.Verify(dp => dp.SaveChangesAsync(default), Times.Once);
    }

    [Test]
    public void SeedTestDataAsync_SelectedCountGreaterThanCount_ThrowsArgumentOutOfRangeException()
    {
        // Given: Mock dependencies and feature configured for invalid selectedCount
        var (feature, _) = CreateFeatureWithMocks();
        var count = 5;
        var selectedCount = 10;

        // When/Then: SeedTestDataAsync should throw ArgumentOutOfRangeException
        var ex = Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await feature.SeedTestDataAsync(count, selectedCount));

        // And: Exception should indicate valid range
        Assert.That(ex!.ParamName, Is.EqualTo("selectedCount"));
        Assert.That(ex.Message, Does.Contain("must be between 0 and 5"));
        Assert.That(ex.Message, Does.Contain("but was 10"));
    }

    [Test]
    public void SeedTestDataAsync_NegativeSelectedCount_ThrowsArgumentOutOfRangeException()
    {
        // Given: Mock dependencies and feature configured for negative selectedCount
        var (feature, _) = CreateFeatureWithMocks();
        var count = 5;
        var selectedCount = -1;

        // When/Then: SeedTestDataAsync should throw ArgumentOutOfRangeException
        var ex = Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
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

    [Test]
    public async Task SeedTestDataAsync_GeneratesDeterministicDates()
    {
        // Given: Mock dependencies and feature configured for transactions
        var (feature, dataProvider) = CreateFeatureWithMocks();
        var count = 32; // Test date cycling (more than 30 days)
        var capturedTransactions = new List<ImportReviewTransaction>();

        dataProvider.Setup(dp => dp.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback<IEnumerable<IModel>>(models =>
                capturedTransactions.AddRange(models.Cast<ImportReviewTransaction>()));

        // When: SeedTestDataAsync is called
        await feature.SeedTestDataAsync(count);

        // Then: Dates should cycle within 30-day window
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

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates an ImportReviewFeature instance with mocked dependencies.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to use. If null, uses a default value.</param>
    /// <returns>A tuple containing the feature instance and the mock data provider.</returns>
    private static (ImportReviewFeature Feature, Mock<IDataProvider> DataProvider) CreateFeatureWithMocks(long? tenantId = null)
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

        var feature = new ImportReviewFeature(
            mockTenantProvider.Object,
            mockDataProvider.Object,
            mockTransactionsFeature.Object,
            mockPayeeMatchingService.Object);

        return (feature, mockDataProvider);
    }

    #endregion
}
