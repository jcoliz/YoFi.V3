using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>
/// Provides business logic for the bank import review workflow.
/// </summary>
/// <param name="tenantProvider">Provider for accessing tenant context information.</param>
/// <param name="dataProvider">Repository for data operations on import review transactions.</param>
/// <param name="transactionsFeature">Feature for managing transactions (used when accepting imports).</param>
/// <remarks>
/// <para>
/// Import workflow orchestration:
/// <list type="number">
/// <item><description>Parse OFX file to extract transaction data</description></item>
/// <item><description>Detect duplicates against existing transactions and pending imports</description></item>
/// <item><description>Store transactions in ImportReviewTransaction staging table with duplicate status</description></item>
/// <item><description>Provide operations to retrieve pending review transactions and complete the review workflow</description></item>
/// </list>
/// </para>
/// <para>
/// Tenant Isolation: All operations are scoped to the current authenticated user's tenant via ITenantProvider.
/// The TenantId is automatically applied to all queries and inserts.
/// </para>
/// <para>
/// Duplicate Detection: Uses ExternalId (FITID) matching strategy to classify transactions as New, ExactDuplicate,
/// or PotentialDuplicate. See <see cref="DetectDuplicate"/> method for details.
/// </para>
/// <para>
/// Transaction Creation: When accepting transactions, this feature delegates to TransactionsFeature.AddTransactionAsync()
/// to ensure consistent transaction creation including default splits. This follows clean architecture principles
/// by reusing existing business logic rather than duplicating transaction creation code.
/// </para>
/// </remarks>
public class ImportReviewFeature(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    TransactionsFeature transactionsFeature)
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 1000;

    private readonly Tenant _currentTenant = tenantProvider.CurrentTenant;

    /// <summary>
    /// Imports an OFX file, parses transactions, detects duplicates, and stores them for review.
    /// </summary>
    /// <param name="fileStream">The stream containing the OFX/QFX file data.</param>
    /// <param name="fileName">The name of the uploaded file.</param>
    /// <returns>An <see cref="ImportReviewUploadDto"/> containing import statistics and any parsing errors.</returns>
    /// <remarks>
    /// All operations are scoped to the current tenant via ITenantProvider.TenantId.
    /// If parsing errors occur, they are included in the result but do not prevent
    /// import of successfully parsed transactions.
    /// </remarks>
    public async Task<ImportReviewUploadDto> ImportFileAsync(Stream fileStream, string fileName)
    {
        // Parse the OFX file using OfxParsingHelper
        var parsingResult = await OfxParsingHelper.ParseAsync(fileStream, fileName);

        if (parsingResult.Transactions.Count == 0)
        {
            return new ImportReviewUploadDto(
                ImportedCount: 0,
                NewCount: 0,
                ExactDuplicateCount: 0,
                PotentialDuplicateCount: 0,
                Errors: parsingResult.Errors
            );
        }

        // Extract all ExternalIds for batch duplicate detection
        var externalIds = parsingResult.Transactions
            .Select(t => t.ExternalId)
            .Distinct()
            .ToArray();

        // Execute batch queries to check for duplicate ExternalIds in both tables
        var existingTransactionsByExternalId = await GetExistingTransactionsByExternalIdAsync(externalIds);
        var pendingImportsByExternalId = await GetPendingImportsByExternalIdAsync(externalIds);

        // Create ImportReviewTransaction records with duplicate detection
        var importReviewTransactions = new List<ImportReviewTransaction>();
        int newCount = 0;
        int exactDuplicateCount = 0;
        int potentialDuplicateCount = 0;

        foreach (var importDto in parsingResult.Transactions)
        {
            // Detect duplicate status
            var (status, duplicateOfKey) = DetectDuplicate(
                importDto,
                existingTransactionsByExternalId,
                pendingImportsByExternalId
            );

            // Update counters
            switch (status)
            {
                case DuplicateStatus.New:
                    newCount++;
                    break;
                case DuplicateStatus.ExactDuplicate:
                    exactDuplicateCount++;
                    break;
                case DuplicateStatus.PotentialDuplicate:
                    potentialDuplicateCount++;
                    break;
            }

            // Create ImportReviewTransaction entity with selection state based on duplicate status
            var importReviewTransaction = new ImportReviewTransaction
            {
                TenantId = _currentTenant.Id,
                Date = importDto.Date,
                Payee = importDto.Payee,
                Amount = importDto.Amount,
                Source = importDto.Source,
                ExternalId = importDto.ExternalId,
                Memo = importDto.Memo,
                DuplicateStatus = status,
                DuplicateOfKey = duplicateOfKey,
                IsSelected = (status == DuplicateStatus.New) // Only select new transactions by default
            };

            importReviewTransactions.Add(importReviewTransaction);
        }

        // Store all import review transactions in the database
        dataProvider.AddRange(importReviewTransactions);
        await dataProvider.SaveChangesAsync();

        return new ImportReviewUploadDto(
            ImportedCount: parsingResult.Transactions.Count,
            NewCount: newCount,
            ExactDuplicateCount: exactDuplicateCount,
            PotentialDuplicateCount: potentialDuplicateCount,
            Errors: parsingResult.Errors
        );
    }

    /// <summary>
    /// Retrieves pending import review transactions for the current tenant with pagination support.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve (1-based). If null or less than 1, defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. If null or less than 1, defaults to 50. Maximum is 1000.</param>
    /// <returns>A <see cref="PaginatedResultDto{T}"/> containing transactions and pagination metadata.</returns>
    public async Task<PaginatedResultDto<ImportReviewTransactionDto>> GetPendingReviewAsync(
        int? pageNumber = null,
        int? pageSize = null)
    {
        // Validate and normalize pagination parameters
        var normalizedPageNumber = pageNumber is null or < 1 ? DefaultPageNumber : pageNumber.Value;
        var normalizedPageSize = pageSize is null or < 1 ? DefaultPageSize : pageSize.Value;

        if (normalizedPageSize > MaxPageSize)
        {
            normalizedPageSize = MaxPageSize;
        }

        // Query total count for pagination metadata
        var totalCount = await dataProvider.CountAsync(GetTenantScopedQuery());

        // Calculate pagination metadata
        var metadata = PaginationHelper.Calculate(normalizedPageNumber, normalizedPageSize, totalCount);

        // Query paginated data ordered by date descending
        var paginatedQuery = GetBaseImportReviewQuery()
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(t => new ImportReviewTransactionDto(
                t.Key,
                t.Date,
                t.Payee,
                string.Empty, // Category placeholder for future Payee Matching rules feature
                t.Amount,
                t.DuplicateStatus,
                t.DuplicateOfKey,
                t.IsSelected
            ));

        var items = await dataProvider.ToListNoTrackingAsync(paginatedQuery);

        return new PaginatedResultDto<ImportReviewTransactionDto>(
            Items: items,
            Metadata: metadata
        );
    }

    /// <summary>
    /// Completes the import review by accepting selected transactions (IsSelected = true) and deleting all pending review transactions.
    /// </summary>
    /// <returns>A <see cref="ImportReviewCompleteDto"/> with counts of accepted and rejected transactions.</returns>
    /// <remarks>
    /// Selection state is managed server-side and stored in the database IsSelected column.
    /// This method queries ALL transactions where IsSelected = true (across all pages), accepts them,
    /// and then deletes ALL pending review transactions to complete the workflow.
    /// </remarks>
    public async Task<ImportReviewCompleteDto> CompleteReviewAsync()
    {
        // Get total count before processing
        var totalCount = await dataProvider.CountAsync(GetTenantScopedQuery());

        // Retrieve selected import review transactions (IsSelected = true) from database
        var selectedQuery = GetBaseImportReviewQuery()
            .Where(t => t.IsSelected);
        var selectedTransactions = await dataProvider.ToListAsync(selectedQuery);

        // Convert to TransactionEditDto collection
        var transactionEdits = selectedTransactions
            .Select(importTransaction => new TransactionEditDto(
                Date: importTransaction.Date,
                Amount: importTransaction.Amount,
                Payee: importTransaction.Payee,
                Memo: importTransaction.Memo,
                Source: importTransaction.Source,
                ExternalId: importTransaction.ExternalId,
                Category: string.Empty // Category is empty for now (future: Payee Matching rules)
            ))
            .ToList();

        // Accept selected transactions via TransactionsFeature in a single batch
        await transactionsFeature.AddTransactionsAsync(transactionEdits);

        // Delete ALL import review transactions for the current tenant
        await DeleteAllAsync();

        var acceptedCount = selectedTransactions.Count;
        var rejectedCount = totalCount - acceptedCount;

        return new ImportReviewCompleteDto(
            AcceptedCount: acceptedCount,
            RejectedCount: rejectedCount
        );
    }

    /// <summary>
    /// Deletes all pending import review transactions for the current tenant without accepting any.
    /// </summary>
    /// <returns>The number of transactions deleted.</returns>
    public async Task<int> DeleteAllAsync()
    {
        // Get count before deletion
        var count = await dataProvider.CountAsync(GetTenantScopedQuery());

        // Bulk delete without loading entities into memory
        await dataProvider.ExecuteDeleteAsync(GetTenantScopedQuery());

        return count;
    }

    /// <summary>
    /// Detects duplicate status for a single imported transaction by checking it against existing data.
    /// </summary>
    /// <param name="importDto">The imported transaction to check.</param>
    /// <param name="existingTransactionsByExternalId">Dictionary of most recent existing transactions by ExternalId (from batch query).</param>
    /// <param name="pendingImportsByExternalId">Dictionary of most recent pending imports by ExternalId (from batch query).</param>
    /// <returns>
    /// A tuple containing the duplicate status and the key of the duplicate transaction (if any).
    /// Returns (DuplicateStatus.New, null) if no duplicates are found.
    /// </returns>
    internal static (DuplicateStatus Status, Guid? DuplicateOfKey) DetectDuplicate(
        TransactionImportDto importDto,
        IReadOnlyDictionary<string, Transaction> existingTransactionsByExternalId,
        IReadOnlyDictionary<string, ImportReviewTransaction> pendingImportsByExternalId)
    {
        // ExternalId is required - this should never be null/empty due to upstream filtering
        if (string.IsNullOrEmpty(importDto.ExternalId))
        {
            throw new ArgumentException(
                "ExternalId cannot be null or empty. This indicates a bug in upstream OFX parsing or filtering logic.",
                nameof(importDto));
        }

        // Check existing transactions first
        if (existingTransactionsByExternalId.TryGetValue(importDto.ExternalId, out var existingTransaction))
        {
            var status = CompareTransactionFields(importDto, existingTransaction.Date, existingTransaction.Amount, existingTransaction.Payee);
            return (status, existingTransaction.Key);
        }

        // Check pending imports second
        if (pendingImportsByExternalId.TryGetValue(importDto.ExternalId, out var pendingImport))
        {
            var status = CompareTransactionFields(importDto, pendingImport.Date, pendingImport.Amount, pendingImport.Payee);
            return (status, pendingImport.Key);
        }

        // No duplicates found
        return (DuplicateStatus.New, null);
    }

    /// <summary>
    /// Compares transaction fields to determine if a match is exact or potential duplicate.
    /// </summary>
    /// <param name="importDto">The imported transaction to compare.</param>
    /// <param name="existingDate">The date of the existing transaction.</param>
    /// <param name="existingAmount">The amount of the existing transaction.</param>
    /// <param name="existingPayee">The payee of the existing transaction.</param>
    /// <returns>
    /// <see cref="DuplicateStatus.ExactDuplicate"/> if all fields match,
    /// <see cref="DuplicateStatus.PotentialDuplicate"/> if fields don't match.
    /// </returns>
    private static DuplicateStatus CompareTransactionFields(
        TransactionImportDto importDto,
        DateOnly existingDate,
        decimal existingAmount,
        string existingPayee)
    {
        var fieldsMatch = existingDate == importDto.Date
            && existingAmount == importDto.Amount
            && existingPayee == importDto.Payee;

        return fieldsMatch ? DuplicateStatus.ExactDuplicate : DuplicateStatus.PotentialDuplicate;
    }

    /// <summary>
    /// Creates a tenant-scoped query for import review transactions (no ordering).
    /// </summary>
    /// <returns>A queryable of import review transactions for the current tenant.</returns>
    private IQueryable<ImportReviewTransaction> GetTenantScopedQuery()
    {
        return dataProvider.Get<ImportReviewTransaction>()
            .Where(t => t.TenantId == _currentTenant.Id);
    }

    /// <summary>
    /// Creates a base query for import review transactions filtered by the current tenant and ordered by date.
    /// </summary>
    /// <returns>A queryable of import review transactions for the current tenant, ordered by date descending, then by payee, then by id.</returns>
    private IQueryable<ImportReviewTransaction> GetBaseImportReviewQuery()
    {
        return GetTenantScopedQuery()
            .OrderByDescending(t => t.Date)
            .ThenBy(t => t.Payee)
            .ThenByDescending(t => t.Id);
    }

    /// <summary>
    /// Retrieves most recent existing transactions by ExternalId using batch query.
    /// </summary>
    /// <remarks>
    /// When multiple transactions have the same ExternalId and Date, uses Id as tie-breaker to ensure
    /// exactly one transaction per ExternalId is returned (prevents duplicate key exception in ToDictionary).
    /// </remarks>
    private async Task<IReadOnlyDictionary<string, Transaction>> GetExistingTransactionsByExternalIdAsync(
        string[] externalIds)
    {
        // Use subquery approach to get most recent transaction per ExternalId
        // Include Id comparison as tie-breaker for transactions with same Date
        var query = dataProvider.Get<Transaction>()
            .Where(t => t.TenantId == _currentTenant.Id && externalIds.Contains(t.ExternalId))
            .Where(t => !dataProvider.Get<Transaction>()
                .Any(t2 => t2.TenantId == _currentTenant.Id
                        && t2.ExternalId == t.ExternalId
                        && (t2.Date > t.Date || (t2.Date == t.Date && t2.Id > t.Id))));

        var transactions = await dataProvider.ToListNoTrackingAsync(query);
        return transactions.ToDictionary(t => t.ExternalId!, t => t);
    }

    /// <summary>
    /// Retrieves most recent pending imports by ExternalId using batch query.
    /// </summary>
    /// <remarks>
    /// When multiple imports have the same ExternalId and Date, uses Id as tie-breaker to ensure
    /// exactly one import per ExternalId is returned (prevents duplicate key exception in ToDictionary).
    /// </remarks>
    private async Task<IReadOnlyDictionary<string, ImportReviewTransaction>> GetPendingImportsByExternalIdAsync(
        string[] externalIds)
    {
        // Use subquery approach to get most recent import per ExternalId
        // Include Id comparison as tie-breaker for imports with same Date
        var query = dataProvider.Get<ImportReviewTransaction>()
            .Where(t => t.TenantId == _currentTenant.Id && externalIds.Contains(t.ExternalId))
            .Where(t => !dataProvider.Get<ImportReviewTransaction>()
                .Any(t2 => t2.TenantId == _currentTenant.Id
                        && t2.ExternalId == t.ExternalId
                        && (t2.Date > t.Date || (t2.Date == t.Date && t2.Id > t.Id))));

        var imports = await dataProvider.ToListNoTrackingAsync(query);
        return imports.ToDictionary(t => t.ExternalId!, t => t);
    }

    /// <summary>
    /// Sets the selection state for the specified transaction(s).
    /// </summary>
    /// <param name="keys">Collection of transaction keys to update.</param>
    /// <param name="isSelected">The desired selection state.</param>
    public async Task SetSelectionAsync(IReadOnlyCollection<Guid> keys, bool isSelected)
    {
        await dataProvider.ExecuteUpdatePropertyAsync(
            GetTenantScopedQuery().Where(t => keys.Contains(t.Key)),
            t => t.IsSelected,
            isSelected
        );
    }

    /// <summary>
    /// Selects all pending import review transactions for the current tenant.
    /// </summary>
    public async Task SelectAllAsync()
    {
        await dataProvider.ExecuteUpdatePropertyAsync(
            GetTenantScopedQuery(),
            t => t.IsSelected,
            true
        );
    }

    /// <summary>
    /// Deselects all pending import review transactions for the current tenant.
    /// </summary>
    public async Task DeselectAllAsync()
    {
        await dataProvider.ExecuteUpdatePropertyAsync(
            GetTenantScopedQuery(),
            t => t.IsSelected,
            false
        );
    }

    /// <summary>
    /// Gets summary statistics for pending import review transactions.
    /// </summary>
    /// <returns>An <see cref="ImportReviewSummaryDto"/> containing counts of total, selected, and duplicate transactions.</returns>
    public async Task<ImportReviewSummaryDto> GetSummaryAsync()
    {
        var query = GetTenantScopedQuery();

        var totalCount = await dataProvider.CountAsync(query);
        var selectedCount = await dataProvider.CountAsync(query.Where(t => t.IsSelected));
        var newCount = await dataProvider.CountAsync(query.Where(t => t.DuplicateStatus == DuplicateStatus.New));
        var exactDuplicateCount = await dataProvider.CountAsync(query.Where(t => t.DuplicateStatus == DuplicateStatus.ExactDuplicate));
        var potentialDuplicateCount = await dataProvider.CountAsync(query.Where(t => t.DuplicateStatus == DuplicateStatus.PotentialDuplicate));

        return new ImportReviewSummaryDto(
            TotalCount: totalCount,
            SelectedCount: selectedCount,
            NewCount: newCount,
            ExactDuplicateCount: exactDuplicateCount,
            PotentialDuplicateCount: potentialDuplicateCount
        );
    }

    /// <summary>
    /// Seeds test import review transactions for testing purposes.
    /// </summary>
    /// <param name="count">Number of import review transactions to create.</param>
    /// <param name="selectedCount">Number of transactions to mark as selected. Defaults to all transactions.</param>
    /// <returns>The number of transactions created.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when selectedCount is greater than count or negative.</exception>
    /// <remarks>
    /// Generates deterministic transaction data based on index and inserts them into ImportReviewTransaction table.
    /// All transactions are marked as New status. The first selectedCount transactions are marked as selected.
    /// This method is intended for use only in test scenarios via TestControlController.
    /// </remarks>
    public async Task<int> SeedTestDataAsync(int count, int? selectedCount = null)
    {
        var normalizedSelectedCount = selectedCount ?? count;

        if (normalizedSelectedCount < 0 || normalizedSelectedCount > count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedCount),
                $"selectedCount must be between 0 and {count}, but was {normalizedSelectedCount}");
        }

        var baseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var importReviewTransactions = new List<ImportReviewTransaction>();

        for (int i = 1; i <= count; i++)
        {
            importReviewTransactions.Add(new ImportReviewTransaction
            {
                TenantId = _currentTenant.Id,
                Date = baseDate.AddDays(i % 30), // Deterministic based on index
                Payee = $"Test Import {i}",
                Amount = 10.00m + (i * 5.00m), // Deterministic based on index
                Source = "OFX",
                ExternalId = $"FITID-TEST{i:D12}", // Deterministic based on index
                Memo = $"Test import transaction {i}",
                DuplicateStatus = DuplicateStatus.New,
                DuplicateOfKey = null,
                IsSelected = i <= normalizedSelectedCount // First N transactions are selected
            });
        }

        dataProvider.AddRange(importReviewTransactions);
        await dataProvider.SaveChangesAsync();

        return count;
    }
}
