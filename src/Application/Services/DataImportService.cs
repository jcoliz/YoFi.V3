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
