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
public sealed class DataImportService(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IMemoryCache memoryCache,
    IRegexValidationService regexValidationService)
{
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
