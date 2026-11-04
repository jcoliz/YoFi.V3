using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Services;

/// <summary>Typed create-only import path. Inputs have no Keys; every invocation adds new records.</summary>
/// <remarks>
/// Reusable by a future file importer. Validates every DTO before staging and constructs
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
public sealed class DataImportService(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IMemoryCache memoryCache,
    IRegexValidationService regexValidationService)
{
    /// <summary>Validates and creates all supplied transactions and rules in one persistence save.</summary>
    /// <param name="dataset">Key-free, tenant-free inputs with initialized immutable collections.</param>
    /// <param name="cancellationToken">Token checked before staging and passed to the save operation.</param>
    /// <returns>Created counts only after the entire save succeeds.</returns>
    /// <remarks>
    /// Assigns fresh Keys, the current tenant, sanitized categories, default splits, and
    /// initial rule audit/usage values. This shared import path does not enforce sample
    /// generation limits. Persistence errors propagate; the caller must not reuse a failed
    /// unit of work with its still-tracked inserts.
    /// </remarks>
    /// <exception cref="ValidationException">Any transaction or rule fails DTO validation; nothing is staged.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before staging or during persistence.</exception>
    public async Task<SampleDataCounts> CreateAsync(SampleDataSet dataset, CancellationToken cancellationToken = default)
    {
        Validate(dataset, regexValidationService);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenantProvider.CurrentTenant.Id;
        var now = DateTimeOffset.UtcNow;
        var records = dataset.Transactions.Select(t => (IModel)ImportModelFactory.Transaction(t, tenantId))
            .Concat(dataset.Rules.Select(r => ImportModelFactory.Rule(r, tenantId, now))).ToArray();

        // The EF IDataProvider stages AddRange and commits this entire graph in one SaveChanges transaction.
        dataProvider.AddRange(records);
        await dataProvider.SaveChangesAsync(cancellationToken);
        memoryCache.Remove(PayeeMatchingRuleFeature.CacheKey(tenantId));
        return new SampleDataCounts(dataset.Transactions.Length, dataset.Rules.Length);
    }

    /// <summary>Validates every transaction and rule without staging or persisting records.</summary>
    /// <param name="dataset">Dataset with initialized immutable collections to validate.</param>
    /// <param name="regexValidationService">Service used by the rule DTO validator.</param>
    /// <exception cref="ValidationException">A record fails validation; the message identifies its entity type.</exception>
    internal static void Validate(SampleDataSet dataset, IRegexValidationService regexValidationService)
    {
        var transactionValidator = new TransactionEditDtoValidator();
        var ruleValidator = new PayeeMatchingRuleEditDtoValidator(regexValidationService);
        foreach (var transaction in dataset.Transactions)
        {
            var result = transactionValidator.Validate(transaction);
            if (!result.IsValid)
            {
                throw new ValidationException(nameof(dataset.Transactions), result.ToString());
            }
        }
        foreach (var rule in dataset.Rules)
        {
            var result = ruleValidator.Validate(rule);
            if (!result.IsValid)
            {
                throw new ValidationException(nameof(dataset.Rules), result.ToString());
            }
        }
    }
}
