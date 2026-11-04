using System.Collections.Immutable;
using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Services;
using YoFi.V3.Application.Validation;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>Coordinates sample use cases for an already authorized tenant.</summary>
/// <remarks>
/// <para>
/// Exposes the scenario catalog, exact preview counts including the current tenant's
/// transaction count, whole-set loading, and OFX downloads. The caller must authorize
/// the tenant; this feature performs neither HTTP authorization nor workspace reset.
/// Generation requests require an explicit scenario ID, current revision, and inclusive
/// date range. No server date defaults are supplied, and existing transaction date
/// validation applies.
/// </para>
/// <para>
/// The initial Personal Finance asset uses the design's illustrative 61,678 annual net
/// salary as one income pattern, without payroll deductions, amortization, or budgets.
/// Curated generation changes require a scenario revision bump so stale previews must
/// refresh before proceeding.
/// </para>
/// <para>
/// SampleData unit tests cover orchestration, generation, and OFX round-tripping.
/// SQLite integration tests verify atomic persistence, preservation, repeated loading,
/// and matching-cache behavior. Maximum-size latency and memory benchmarking remains
/// a release check.
/// </para>
/// </remarks>
/// <param name="loader">Validated immutable bundled scenario catalog.</param>
/// <param name="generator">Deterministic, tenant-free dataset generator.</param>
/// <param name="importer">Shared create-only persistence operation.</param>
/// <param name="tenantProvider">Provider of the already authorized current tenant.</param>
/// <param name="dataProvider">Data access used to count existing tenant transactions.</param>
/// <param name="regexValidationService">Rule validation dependency used when validating generated DTOs.</param>
public sealed class SampleDataFeature(
    SampleScenarioLoader loader,
    SampleDataGenerator generator,
    DataImportService importer,
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IRegexValidationService regexValidationService)
{
    /// <summary>Returns display metadata, current revisions, and supported entity types for bundled scenarios.</summary>
    /// <returns>An immutable catalog without generation defaults or workspace-specific data.</returns>
    public ImmutableArray<SampleScenarioDto> GetScenarios()
        => loader.Scenarios.Select(s => new SampleScenarioDto(s.Id, s.Revision, s.Name, s.Description,
            ["Transactions", "PayeeMatchingRules"])).ToImmutableArray();

    /// <summary>Generates and validates a dataset to preview exact counts without persisting it.</summary>
    /// <param name="request">Explicit scenario ID, current revision, and inclusive date range.</param>
    /// <returns>Resolved inputs, dataset counts, and the current tenant's unfiltered transaction count.</returns>
    /// <exception cref="ValidationException">The scenario, revision, dates, size, or generated DTOs are invalid.</exception>
    public async Task<SampleDataPreviewDto> PreviewAsync(SampleDataRequest request)
    {
        var dataset = Generate(request);
        var tenantId = tenantProvider.CurrentTenant.Id;
        var existing = await dataProvider.CountAsync(dataProvider.Get<Transaction>().Where(t => t.TenantId == tenantId));
        return new SampleDataPreviewDto(request, new(dataset.Transactions.Length, dataset.Rules.Length), existing);
    }

    /// <summary>Regenerates, validates, and commits the complete dataset for the current tenant.</summary>
    /// <param name="request">Explicit scenario ID, current revision, and inclusive date range.</param>
    /// <param name="cancellationToken">Token checked before staging and passed to the persistence save.</param>
    /// <returns>Resolved inputs and committed created counts; updated counts are always zero.</returns>
    /// <remarks>
    /// Existing data and pending import reviews are preserved. Repeated calls intentionally
    /// add complete copies with fresh Keys, even when synthetic ExternalIds match.
    /// Persistence failures propagate without returning a successful result.
    /// </remarks>
    /// <exception cref="ValidationException">The request or generated dataset is invalid.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is observed before staging or during persistence.</exception>
    public async Task<SampleDataLoadResultDto> LoadAsync(SampleDataRequest request, CancellationToken cancellationToken = default)
    {
        var dataset = Generate(request);
        var created = await importer.CreateAsync(dataset, cancellationToken);
        return new SampleDataLoadResultDto(request, created, new(0, 0));
    }

    /// <summary>Generates an OFX attachment using the same occurrences and identities as direct loading.</summary>
    /// <param name="request">Explicit scenario ID, current revision, and inclusive statement date range.</param>
    /// <returns>A named OFX attachment whose stream starts at position zero; the consumer must dispose it.</returns>
    /// <remarks>Does not persist data or encode categories; the existing bank importer applies matching rules.</remarks>
    /// <exception cref="ValidationException">The request or generated dataset is invalid.</exception>
    public SampleOfxDownload DownloadOfx(SampleDataRequest request)
    {
        var dataset = Generate(request);
        return new SampleOfxDownload(SampleOfxWriter.Write(dataset.Transactions, request.StartDate!.Value, request.EndDate!.Value),
            $"{request.ScenarioId}-{request.StartDate:yyyy-MM-dd}-{request.EndDate:yyyy-MM-dd}.ofx");
    }

    private SampleDataSet Generate(SampleDataRequest request)
    {
        var scenario = loader.Scenarios.SingleOrDefault(s => s.Id == request.ScenarioId)
            ?? throw new ValidationException(nameof(request.ScenarioId), $"Sample scenario '{request.ScenarioId}' is unavailable.");
        if (request.Revision != scenario.Revision)
        {
            throw new ValidationException(nameof(request.Revision), "The sample scenario revision has changed. Refresh the preview before continuing.");
        }
        if (request.StartDate is not { } start || request.EndDate is not { } end)
        {
            throw new ValidationException(nameof(request.StartDate), "Explicit start and end dates are required.");
        }
        SampleDataGenerator.ValidateRange(start, end);
        var validator = new TransactionEditDtoValidator();
        foreach (var date in new[] { start, end })
        {
            var validation = validator.Validate(new TransactionEditDto(date, 1, "Sample", null, null, null, null));
            if (!validation.IsValid)
            {
                throw new ValidationException(nameof(request.StartDate), validation.ToString());
            }
        }

        var dataset = generator.Generate(scenario, start, end);
        DataImportService.Validate(dataset, regexValidationService);
        return dataset;
    }
}
