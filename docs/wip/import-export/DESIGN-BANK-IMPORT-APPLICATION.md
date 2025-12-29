---
status: Draft
layer: Application
parent: DESIGN-BANK-IMPORT.md
created: 2025-12-28
related_docs:
  - DESIGN-BANK-IMPORT.md
  - DESIGN-BANK-IMPORT-DATABASE.md
  - PRD-BANK-IMPORT.md
---

# Application Layer Design: Bank Import Feature

## Overview

This document provides the complete application layer design for the Bank Import feature, including business logic, DTOs, and the [`ImportReviewFeature`](src/Application/Import/Features/ImportReviewFeature.cs) class. The application layer orchestrates the import workflow: parsing OFX files, detecting duplicates, managing pending review transactions, and accepting transactions into the main transaction table.

**Key components:**
- **DTOs** - [`ImportReviewTransactionDto`](src/Application/Import/Dto/ImportReviewTransactionDto.cs), [`ImportResultDto`](src/Application/Import/Dto/ImportResultDto.cs), [`AcceptTransactionsResultDto`](src/Application/Import/Dto/AcceptTransactionsResultDto.cs)
- **ImportReviewFeature** - Business logic for import workflow orchestration
- **Duplicate Detection Strategy** - Two-phase detection using ExternalId and field matching
- **Service Registration** - Integration with [`ServiceCollectionExtensions`](src/Application/ServiceCollectionExtensions.cs)

**Layer responsibilities:**
- Parse OFX files using [`OfxParsingService`](src/Application/Import/Services/OfxParsingService.cs) (already implemented)
- Detect duplicates against existing transactions and pending imports
- Store parsed transactions in [`ImportReviewTransaction`](src/Entities/Models/ImportReviewTransaction.cs) staging table
- Provide CRUD operations for pending review transactions
- Accept selected transactions into main [`Transaction`](src/Entities/Models/Transaction.cs) table
- Generate default splits when accepting transactions

## Data flow

OFX File (via OfxParsingService)
  → TransactionImportDto (INPUT to ImportReviewFeature)
    → ImportReviewTransaction entity (stored in database)
      → ImportReviewTransactionDto (OUTPUT from ImportReviewFeature)
        → API Controllers
          → Frontend

## DTOs

### ImportReviewTransactionDto

Location: `src/Application/Import/Dto/ImportReviewTransactionDto.cs`

```csharp
namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Presents information about an imported transactions for user review.
/// </summary>
/// <param name="Key">The unique identifier for the import review transaction.</param>
/// <param name="Date">Transaction date as reported by the bank.</param>
/// <param name="Payee">Payee or merchant name for the transaction.</param>
/// <param name="Amount">Transaction amount (positive for deposits, negative for withdrawals).</param>
/// <param name="Source">Source of the import (e.g., bank name and account type).</param>
/// <param name="ExternalId">External transaction ID from the bank (OFX FITID field), used for duplicate detection.</param>
/// <param name="Memo">Memo or notes field from the bank statement.</param>
/// <param name="DuplicateStatus">Status indicating whether this transaction is new or a duplicate.</param>
/// <param name="DuplicateOfKey">Key of the existing transaction if this is detected as a duplicate.</param>
/// <param name="ImportedAt">Timestamp when the import file was uploaded and parsed.</param>
public record ImportReviewTransactionDto(
    Guid Key,
    DateOnly Date,
    string Payee,
    decimal Amount,
    string? Source,
    string? ExternalId,
    string? Memo,
    DuplicateStatus DuplicateStatus,
    Guid? DuplicateOfKey,
    DateTime ImportedAt
);
```

### ImportResultDto

Location: `src/Application/Import/Dto/ImportResultDto.cs`

```csharp
namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Result of an OFX file import operation, to be shown to the user.
/// </summary>
/// <param name="ImportedCount">Number of transactions successfully imported and stored for review.</param>
/// <param name="NewCount">Number of new transactions (no duplicates detected).</param>
/// <param name="ExactDuplicateCount">Number of exact duplicate transactions detected.</param>
/// <param name="PotentialDuplicateCount">Number of potential duplicate transactions detected.</param>
/// <param name="Errors">Collection of errors encountered during OFX parsing.</param>
/// <remarks>
/// The sum of NewCount, ExactDuplicateCount, and PotentialDuplicateCount equals ImportedCount.
/// Errors indicate problems parsing individual transactions or the OFX file structure, but do not prevent
/// successfully parsed transactions from being imported.
/// </remarks>
public record ImportResultDto(
    int ImportedCount,
    int NewCount,
    int ExactDuplicateCount,
    int PotentialDuplicateCount,
    IReadOnlyCollection<OfxParsingError> Errors
);
```

### AcceptTransactionsResultDto

Location: `src/Application/Import/Dto/AcceptTransactionsResultDto.cs`

```csharp
namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Result of accepting import review transactions into the main transaction table. This is to show
/// the user an informative display of the outcome of the action they took on the reviewed transactions.
/// </summary>
/// <param name="AcceptedCount">Number of transactions successfully accepted and copied to the main transaction table.</param>
/// <param name="DeletedCount">Number of import review transactions deleted after acceptance.</param>
/// <remarks>
/// AcceptedCount and DeletedCount should normally be equal (each accepted transaction is deleted from review).
/// If counts differ, it may indicate a partial failure requiring investigation.
/// </remarks>
public record AcceptTransactionsResultDto(
    int AcceptedCount,
    int DeletedCount
);
```

## ImportReviewFeature Class

Location: `src/Application/Import/Features/ImportReviewFeature.cs`

```csharp
using YoFi.V3.Application.Import.Dto;
using YoFi.V3.Application.Import.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Import.Features;

/// <summary>
/// Provides business logic for the bank import review workflow.
/// </summary>
/// <param name="tenantProvider">Provider for accessing tenant context information.</param>
/// <param name="dataProvider">Repository for data operations on transactions and import review transactions.</param>
/// <param name="ofxParsingService">Service for parsing OFX/QFX files.</param>
/// <remarks>
/// <para>
/// This feature orchestrates the complete import workflow:
/// </para>
/// <list type="number">
/// <item>Parse OFX file to extract transaction data</item>
/// <item>Detect duplicates against existing transactions and pending imports</item>
/// <item>Store transactions in ImportReviewTransaction staging table with duplicate status</item>
/// <item>Provide operations to retrieve, accept, or delete pending review transactions</item>
/// </list>
/// <para>
/// <strong>Tenant Isolation:</strong> All operations are scoped to the current authenticated user's tenant
/// via ITenantProvider. The TenantId is automatically applied to all queries and inserts.
/// </para>
/// <para>
/// <strong>Duplicate Detection:</strong> Uses a two-phase strategy (ExternalId matching, then field matching)
/// to classify transactions as New, ExactDuplicate, or PotentialDuplicate. See DetectDuplicate method for details.
/// </para>
/// <para>
/// <strong>Default Split Creation:</strong> When accepting transactions, a single Split record is created per
/// transaction with the full amount and no category assignment. Users can edit categories later.
/// </para>
/// </remarks>
public class ImportReviewFeature(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IOfxParsingService ofxParsingService)
{
    /// <summary>
    /// Imports an OFX file, parses transactions, detects duplicates, and stores them for review.
    /// </summary>
    /// <param name="fileStream">The stream containing the OFX/QFX file data.</param>
    /// <param name="fileName">The name of the uploaded file.</param>
    /// <returns>An <see cref="ImportResultDto"/> containing import statistics and any parsing errors.</returns>
    /// <exception cref="InvalidOperationException">Thrown when tenant context is not available.</exception>
    /// <remarks>
    /// <para>
    /// This method performs the following steps:
    /// </para>
    /// <list type="number">
    /// <item>Parse the OFX file using OfxParsingService</item>
    /// <item>Load existing transactions and pending imports for the current tenant</item>
    /// <item>For each parsed transaction, detect duplicates using two-phase strategy</item>
    /// <item>Create ImportReviewTransaction records with appropriate DuplicateStatus</item>
    /// <item>Store all import review transactions in the database</item>
    /// <item>Return summary statistics and any parsing errors</item>
    /// </list>
    /// <para>
    /// All operations are scoped to the current tenant via ITenantProvider.TenantId.
    /// If parsing errors occur, they are included in the OFXParsingResult but do not prevent
    /// import of successfully parsed transactions.
    /// </para>
    /// </remarks>
    public async Task<ImportResultDto> ImportFileAsync(Stream fileStream, string fileName);

    /// <summary>
    /// Retrieves all pending import review transactions for the current tenant.
    /// </summary>
    /// <returns>A collection of <see cref="ImportReviewTransactionDto"/> ordered by date descending.</returns>
    /// <exception cref="InvalidOperationException">Thrown when tenant context is not available.</exception>
    public async Task<IReadOnlyCollection<ImportReviewTransactionDto>> GetPendingReviewAsync();

    /// <summary>
    /// Accepts selected import review transactions, copies them to the main transaction table, and deletes them from review.
    /// </summary>
    /// <param name="keys">The collection of transaction keys to accept.</param>
    /// <returns>An <see cref="AcceptTransactionsResultDto"/> with counts of accepted and deleted transactions.</returns>
    /// <exception cref="InvalidOperationException">Thrown when tenant context is not available.</exception>
    /// <remarks>
    /// <para>
    /// This method performs an atomic operation:
    /// </para>
    /// <list type="number">
    /// <item>Retrieve import review transactions by keys (with tenant isolation)</item>
    /// <item>For each transaction, create a new Transaction record with a default Split</item>
    /// <item>Add all new transactions to the main Transaction table</item>
    /// <item>Delete the import review transactions</item>
    /// </list>
    /// <para>
    /// <strong>Default Split Creation:</strong> Each accepted transaction gets a single Split with:
    /// </para>
    /// <list type="bullet">
    /// <item>Amount = Transaction.Amount (full amount)</item>
    /// <item>Category = null (user can assign later)</item>
    /// </list>
    /// <para>
    /// The Transaction.Key is preserved from the import review transaction to maintain client-side tracking.
    /// </para>
    /// </remarks>
    public async Task<AcceptTransactionsResultDto> AcceptTransactionsAsync(IReadOnlyCollection<Guid> keys);

    /// <summary>
    /// Deletes selected import review transactions without accepting them.
    /// </summary>
    /// <param name="keys">The collection of transaction keys to delete.</param>
    /// <exception cref="InvalidOperationException">Thrown when tenant context is not available.</exception>
    /// <remarks>
    /// Use this method to reject transactions that the user does not want to import
    /// (e.g., confirmed duplicates or irrelevant transactions).
    /// </remarks>
    public async Task DeleteTransactionsAsync(IReadOnlyCollection<Guid> keys);

    /// <summary>
    /// Deletes all pending import review transactions for the current tenant.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when tenant context is not available.</exception>
    /// <remarks>
    /// Use this method to clear all pending imports when the user wants to start over
    /// or abandon the current import review session.
    /// </remarks>
    public async Task DeleteAllAsync();

    /// <summary>
    /// Detects duplicate status for an imported transaction using a two-phase strategy.
    /// </summary>
    /// <param name="importDto">The imported transaction to check.</param>
    /// <param name="existingTransactions">Collection of existing transactions in the main table.</param>
    /// <param name="pendingImports">Collection of pending import review transactions.</param>
    /// <returns>
    /// A tuple containing the duplicate status and the key of the duplicate transaction (if any).
    /// Returns (DuplicateStatus.New, null) if no duplicates are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Two-Phase Duplicate Detection Strategy:</strong>
    /// </para>
    /// <para>
    /// <strong>Phase 1: ExternalId (FITID) Matching</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Check if any existing transaction or pending import has the same ExternalId (case-insensitive)</item>
    /// <item>If found with matching Date, Amount, and Payee → ExactDuplicate</item>
    /// <item>If found with different Date, Amount, or Payee → PotentialDuplicate (bank correction or data issue)</item>
    /// </list>
    /// <para>
    /// <strong>Phase 2: Field-Level Matching (if no ExternalId match)</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Check if any existing transaction or pending import has matching Date, Amount, and Payee (case-insensitive)</item>
    /// <item>If found → PotentialDuplicate (likely duplicate, but no FITID confirmation)</item>
    /// <item>If not found → New</item>
    /// </list>
    /// <para>
    /// <strong>Rationale:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><strong>ExternalId is authoritative</strong> - Bank-provided FITID uniquely identifies a transaction</item>
    /// <item><strong>Field matching is fallback</strong> - Catches duplicates when FITID is missing or unreliable</item>
    /// <item><strong>Potential duplicates need review</strong> - User decides if same FITID with different data is a correction or error</item>
    /// <item><strong>Check both tables</strong> - Prevents importing the same transaction multiple times within a single session</item>
    /// </list>
    /// <para>
    /// <strong>Example Scenarios:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Same FITID, same data → ExactDuplicate (don't import)</item>
    /// <item>Same FITID, different amount → PotentialDuplicate (bank correction? user review needed)</item>
    /// <item>No FITID, but same date/amount/payee → PotentialDuplicate (likely duplicate, needs review)</item>
    /// <item>No matches → New (safe to import)</item>
    /// </list>
    /// </remarks>
    private static (DuplicateStatus Status, Guid? DuplicateOfKey) DetectDuplicate(
        TransactionImportDto importDto,
        IReadOnlyCollection<Transaction> existingTransactions,
        IReadOnlyCollection<ImportReviewTransaction> pendingImports);
}
```

## References

**Design Documents:**
- [`DESIGN-BANK-IMPORT.md`](DESIGN-BANK-IMPORT.md) - Overall feature architecture
- [`DESIGN-BANK-IMPORT-DATABASE.md`](DESIGN-BANK-IMPORT-DATABASE.md) - Entity model and data layer design
- [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - Product requirements

**Related Code:**
- [`OfxParsingService.cs`](src/Application/Import/Services/OfxParsingService.cs) - OFX file parsing (already implemented)
- [`TransactionImportDto.cs`](src/Application/Import/Dto/TransactionImportDto.cs) - Parsed transaction DTO
- [`TenantFeature.cs`](src/Application/Tenancy/Features/TenantFeature.cs) - Reference Feature implementation pattern
- [`ServiceCollectionExtensions.cs`](src/Application/ServiceCollectionExtensions.cs) - Service registration

**Architecture:**
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) - Clean Architecture layers
- [`docs/wip/APPLICATION-FEATURE-RETURN-VALUES.md`](../APPLICATION-FEATURE-RETURN-VALUES.md) - Collection return type guidance
