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
public class ImportReviewFeatureTests : FeatureTestBase
{
    private ImportReviewFeature _feature = null!;
    private TransactionsFeature _transactionsFeature = null!;
    private ITenantProvider _tenantProvider = null!;
    private Tenant _testTenant = null!;
    private TestTimeProvider _timeProvider = null!;

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
        _timeProvider = new TestTimeProvider();
        _feature = new ImportReviewFeature(_tenantProvider, _dataProvider, _transactionsFeature, stubPayeeMatchingService, _timeProvider);
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



    #endregion

    #region CompleteReviewAsync Tests





    #endregion

    #region DeleteAllAsync Tests



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





    #endregion

    #region GetSummaryAsync Tests




    #endregion

    #region Integration Scenarios



    #endregion

    #region Category Matching Tests





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
