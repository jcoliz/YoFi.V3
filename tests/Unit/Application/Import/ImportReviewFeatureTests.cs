using NUnit.Framework;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;

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
}
