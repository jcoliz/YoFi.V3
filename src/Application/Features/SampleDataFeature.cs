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
public sealed class SampleDataFeature(
    SampleScenarioLoader loader,
    SampleDataGenerator generator,
    DataImportService importer,
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IRegexValidationService regexValidationService)
{
    public ImmutableArray<SampleScenarioDto> GetScenarios()
        => loader.Scenarios.Select(s => new SampleScenarioDto(s.Id, s.Revision, s.Name, s.Description,
            ["Transactions", "PayeeMatchingRules"])).ToImmutableArray();

    public async Task<SampleDataPreviewDto> PreviewAsync(SampleDataRequest request)
    {
        var dataset = Generate(request);
        var tenantId = tenantProvider.CurrentTenant.Id;
        var existing = await dataProvider.CountAsync(dataProvider.Get<Transaction>().Where(t => t.TenantId == tenantId));
        return new SampleDataPreviewDto(request, new(dataset.Transactions.Length, dataset.Rules.Length), existing);
    }

    public async Task<SampleDataLoadResultDto> LoadAsync(SampleDataRequest request, CancellationToken cancellationToken = default)
    {
        var dataset = Generate(request);
        var created = await importer.CreateAsync(dataset, cancellationToken);
        return new SampleDataLoadResultDto(request, created, new(0, 0));
    }

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
