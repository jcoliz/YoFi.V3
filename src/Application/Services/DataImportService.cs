using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Services;

/// <summary>Typed create-only import path. Inputs have no Keys; every invocation adds new records.</summary>
/// <remarks>
/// Shared by sample loading and file imports. Validates every DTO before staging and constructs
/// records using the same <see cref="ImportModelFactory"/> as interactive creation,
/// preserving category sanitization, internal splits, and rule initialization.
/// A single EF-backed SaveChangesAsync transaction persists both entity types and splits.
/// Rule caches are invalidated only after a successful commit. Repeated loading creates
/// fresh Keys with the same ExternalIds; existing workspace records are never updated,
/// deleted, or deduplicated.
/// </remarks>
/// <param name="tenantProvider">Provider of the authorized tenant attached at the persistence boundary.</param>
/// <param name="dataProvider">Unit of work that stages records and atomically saves the complete graph.</param>
/// <param name="memoryCache">Shared rule cache invalidated for the target tenant after a successful save.</param>
/// <param name="regexValidationService">Service used by matching-rule DTO validation.</param>
/// <param name="timeProvider">Provider for UTC date validation and rule audit timestamps.</param>
public sealed class DataImportService(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IMemoryCache memoryCache,
    IRegexValidationService regexValidationService,
    TimeProvider timeProvider)
{
    /// <summary>Validates and creates all supplied transactions and rules in one persistence save.</summary>
    /// <param name="dataset">Key-free, tenant-free inputs with initialized immutable collections.</param>
    /// <param name="cancellationToken">Token checked before staging and passed to the save operation.</param>
    /// <returns>Created counts only after the entire save succeeds.</returns>
    /// <remarks>
    /// Assigns fresh Keys, the current tenant, sanitized categories, supplied sample or default splits, and
    /// initial rule audit/usage values. This shared import path does not enforce sample
    /// generation limits. Persistence errors propagate; the caller must not reuse a failed
    /// unit of work with its still-tracked inserts.
    /// </remarks>
    /// <exception cref="ValidationException">Any transaction or rule fails DTO validation; nothing is staged.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before staging or during persistence.</exception>
    public async Task<SampleDataCounts> CreateAsync(SampleDataSet dataset, CancellationToken cancellationToken = default)
    {
        await CreateRecordsAsync(dataset, cancellationToken);
        return new SampleDataCounts(dataset.Transactions.Length, dataset.Rules.Length);
    }

    /// <summary>Creates a dataset and returns the keys assigned to its transactions.</summary>
    /// <param name="dataset">Key-free, tenant-free inputs with initialized immutable collections.</param>
    /// <param name="cancellationToken">Token checked before staging and passed to the save operation.</param>
    /// <returns>The keys assigned to the committed transactions.</returns>
    public async Task<IReadOnlyCollection<Guid>> CreateAndGetTransactionKeysAsync(
        SampleDataSet dataset,
        CancellationToken cancellationToken = default)
    {
        var transactions = await CreateRecordsAsync(dataset, cancellationToken);
        return transactions.Select(transaction => transaction.Key).ToArray();
    }

    private async Task<IReadOnlyCollection<Transaction>> CreateRecordsAsync(
        SampleDataSet dataset,
        CancellationToken cancellationToken)
    {
        Validate(dataset, regexValidationService, timeProvider);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenantProvider.CurrentTenant.Id;
        var now = timeProvider.GetUtcNow();
        var transactions = dataset.Transactions.Select(t =>
                t.ExternalId != null && dataset.TransactionSplits.TryGetValue(t.ExternalId, out var splits)
                    ? ImportModelFactory.SampleTransactionWithSplits(t, splits, tenantId)
                    : ImportModelFactory.Transaction(t, tenantId))
            .ToArray();
        var records = transactions.Cast<IModel>()
            .Concat(dataset.Rules.Select(r => ImportModelFactory.Rule(r, tenantId, now))).ToArray();

        // The EF IDataProvider stages AddRange and commits this entire graph in one SaveChanges transaction.
        dataProvider.AddRange(records);
        await dataProvider.SaveChangesAsync(cancellationToken);
        memoryCache.Remove(PayeeMatchingRuleFeature.CacheKey(tenantId));
        return transactions;
    }

    /// <summary>Validates and creates payee matching rules without updating or deduplicating existing rules.</summary>
    /// <param name="rules">Key-free payee matching rule inputs.</param>
    /// <param name="cancellationToken">Token checked before staging and passed to persistence.</param>
    /// <returns>Number of rules committed.</returns>
    /// <remarks>Empty input is a no-op. A successful non-empty save invalidates the tenant rule cache.</remarks>
    public async Task<int> CreateRulesAsync(
        IReadOnlyCollection<PayeeMatchingRuleEditDto> rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ValidateRules(rules, regexValidationService);
        cancellationToken.ThrowIfCancellationRequested();
        if (rules.Count == 0)
        {
            return 0;
        }

        var tenantId = tenantProvider.CurrentTenant.Id;
        var now = timeProvider.GetUtcNow();
        var records = rules.Select(rule => (IModel)ImportModelFactory.Rule(rule, tenantId, now)).ToArray();
        dataProvider.AddRange(records);
        await dataProvider.SaveChangesAsync(cancellationToken);
        memoryCache.Remove(PayeeMatchingRuleFeature.CacheKey(tenantId));
        return records.Length;
    }

    /// <summary>Upserts native payee matching rules in the authorized tenant.</summary>
    /// <param name="rules">Validated native rule rows.</param>
    /// <param name="cancellationToken">Cancellation token for queries and persistence.</param>
    /// <returns>The number of created and updated rules.</returns>
    public async Task<(int Created, int Updated)> UpsertNativeRulesAsync(
        IReadOnlyCollection<NativePayeeMatchingRuleImportDto> rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenantProvider.CurrentTenant.Id;
        var keys = rules.Where(rule => rule.Key.HasValue).Select(rule => rule.Key!.Value).ToArray();
        var existing = keys.Length == 0
            ? []
            : await dataProvider.ToListAsync(dataProvider.Get<PayeeMatchingRule>()
                .Where(rule => rule.TenantId == tenantId && keys.Contains(rule.Key)));
        var existingByKey = existing.ToDictionary(rule => rule.Key);
        var now = timeProvider.GetUtcNow();
        var created = 0;
        var updated = 0;

        foreach (var input in rules)
        {
            if (input.Key.HasValue && existingByKey.TryGetValue(input.Key.Value, out var rule))
            {
                rule.PayeePattern = input.PayeePattern;
                rule.PayeeIsRegex = input.PayeeIsRegex;
                rule.Category = CategoryHelper.SanitizeCategory(input.Category);
                rule.ModifiedAt = now;
                updated++;
            }
            else
            {
                dataProvider.Add(new PayeeMatchingRule
                {
                    Key = input.Key ?? Guid.NewGuid(),
                    TenantId = tenantId,
                    PayeePattern = input.PayeePattern,
                    PayeeIsRegex = input.PayeeIsRegex,
                    Category = CategoryHelper.SanitizeCategory(input.Category),
                    CreatedAt = now,
                    ModifiedAt = now,
                    MatchCount = 0,
                    LastUsedAt = null
                });
                created++;
            }
        }

        await dataProvider.SaveChangesAsync(cancellationToken);
        memoryCache.Remove(PayeeMatchingRuleFeature.CacheKey(tenantId));
        return (created, updated);
    }

    /// <summary>Creates original-worksheet transaction graphs after tenant duplicate filtering.</summary>
    /// <param name="transactions">Validated original transaction graphs.</param>
    /// <param name="cancellationToken">Cancellation token for queries and persistence.</param>
    /// <returns>The number of newly created transactions.</returns>
    /// <exception cref="ValidationException">An input graph is invalid or referential integrity is unsatisfied.</exception>
    public async Task<int> CreateOriginalTransactionsAsync(
        IReadOnlyCollection<OriginalTransactionImportDto> transactions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        cancellationToken.ThrowIfCancellationRequested();

        var transactionValidator = new TransactionEditDtoValidator(timeProvider);
        var splitValidator = new SplitEditDtoValidator();
        foreach (var input in transactions)
        {
            var dto = new TransactionEditDto(input.Date, input.Amount, input.Payee, input.Memo, null,
                input.BankReference, input.Splits.Count == 1 ? input.Splits.Single().Category : null);
            var result = transactionValidator.Validate(dto);
            if (!result.IsValid)
                throw new ValidationException(nameof(transactions), result.ToString());
            decimal total = 0;
            foreach (var split in input.Splits)
            {
                var splitResult = splitValidator.Validate(new SplitEditDto(split.Amount, split.Category, split.Memo));
                if (!splitResult.IsValid)
                    throw new ValidationException(nameof(transactions), splitResult.ToString());
                total += split.Amount;
            }
            if (total != input.Amount)
                throw new ValidationException(nameof(transactions), "Split amounts must equal the transaction amount.");
        }

        var tenantId = tenantProvider.CurrentTenant.Id;
        var externalIds = transactions.Select(input => input.BankReference)
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var existing = externalIds.Length == 0
            ? []
            : await dataProvider.ToListAsync(dataProvider.Get<Transaction>()
                .Where(transaction => transaction.TenantId == tenantId && transaction.ExternalId != null
                    && externalIds.Contains(transaction.ExternalId))
                .Select(transaction => transaction.ExternalId));
        var existingIds = existing.ToHashSet(StringComparer.Ordinal);

        var created = transactions.Where(input =>
                input.BankReference is null || !existingIds.Contains(input.BankReference)).ToArray();
        var records = created.Select(input =>
        {
            var transaction = ImportModelFactory.Transaction(new TransactionEditDto(
                input.Date, input.Amount, input.Payee, input.Memo, null, input.BankReference,
                input.Splits.Count == 1 ? input.Splits.Single().Category : null), tenantId);
            transaction.Splits.Clear();
            foreach (var split in input.Splits)
            {
                transaction.Splits.Add(new Split
                {
                    Amount = split.Amount,
                    Category = CategoryHelper.SanitizeCategory(split.Category),
                    Memo = split.Memo,
                    Order = transaction.Splits.Count
                });
            }
            return (IModel)transaction;
        }).ToArray();

        if (records.Length == 0) return 0;
        dataProvider.AddRange(records);
        await dataProvider.SaveChangesAsync(cancellationToken);
        return records.Length;
    }

    /// <summary>Upserts native transaction and split graphs in the authorized tenant.</summary>
    /// <param name="transactions">Validated native transaction rows.</param>
    /// <param name="splits">Validated native split rows.</param>
    /// <param name="cancellationToken">Cancellation token for queries and persistence.</param>
    /// <returns>Created, updated, and skipped native worksheet counts.</returns>
    public async Task<NativeTransactionImportResultDto> UpsertNativeTransactionsAsync(
        IReadOnlyCollection<NativeTransactionImportDto> transactions,
        IReadOnlyCollection<NativeSplitImportDto> splits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(splits);
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = tenantProvider.CurrentTenant.Id;
        var transactionKeys = transactions.Where(input => input.Key.HasValue).Select(input => input.Key!.Value).ToArray();
        var existingTransactions = transactionKeys.Length == 0
            ? []
            : await dataProvider.ToListAsync(dataProvider.Get<Transaction>()
                .Where(transaction => transaction.TenantId == tenantId && transactionKeys.Contains(transaction.Key)));

        var existingIds = existingTransactions.Select(transaction => transaction.Id).ToArray();
        var existingSplits = existingIds.Length == 0
            ? []
            : await dataProvider.ToListAsync(dataProvider.Get<Split>()
                .Where(split => existingIds.Contains(split.TransactionId)));
        foreach (var transaction in existingTransactions)
        {
            transaction.Splits = existingSplits
                .Where(split => split.TransactionId == transaction.Id)
                .ToList();
        }

        var existingByKey = existingTransactions.ToDictionary(transaction => transaction.Key);
        var createdTransactions = 0;
        var updatedTransactions = 0;
        var createdSplits = 0;
        var updatedSplits = 0;

        foreach (var input in transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Transaction transaction;
            if (input.Key.HasValue && existingByKey.TryGetValue(input.Key.Value, out var existing))
            {
                transaction = existing;
                updatedTransactions++;
            }
            else
            {
                transaction = new Transaction { TenantId = tenantId, Key = input.Key ?? Guid.NewGuid() };
                dataProvider.Add(transaction);
                createdTransactions++;
            }

            transaction.Date = input.Date;
            transaction.Amount = input.Amount;
            transaction.Payee = input.Payee;
            transaction.Memo = input.Memo;
            transaction.Source = input.Source;
            transaction.ExternalId = input.ExternalId;

            var isSplitGraph = string.Equals(input.Category?.Trim(), "SPLIT", StringComparison.OrdinalIgnoreCase);
            if (isSplitGraph)
            {
                var transactionSplits = splits
                    .Where(split => split.TransactionKey == (input.Key ?? transaction.Key))
                    .OrderBy(split => split.Order)
                    .ToArray();
                var representedKeys = transactionSplits.Where(split => split.Key.HasValue)
                    .Select(split => split.Key!.Value).ToHashSet();

                foreach (var existingSplit in transaction.Splits
                    .Where(split => !representedKeys.Contains(split.Key)).ToArray())
                {
                    transaction.Splits.Remove(existingSplit);
                    dataProvider.Remove(existingSplit);
                }

                foreach (var splitInput in transactionSplits)
                {
                    Split split;
                    if (splitInput.Key.HasValue)
                    {
                        split = transaction.Splits.FirstOrDefault(item => item.Key == splitInput.Key.Value);
                        if (split is null)
                        {
                            split = new Split { Key = splitInput.Key.Value, TransactionId = transaction.Id };
                            transaction.Splits.Add(split);
                            dataProvider.Add(split);
                            createdSplits++;
                        }
                        else updatedSplits++;
                    }
                    else
                    {
                        split = new Split { TransactionId = transaction.Id };
                        transaction.Splits.Add(split);
                        dataProvider.Add(split);
                        createdSplits++;
                    }

                    split.Amount = splitInput.Amount;
                    split.Category = CategoryHelper.SanitizeCategory(splitInput.Category);
                    split.Memo = splitInput.Memo;
                    split.Order = splitInput.Order;
                }
            }
            else
            {
                var category = CategoryHelper.SanitizeCategory(input.Category);
                foreach (var existingSplit in transaction.Splits.Skip(1).ToArray())
                {
                    transaction.Splits.Remove(existingSplit);
                    dataProvider.Remove(existingSplit);
                }
                var split = transaction.Splits.FirstOrDefault();
                if (split is null)
                {
                    split = new Split { TransactionId = transaction.Id };
                    transaction.Splits.Add(split);
                    dataProvider.Add(split);
                    createdSplits++;
                }
                else updatedSplits++;
                split.Amount = input.Amount;
                split.Category = category;
                split.Memo = null;
                split.Order = 0;
            }
        }

        await dataProvider.SaveChangesAsync(cancellationToken);
        return new NativeTransactionImportResultDto(createdTransactions, updatedTransactions,
            createdSplits, updatedSplits, 0);
    }

    /// <summary>Reconciles a validated native split worksheet against existing transaction graphs.</summary>
    public async Task<NativeTransactionImportResultDto> UpsertNativeSplitsAsync(
        IReadOnlyCollection<NativeSplitImportDto> splits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(splits);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenantProvider.CurrentTenant.Id;
        var transactionKeys = splits.Select(split => split.TransactionKey).Distinct().ToArray();
        var transactions = transactionKeys.Length == 0
            ? []
            : await dataProvider.ToListAsync(dataProvider.Get<Transaction>()
                .Where(transaction => transaction.TenantId == tenantId && transactionKeys.Contains(transaction.Key)));
        var transactionIds = transactions.Select(transaction => transaction.Id).ToArray();
        var existingSplits = transactionIds.Length == 0
            ? []
            : await dataProvider.ToListAsync(dataProvider.Get<Split>()
                .Where(split => transactionIds.Contains(split.TransactionId)));

        var created = 0;
        var updated = 0;
        foreach (var transaction in transactions)
        {
            var current = existingSplits.Where(split => split.TransactionId == transaction.Id).ToList();
            var rows = splits.Where(split => split.TransactionKey == transaction.Key)
                .OrderBy(split => split.Order).ToArray();
            var representedKeys = rows.Where(row => row.Key.HasValue).Select(row => row.Key!.Value).ToHashSet();
            foreach (var omitted in current.Where(split => !representedKeys.Contains(split.Key)).ToArray())
            {
                transaction.Splits.Remove(omitted);
                dataProvider.Remove(omitted);
            }
            foreach (var row in rows)
            {
                var split = row.Key.HasValue
                    ? current.FirstOrDefault(item => item.Key == row.Key.Value)
                    : null;
                if (split is null)
                {
                    split = new Split { Key = row.Key ?? Guid.NewGuid(), TransactionId = transaction.Id };
                    dataProvider.Add(split);
                    created++;
                }
                else updated++;
                split.Amount = row.Amount;
                split.Category = CategoryHelper.SanitizeCategory(row.Category);
                split.Memo = row.Memo;
                split.Order = row.Order;
            }
        }
        await dataProvider.SaveChangesAsync(cancellationToken);
        return new NativeTransactionImportResultDto(0, 0, created, updated, 0);
    }

    /// <summary>Generates the original YoFi duplicate signature for append-only transaction imports.</summary>
    /// <param name="payee">Transaction payee.</param>
    /// <param name="amount">Transaction amount.</param>
    /// <param name="date">Transaction date.</param>
    /// <returns>Uppercase MD5 hexadecimal legacy signature.</returns>
    public static string GenerateLegacyExternalId(string payee, decimal amount, DateOnly date)
    {
        var currency = amount.ToString("C2", CultureInfo.CreateSpecificCulture("en-US"));
        var signature = $"/{(string.IsNullOrWhiteSpace(payee) ? "Null" : payee.Trim())}/{currency}/{date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)}";
        var bytes = Encoding.UTF32.GetBytes(signature);
        return Convert.ToHexString(MD5.HashData(bytes));
    }

    /// <summary>Validates every transaction and rule without staging or persisting records.</summary>
    /// <param name="dataset">Dataset with initialized immutable collections to validate.</param>
    /// <param name="regexValidationService">Service used by the rule DTO validator.</param>
    /// <param name="timeProvider">Provider for the current UTC date used by transaction validation.</param>
    /// <exception cref="ValidationException">A record fails validation; the message identifies its entity type.</exception>
    internal static void Validate(SampleDataSet dataset, IRegexValidationService regexValidationService, TimeProvider timeProvider)
    {
        var transactionValidator = new TransactionEditDtoValidator(timeProvider);
        foreach (var transaction in dataset.Transactions)
        {
            var result = transactionValidator.Validate(transaction);
            if (!result.IsValid)
            {
                throw new ValidationException(nameof(dataset.Transactions), result.ToString());
            }
        }
        var transactionsByExternalId = dataset.TransactionSplits.Count == 0 ? null
            : dataset.Transactions.ToLookup(t => t.ExternalId, StringComparer.Ordinal);
        foreach (var (externalId, splits) in dataset.TransactionSplits)
        {
            var matches = transactionsByExternalId![externalId].ToArray();
            if (matches.Length != 1 || splits.IsDefaultOrEmpty)
            {
                throw new ValidationException(nameof(dataset.TransactionSplits),
                    "Sample allocations require one matching transaction and a nonempty split collection.");
            }
            foreach (var split in splits)
            {
                if (split == null || split.Category == null || split.Category.Length > 200
                    || split.Memo is { Length: > 500 }
                    || (!string.IsNullOrWhiteSpace(split.Category)
                        && CategoryHelper.SanitizeCategory(split.Category).Length == 0)
                    || Math.Abs(split.Amount) > decimal.MaxValue / SampleDataGenerator.MaxTransactions)
                {
                    throw new ValidationException(nameof(dataset.TransactionSplits),
                        "Sample split categories and amounts must satisfy transaction constraints.");
                }
            }
            decimal total;
            try
            {
                total = splits.Sum(s => s.Amount);
            }
            catch (OverflowException)
            {
                throw new ValidationException(nameof(dataset.TransactionSplits), "Sample split amounts exceed decimal bounds.");
            }
            if (total != matches[0].Amount)
            {
                throw new ValidationException(nameof(dataset.TransactionSplits), "Sample split amounts must equal the transaction amount.");
            }
        }
        ValidateRules(dataset.Rules, regexValidationService);
    }

    private static void ValidateRules(
        IEnumerable<PayeeMatchingRuleEditDto> rules,
        IRegexValidationService regexValidationService)
    {
        var ruleValidator = new PayeeMatchingRuleEditDtoValidator(regexValidationService);
        foreach (var rule in rules)
        {
            var result = ruleValidator.Validate(rule);
            if (!result.IsValid)
            {
                throw new ValidationException(nameof(SampleDataSet.Rules), result.ToString());
            }
        }
    }
}
