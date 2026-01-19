using Microsoft.EntityFrameworkCore;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Integration tests for ImportReviewFeature.
/// </summary>
/// <remarks>
/// Tests ImportReviewFeature business logic with real ApplicationDbContext
/// to verify duplicate detection, transaction import, and review workflow operations.
/// </remarks>
[TestFixture]
public class ImportReviewFeatureTests : FeatureTestBase
{
    private ImportReviewFeature _feature = null!;
    private TransactionsFeature _transactionsFeature = null!;
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
            Description = "Test tenant for import feature tests"
        };
        _context.Tenants.Add(_testTenant);
        await _context.SaveChangesAsync();

        // Create tenant provider
        _tenantProvider = new TestTenantProvider { CurrentTenant = _testTenant };

        // Create stub payee matching service (no-op for these tests)
        var stubPayeeMatchingService = new StubPayeeMatchingService();

        // Create features with real dependencies
        _transactionsFeature = new TransactionsFeature(_tenantProvider, _dataProvider);
        _feature = new ImportReviewFeature(_tenantProvider, _dataProvider, _transactionsFeature, stubPayeeMatchingService);
    }

    #region ImportFileAsync Tests

    [Test]
    public async Task ImportFileAsync_ValidOFXFile_ParsesAndStoresTransactions()
    {
        // Given: Valid OFX file stream
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);

        // When: Importing file through ImportReviewFeature
        var result = await _feature.ImportFileAsync(stream, "Bank1.ofx");

        // Then: Transactions should be parsed and imported
        Assert.That(result.ImportedCount, Is.GreaterThan(0));
        Assert.That(result.NewCount, Is.GreaterThan(0));

        // And: Transactions should be stored in database
        var importedTransactions = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(importedTransactions, Is.Not.Empty);
        Assert.That(importedTransactions.Count, Is.EqualTo(result.ImportedCount));
    }

    [Test]
    public async Task ImportFileAsync_EmptyFile_ReturnsZeroImported()
    {
        // Given: Empty stream
        using var emptyStream = new MemoryStream(Array.Empty<byte>());

        // When: Importing empty file
        var result = await _feature.ImportFileAsync(emptyStream, "empty.ofx");

        // Then: Zero transactions should be imported
        Assert.That(result.ImportedCount, Is.EqualTo(0));
        Assert.That(result.NewCount, Is.EqualTo(0));
        Assert.That(result.ExactDuplicateCount, Is.EqualTo(0));
        Assert.That(result.PotentialDuplicateCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ImportFileAsync_CorruptedOFX_HandlesGracefullyWithZeroImported()
    {
        // Given: Corrupted OFX content
        var corruptedContent = System.Text.Encoding.UTF8.GetBytes("<OFX><INVALID>Not proper OFX</BROKEN>");
        using var stream = new MemoryStream(corruptedContent);

        // When: Importing corrupted file
        var result = await _feature.ImportFileAsync(stream, "corrupted.ofx");

        // Then: Should handle gracefully with zero imported
        Assert.That(result.ImportedCount, Is.EqualTo(0));

        // And: Should report parsing errors
        Assert.That(result.Errors, Is.Not.Empty, "Corrupted OFX should generate parsing errors");
    }

    [Test]
    public async Task ImportFileAsync_NewTransactions_MarksAllAsNew()
    {
        // Given: Valid OFX file with no duplicates
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);

        // When: Importing file
        var result = await _feature.ImportFileAsync(stream, "Bank1.ofx");

        // Then: All transactions should be marked as New
        Assert.That(result.NewCount, Is.EqualTo(result.ImportedCount));
        Assert.That(result.ExactDuplicateCount, Is.EqualTo(0));
        Assert.That(result.PotentialDuplicateCount, Is.EqualTo(0));

        // And: All should be selected by default
        var importedTransactions = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(importedTransactions.All(t => t.IsSelected), Is.True,
            "New transactions should be selected by default");
    }

    [Test]
    public async Task ImportFileAsync_WithExactDuplicate_DetectsAndMarksCorrectly()
    {
        // Given: Existing transaction with external ID
        var existingTransaction = new Transaction
        {
            Date = new DateOnly(2022, 2, 21),
            Amount = -87.69m,
            Payee = "SAFEWAY",
            ExternalId = "20220221 469976 8,769 2,022,022,018,019",
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(existingTransaction);
        await _context.SaveChangesAsync();

        // When: Importing OFX file containing same transaction
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);
        var result = await _feature.ImportFileAsync(stream, "Bank1.ofx");

        // Then: Duplicate should be detected
        Assert.That(result.ExactDuplicateCount + result.PotentialDuplicateCount, Is.GreaterThan(0));

        // And: Duplicate transactions should NOT be selected by default
        var duplicates = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id
                && t.DuplicateStatus != DuplicateStatus.New)
            .ToListAsync();
        Assert.That(duplicates.All(t => !t.IsSelected), Is.True,
            "Duplicate transactions should not be selected by default");
    }

    [Test]
    public async Task ImportFileAsync_WithMultipleDuplicateSameExternalId_HandlesCorrectly()
    {
        // Given: Two transactions with SAME ExternalId and SAME Date (regression test for AB#1992)
        var sharedExternalId = "20220221 469976 8,769 2,022,022,018,019";
        var sharedDate = new DateOnly(2022, 2, 21);

        var transaction1 = new Transaction
        {
            Date = sharedDate,
            Amount = -87.69m,
            Payee = "DUPLICATE-1",
            ExternalId = sharedExternalId,
            TenantId = _testTenant.Id
        };
        var transaction2 = new Transaction
        {
            Date = sharedDate,
            Amount = -87.69m,
            Payee = "DUPLICATE-2",
            ExternalId = sharedExternalId,
            TenantId = _testTenant.Id
        };
        _context.Transactions.AddRange(transaction1, transaction2);
        await _context.SaveChangesAsync();

        // When: Importing OFX file with that ExternalId
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);

        // Then: Should not throw exception (bug was: ToDictionary duplicate key)
        var result = await _feature.ImportFileAsync(stream, "Bank1.ofx");

        // And: Should detect duplicate
        Assert.That(result.ExactDuplicateCount + result.PotentialDuplicateCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task ImportFileAsync_TenantIsolation_OnlyStoresForCurrentTenant()
    {
        // Given: Two tenants
        var otherTenant = new Tenant { Name = "Other Tenant", Description = "Other" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();

        // When: Importing file for current tenant
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);
        var result = await _feature.ImportFileAsync(stream, "Bank1.ofx");

        // Then: Transactions should only be for current tenant
        var allImports = await _context.ImportReviewTransactions.ToListAsync();
        Assert.That(allImports.All(t => t.TenantId == _testTenant.Id), Is.True);
        Assert.That(allImports.Count, Is.EqualTo(result.ImportedCount));
    }

    #endregion

    #region GetPendingReviewAsync Tests

    [Test]
    public async Task GetPendingReviewAsync_WithPendingTransactions_ReturnsPaginatedResults()
    {
        // Given: Multiple import review transactions
        await _feature.SeedTestDataAsync(10);

        // When: Getting pending review
        var result = await _feature.GetPendingReviewAsync();

        // Then: Should return paginated results
        Assert.That(result.Items, Is.Not.Empty);
        Assert.That(result.Metadata, Is.Not.Null);
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(10));
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(1));
        Assert.That(result.Metadata.PageSize, Is.EqualTo(50));
    }

    [Test]
    public async Task GetPendingReviewAsync_NoPendingTransactions_ReturnsEmptyList()
    {
        // Given: No pending transactions

        // When: Getting pending review
        var result = await _feature.GetPendingReviewAsync();

        // Then: Should return empty list
        Assert.That(result.Items, Is.Empty);
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GetPendingReviewAsync_WithPagination_ReturnsCorrectPage()
    {
        // Given: 100 import review transactions
        await _feature.SeedTestDataAsync(100);

        // When: Getting page 2
        var result = await _feature.GetPendingReviewAsync(pageNumber: 2);

        // Then: Should return page 2
        Assert.That(result.Items.Count, Is.EqualTo(50));
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(2));
        Assert.That(result.Metadata.PageSize, Is.EqualTo(50));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(100));
        Assert.That(result.Metadata.TotalPages, Is.EqualTo(2));
    }

    [Test]
    public async Task GetPendingReviewAsync_InvalidPageNumber_DefaultsToPage1()
    {
        // Given: Pending transactions
        await _feature.SeedTestDataAsync(10);

        // When: Requesting page 0 or negative
        var result = await _feature.GetPendingReviewAsync(pageNumber: 0);

        // Then: Should default to page 1
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(1));
    }


    [Test]
    public async Task GetPendingReviewAsync_TenantIsolation_OnlyReturnsCurrentTenantTransactions()
    {
        // Given: Transactions for two tenants
        var otherTenant = new Tenant { Name = "Other Tenant", Description = "Other" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();

        // And: Import for current tenant
        await _feature.SeedTestDataAsync(5);

        // And: Import for other tenant
        var otherImport = new ImportReviewTransaction
        {
            TenantId = otherTenant.Id,
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Other Tenant Transaction",
            Amount = 100m,
            DuplicateStatus = DuplicateStatus.New
        };
        _context.ImportReviewTransactions.Add(otherImport);
        await _context.SaveChangesAsync();

        // When: Getting pending review
        var result = await _feature.GetPendingReviewAsync();

        // Then: Should only return current tenant's transactions
        Assert.That(result.Items.Count, Is.EqualTo(5));
        Assert.That(result.Items.All(t => t.Payee != "Other Tenant Transaction"), Is.True);
    }

    [Test]
    public async Task GetPendingReviewAsync_OrdersByDateDescending()
    {
        // Given: Transactions with different dates
        var baseDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var imports = new List<ImportReviewTransaction>
        {
            new() { TenantId = _testTenant.Id, Date = baseDate.AddDays(-2), Payee = "Oldest", Amount = 100m, DuplicateStatus = DuplicateStatus.New },
            new() { TenantId = _testTenant.Id, Date = baseDate, Payee = "Newest", Amount = 200m, DuplicateStatus = DuplicateStatus.New },
            new() { TenantId = _testTenant.Id, Date = baseDate.AddDays(-1), Payee = "Middle", Amount = 150m, DuplicateStatus = DuplicateStatus.New }
        };
        _context.ImportReviewTransactions.AddRange(imports);
        await _context.SaveChangesAsync();

        // When: Getting pending review
        var result = await _feature.GetPendingReviewAsync();

        // Then: Should be ordered by date descending
        var items = result.Items.ToList();
        Assert.That(items[0].Payee, Is.EqualTo("Newest"));
        Assert.That(items[1].Payee, Is.EqualTo("Middle"));
        Assert.That(items[2].Payee, Is.EqualTo("Oldest"));
    }

    [Test]
    public async Task GetPendingReviewAsync_ReturnsAllDtoFieldsCorrectly()
    {
        // Given: Import review transaction with known values
        var duplicateKey = Guid.NewGuid();
        var transaction = new ImportReviewTransaction
        {
            TenantId = _testTenant.Id,
            Date = new DateOnly(2024, 3, 15),
            Payee = "Test Store",
            Amount = 123.45m,
            Source = "Test Bank",
            ExternalId = "FITID123",
            Memo = "Test memo",
            DuplicateStatus = DuplicateStatus.PotentialDuplicate,
            DuplicateOfKey = duplicateKey,
            IsSelected = false
        };
        _context.ImportReviewTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        // When: Getting pending review
        var result = await _feature.GetPendingReviewAsync();

        // Then: All DTO fields should match entity values
        var dto = result.Items.Single();
        Assert.That(dto.Key, Is.EqualTo(transaction.Key));
        Assert.That(dto.Date, Is.EqualTo(new DateOnly(2024, 3, 15)));
        Assert.That(dto.Payee, Is.EqualTo("Test Store"));
        Assert.That(dto.Category, Is.EqualTo(string.Empty)); // Placeholder for future Payee Matching rules
        Assert.That(dto.Amount, Is.EqualTo(123.45m));
        Assert.That(dto.DuplicateStatus, Is.EqualTo(DuplicateStatus.PotentialDuplicate));
        Assert.That(dto.DuplicateOfKey, Is.EqualTo(duplicateKey));
        Assert.That(dto.IsSelected, Is.False);
    }

    #endregion

    #region CompleteReviewAsync Tests

    [Test]
    public async Task CompleteReviewAsync_WithSelectedTransactions_AcceptsOnlySelected()
    {
        // Given: Import review transactions with some selected
        await _feature.SeedTestDataAsync(count: 10, selectedCount: 3);

        // When: Completing review
        var result = await _feature.CompleteReviewAsync();

        // Then: Only selected transactions should be accepted
        Assert.That(result.AcceptedCount, Is.EqualTo(3));
        Assert.That(result.RejectedCount, Is.EqualTo(7));

        // And: Accepted transactions should be in Transactions table
        var transactions = await _context.Transactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(transactions.Count, Is.EqualTo(3));

        // And: All import review transactions should be deleted
        var remainingImports = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(remainingImports, Is.Empty);
    }

    [Test]
    public async Task CompleteReviewAsync_NoSelection_AcceptsNone()
    {
        // Given: Import review transactions with none selected
        await _feature.SeedTestDataAsync(count: 10, selectedCount: 0);

        // When: Completing review
        var result = await _feature.CompleteReviewAsync();

        // Then: No transactions should be accepted
        Assert.That(result.AcceptedCount, Is.EqualTo(0));
        Assert.That(result.RejectedCount, Is.EqualTo(10));

        // And: No transactions in Transactions table
        var transactions = await _context.Transactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(transactions, Is.Empty);

        // And: All import review transactions should be deleted
        var remainingImports = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(remainingImports, Is.Empty);
    }

    [Test]
    public async Task CompleteReviewAsync_AcceptedTransactionsHaveSplits()
    {
        // Given: Import review transactions selected
        await _feature.SeedTestDataAsync(count: 2, selectedCount: 2);

        // When: Completing review
        var result = await _feature.CompleteReviewAsync();

        // Then: Accepted transactions should have splits created
        var transactions = await _context.Transactions
            .Include(t => t.Splits)
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(transactions.Count, Is.EqualTo(2));
        Assert.That(transactions.All(t => t.Splits.Any()), Is.True,
            "All accepted transactions should have splits");
    }

    [Test]
    public async Task CompleteReviewAsync_TenantIsolation_OnlyAffectsCurrentTenant()
    {
        // Given: Two tenants with import review transactions
        var otherTenant = new Tenant { Name = "Other Tenant", Description = "Other" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();

        await _feature.SeedTestDataAsync(count: 5, selectedCount: 5);

        var otherImport = new ImportReviewTransaction
        {
            TenantId = otherTenant.Id,
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Other Tenant Transaction",
            Amount = 100m,
            DuplicateStatus = DuplicateStatus.New,
            IsSelected = true
        };
        _context.ImportReviewTransactions.Add(otherImport);
        await _context.SaveChangesAsync();

        // When: Completing review for current tenant
        var result = await _feature.CompleteReviewAsync();

        // Then: Only current tenant's transactions should be affected
        Assert.That(result.AcceptedCount, Is.EqualTo(5));

        // And: Other tenant's import should remain
        var otherTenantImports = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == otherTenant.Id)
            .ToListAsync();
        Assert.That(otherTenantImports, Has.Count.EqualTo(1));
    }

    #endregion

    #region DeleteAllAsync Tests

    [Test]
    public async Task DeleteAllAsync_WithPendingTransactions_DeletesAllForTenant()
    {
        // Given: Import review transactions
        await _feature.SeedTestDataAsync(10);

        // When: Deleting all
        var deletedCount = await _feature.DeleteAllAsync();

        // Then: All transactions should be deleted
        Assert.That(deletedCount, Is.EqualTo(10));

        var remaining = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(remaining, Is.Empty);
    }

    [Test]
    public async Task DeleteAllAsync_EmptyQueue_ReturnsZero()
    {
        // Given: No pending transactions

        // When: Deleting all
        var deletedCount = await _feature.DeleteAllAsync();

        // Then: Should return zero
        Assert.That(deletedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task DeleteAllAsync_TenantIsolation_OnlyDeletesCurrentTenant()
    {
        // Given: Two tenants with imports
        var otherTenant = new Tenant { Name = "Other Tenant", Description = "Other" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();

        await _feature.SeedTestDataAsync(5);

        var otherImport = new ImportReviewTransaction
        {
            TenantId = otherTenant.Id,
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Other Tenant",
            Amount = 100m,
            DuplicateStatus = DuplicateStatus.New
        };
        _context.ImportReviewTransactions.Add(otherImport);
        await _context.SaveChangesAsync();

        // When: Deleting all for current tenant
        await _feature.DeleteAllAsync();

        // Then: Other tenant's import should remain
        var otherTenantImports = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == otherTenant.Id)
            .ToListAsync();
        Assert.That(otherTenantImports, Has.Count.EqualTo(1));
    }

    #endregion

    #region Selection Management Tests

    [Test]
    public async Task SetSelectionAsync_ValidKeys_UpdatesSelectionState()
    {
        // Given: Import review transactions
        await _feature.SeedTestDataAsync(5);
        var transactions = await _context.ImportReviewTransactions.ToListAsync();
        var keysToSelect = transactions.Take(2).Select(t => t.Key).ToList();

        // When: Setting selection
        await _feature.SetSelectionAsync(keysToSelect, isSelected: true);

        // Then: Selection state should be updated
        _context.ChangeTracker.Clear();
        var updated = await _context.ImportReviewTransactions
            .Where(t => keysToSelect.Contains(t.Key))
            .ToListAsync();
        Assert.That(updated.All(t => t.IsSelected), Is.True);
    }

    [Test]
    public async Task SetSelectionAsync_Deselect_UpdatesSelectionState()
    {
        // Given: Import review transactions (all selected by default from seed)
        await _feature.SeedTestDataAsync(count: 5, selectedCount: 5);
        var transactions = await _context.ImportReviewTransactions.ToListAsync();
        var keysToDeselect = transactions.Take(2).Select(t => t.Key).ToList();

        // When: Deselecting
        await _feature.SetSelectionAsync(keysToDeselect, isSelected: false);

        // Then: Selection state should be updated
        _context.ChangeTracker.Clear();
        var updated = await _context.ImportReviewTransactions
            .Where(t => keysToDeselect.Contains(t.Key))
            .ToListAsync();
        Assert.That(updated.All(t => !t.IsSelected), Is.True);
    }

    [Test]
    public async Task SelectAllAsync_MarksAllAsSelected()
    {
        // Given: Import review transactions with mixed selection
        await _feature.SeedTestDataAsync(count: 10, selectedCount: 5);

        // When: Selecting all
        await _feature.SelectAllAsync();

        // Then: All transactions should be selected
        _context.ChangeTracker.Clear();
        var all = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(all.All(t => t.IsSelected), Is.True);
    }

    [Test]
    public async Task DeselectAllAsync_MarksAllAsDeselected()
    {
        // Given: Import review transactions (all selected)
        await _feature.SeedTestDataAsync(count: 10, selectedCount: 10);

        // When: Deselecting all
        await _feature.DeselectAllAsync();

        // Then: All transactions should be deselected
        _context.ChangeTracker.Clear();
        var all = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(all.All(t => !t.IsSelected), Is.True);
    }

    #endregion

    #region GetSummaryAsync Tests

    [Test]
    public async Task GetSummaryAsync_WithPendingTransactions_ReturnsAccurateCounts()
    {
        // Given: Import review transactions with mixed states
        await _feature.SeedTestDataAsync(count: 10, selectedCount: 3);

        // When: Getting summary
        var summary = await _feature.GetSummaryAsync();

        // Then: Counts should be accurate
        Assert.That(summary.TotalCount, Is.EqualTo(10));
        Assert.That(summary.SelectedCount, Is.EqualTo(3));
        Assert.That(summary.NewCount, Is.EqualTo(10)); // All seeded are new
    }

    [Test]
    public async Task GetSummaryAsync_NoPendingTransactions_ReturnsZeroCounts()
    {
        // Given: No pending transactions

        // When: Getting summary
        var summary = await _feature.GetSummaryAsync();

        // Then: All counts should be zero
        Assert.That(summary.TotalCount, Is.EqualTo(0));
        Assert.That(summary.SelectedCount, Is.EqualTo(0));
        Assert.That(summary.NewCount, Is.EqualTo(0));
        Assert.That(summary.ExactDuplicateCount, Is.EqualTo(0));
        Assert.That(summary.PotentialDuplicateCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GetSummaryAsync_WithDuplicates_CountsByStatus()
    {
        // Given: Import review transactions with different duplicate statuses
        var imports = new List<ImportReviewTransaction>
        {
            new() { TenantId = _testTenant.Id, Date = DateOnly.FromDateTime(DateTime.Now), Payee = "New1", Amount = 100m, DuplicateStatus = DuplicateStatus.New, IsSelected = true },
            new() { TenantId = _testTenant.Id, Date = DateOnly.FromDateTime(DateTime.Now), Payee = "New2", Amount = 200m, DuplicateStatus = DuplicateStatus.New, IsSelected = true },
            new() { TenantId = _testTenant.Id, Date = DateOnly.FromDateTime(DateTime.Now), Payee = "Exact", Amount = 300m, DuplicateStatus = DuplicateStatus.ExactDuplicate, IsSelected = false },
            new() { TenantId = _testTenant.Id, Date = DateOnly.FromDateTime(DateTime.Now), Payee = "Potential", Amount = 400m, DuplicateStatus = DuplicateStatus.PotentialDuplicate, IsSelected = false }
        };
        _context.ImportReviewTransactions.AddRange(imports);
        await _context.SaveChangesAsync();

        // When: Getting summary
        var summary = await _feature.GetSummaryAsync();

        // Then: Counts should be correct by status
        Assert.That(summary.TotalCount, Is.EqualTo(4));
        Assert.That(summary.SelectedCount, Is.EqualTo(2));
        Assert.That(summary.NewCount, Is.EqualTo(2));
        Assert.That(summary.ExactDuplicateCount, Is.EqualTo(1));
        Assert.That(summary.PotentialDuplicateCount, Is.EqualTo(1));
    }

    #endregion

    #region Integration Scenarios

    [Test]
    public async Task MultipleUploads_MergeIntoSingleReviewQueue()
    {
        // Given: First upload
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream1 = new MemoryStream(ofxContent);
        var upload1Result = await _feature.ImportFileAsync(stream1, "Bank1.ofx");

        // When: Second upload
        using var stream2 = new MemoryStream(ofxContent);
        await _feature.ImportFileAsync(stream2, "Bank1-second.ofx");

        // Then: Review queue should contain transactions from both uploads
        var allImports = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(allImports.Count, Is.GreaterThanOrEqualTo(upload1Result.ImportedCount));
    }

    [Test]
    public async Task CompleteReview_TransactionsInReview_NotIncludedInTransactionList()
    {
        // Given: Accepted transaction
        var acceptedDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Accepted Transaction",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        await _transactionsFeature.AddTransactionAsync(acceptedDto);

        // And: Pending import review transactions
        await _feature.SeedTestDataAsync(5);

        // When: Getting transactions (not import review)
        var transactions = await _transactionsFeature.GetTransactionsAsync();

        // Then: Should only return accepted transactions
        Assert.That(transactions.Items, Has.Count.EqualTo(1));
        Assert.That(transactions.Items.First().Payee, Is.EqualTo("Accepted Transaction"));

        // And: Import review transactions should be separate
        var imports = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();
        Assert.That(imports, Has.Count.EqualTo(5));
    }

    #endregion

    #region Category Matching Tests

    [Test]
    public async Task ImportFileAsync_WithMatchingCategories_StoresCategoriesCorrectly()
    {
        // Given: Payee matching service that returns categories for specific payees
        var matchingService = new CategoryMatchingPayeeMatchingService();
        var featureWithMatching = new ImportReviewFeature(_tenantProvider, _dataProvider, _transactionsFeature, matchingService);

        // And: Valid OFX file with QFC transaction (matches "Groceries")
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);

        // When: Importing file with category matching enabled
        var result = await featureWithMatching.ImportFileAsync(stream, "Bank1.ofx");

        // Then: Transactions should be imported
        Assert.That(result.ImportedCount, Is.GreaterThan(0));

        // And: QFC transaction should have "Groceries" category
        var importedTransactions = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();

        var qfcTransaction = importedTransactions.FirstOrDefault(t => t.Payee.Contains("QFC"));
        Assert.That(qfcTransaction, Is.Not.Null, "QFC transaction should exist in Bank1.ofx");
        Assert.That(qfcTransaction!.Category, Is.EqualTo("Groceries"), "QFC should be categorized as Groceries");
    }

    [Test]
    public async Task ImportFileAsync_WithNoMatchingCategories_StoresNullCategories()
    {
        // Given: Payee matching service that returns null for all payees (no match)
        var matchingService = new StubPayeeMatchingService();
        var featureWithNoMatching = new ImportReviewFeature(_tenantProvider, _dataProvider, _transactionsFeature, matchingService);

        // And: Valid OFX file
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);

        // When: Importing file without category matching
        var result = await featureWithNoMatching.ImportFileAsync(stream, "Bank1.ofx");

        // Then: Transactions should be imported
        Assert.That(result.ImportedCount, Is.GreaterThan(0));

        // And: All transactions should have null categories
        var importedTransactions = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();

        Assert.That(importedTransactions.All(t => t.Category == null), Is.True,
            "All transactions should have null category when no rules match");
    }

    [Test]
    public async Task ImportFileAsync_WithMixedMatching_StoresCorrectCategoriesPerTransaction()
    {
        // Given: Payee matching service that returns categories for some payees only
        var matchingService = new CategoryMatchingPayeeMatchingService();
        var featureWithMatching = new ImportReviewFeature(_tenantProvider, _dataProvider, _transactionsFeature, matchingService);

        // And: Valid OFX file with multiple transactions
        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);

        // When: Importing file
        var result = await featureWithMatching.ImportFileAsync(stream, "Bank1.ofx");

        // Then: Transactions should be imported
        Assert.That(result.ImportedCount, Is.GreaterThan(0));

        // And: Different transactions should have appropriate categories
        var importedTransactions = await _context.ImportReviewTransactions
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();

        // And: Matched transactions should have categories
        var matchedCount = importedTransactions.Count(t => t.Category != null);
        Assert.That(matchedCount, Is.GreaterThan(0), "Some transactions should have matched categories");

        // And: Unmatched transactions should have null categories
        var unmatchedCount = importedTransactions.Count(t => t.Category == null);
        Assert.That(unmatchedCount, Is.GreaterThan(0), "Some transactions should not have matched categories");
    }

    [Test]
    public async Task CompleteReviewAsync_WithMatchedCategories_TransfersCategoriesToAcceptedTransactions()
    {
        // Given: Import review transaction with matched category
        var matchingService = new CategoryMatchingPayeeMatchingService();
        var featureWithMatching = new ImportReviewFeature(_tenantProvider, _dataProvider, _transactionsFeature, matchingService);

        var ofxFilePath = Path.Combine("SampleData", "Ofx", "Bank1.ofx");
        var ofxContent = await File.ReadAllBytesAsync(ofxFilePath);
        using var stream = new MemoryStream(ofxContent);
        await featureWithMatching.ImportFileAsync(stream, "Bank1.ofx");

        // And: Select all transactions
        await featureWithMatching.SelectAllAsync();

        // When: Completing review
        await featureWithMatching.CompleteReviewAsync();

        // Then: Accepted transactions should preserve matched categories
        var acceptedTransactions = await _context.Transactions
            .Include(t => t.Splits)
            .Where(t => t.TenantId == _testTenant.Id)
            .ToListAsync();

        var qfcTransaction = acceptedTransactions.FirstOrDefault(t => t.Payee.Contains("QFC"));
        Assert.That(qfcTransaction, Is.Not.Null, "QFC transaction should exist");

        // Category is stored in splits, verify the split has the correct category
        var categorySplit = qfcTransaction!.Splits.FirstOrDefault();
        Assert.That(categorySplit, Is.Not.Null);
        Assert.That(categorySplit!.Category, Is.EqualTo("Groceries"),
            "Matched category should be transferred to accepted transaction's split");
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

/// <summary>
/// Stub implementation of IPayeeMatchingService for integration tests.
/// Returns null categories (no matching).
/// </summary>
file class StubPayeeMatchingService : IPayeeMatchingService
{
    public Task<IReadOnlyList<string?>> ApplyMatchingRulesAsync(
        IReadOnlyCollection<IMatchableTransaction> transactions)
    {
        // No-op: return null categories for all transactions (no matching)
        IReadOnlyList<string?> result = transactions.Select(_ => (string?)null).ToList();
        return Task.FromResult(result);
    }
}

/// <summary>
/// Test implementation of IPayeeMatchingService that returns specific categories for known payees.
/// Used to test category matching functionality.
/// Matches payees from Bank1.ofx sample file.
/// </summary>
file class CategoryMatchingPayeeMatchingService : IPayeeMatchingService
{
    public Task<IReadOnlyList<string?>> ApplyMatchingRulesAsync(
        IReadOnlyCollection<IMatchableTransaction> transactions)
    {
        // Return categories for known payees (from Bank1.ofx), null for unknown
        // Use substring matching since OFX payees contain additional text
        IReadOnlyList<string?> result = transactions
            .Select(t => GetCategoryForPayee(t.Payee))
            .ToList();
        return Task.FromResult(result);
    }

    private static string? GetCategoryForPayee(string payee)
    {
        // Match based on substrings found in Bank1.ofx
        if (payee.Contains("QFC", StringComparison.OrdinalIgnoreCase))
            return "Groceries";
        if (payee.Contains("CHEVRON", StringComparison.OrdinalIgnoreCase))
            return "Auto:Fuel";
        if (payee.Contains("JAMBA", StringComparison.OrdinalIgnoreCase))
            return "Dining:Coffee";
        if (payee.Contains("GARDEN", StringComparison.OrdinalIgnoreCase))
            return "Home:Garden";

        return null; // No match
    }
}
