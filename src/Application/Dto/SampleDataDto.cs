using System.Collections.Immutable;

namespace YoFi.V3.Application.Dto;

/// <summary>Explicit generation inputs shared by preview, load, and OFX download operations.</summary>
/// <param name="ScenarioId">Stable ID from the bundled scenario catalog, not a file path.</param>
/// <param name="Revision">Catalog revision accepted by the caller; stale revisions are rejected.</param>
/// <param name="StartDate">Inclusive calendar start date. Null is rejected rather than defaulted.</param>
/// <param name="EndDate">Inclusive calendar end date. Null is rejected rather than defaulted.</param>
/// <remarks>Dates must satisfy transaction validation and the generator's one-calendar-year limit.</remarks>
public record SampleDataRequest(string ScenarioId, int Revision, DateOnly? StartDate, DateOnly? EndDate);

/// <summary>Display metadata and supported entity types for a bundled sample scenario.</summary>
/// <param name="Id">Stable scenario identifier used in generation requests.</param>
/// <param name="Revision">Current revision required by generation requests.</param>
/// <param name="Name">User-facing scenario name.</param>
/// <param name="Description">User-facing description of the scenario's contents.</param>
/// <param name="EntityTypes">Entity type names loaded together; internal splits are not separate types.</param>
public record SampleScenarioDto(string Id, int Revision, string Name, string Description, ImmutableArray<string> EntityTypes);

/// <summary>Per-type record counts, excluding internal transaction splits.</summary>
/// <param name="Transactions">Number of transaction records.</param>
/// <param name="PayeeMatchingRules">Number of payee matching rule records.</param>
public record SampleDataCounts(int Transactions, int PayeeMatchingRules)
{
    /// <summary>Gets the combined number of transactions and payee matching rules.</summary>
    public int Total => Transactions + PayeeMatchingRules;
}

/// <summary>Exact generation counts and the current tenant's existing transaction count, without writes.</summary>
/// <param name="Inputs">Validated scenario, revision, and date range used to generate the preview.</param>
/// <param name="Counts">Exact counts for the complete dataset that loading would create.</param>
/// <param name="ExistingTransactionCount">Unfiltered current-tenant transaction count when previewed, not a deletion confirmation.</param>
public record SampleDataPreviewDto(SampleDataRequest Inputs, SampleDataCounts Counts, int ExistingTransactionCount);

/// <summary>Counts returned only after the complete sample dataset has been committed.</summary>
/// <param name="Inputs">Validated scenario, revision, and date range used for loading.</param>
/// <param name="Created">Committed created counts for each entity type.</param>
/// <param name="Updated">Updated counts; always zero because sample loading is create-only.</param>
public record SampleDataLoadResultDto(SampleDataRequest Inputs, SampleDataCounts Created, SampleDataCounts Updated);

/// <summary>Tenant-free, key-free transaction and rule inputs for a whole-set import.</summary>
/// <param name="Transactions">Generated transactions within the inclusive requested range.</param>
/// <param name="Rules">Rules covering all scenario merchant alternatives, regardless of occurrences in the range.</param>
/// <remarks>Collections are immutable, but DTO validity must still be checked before persistence.</remarks>
public record SampleDataSet(ImmutableArray<TransactionEditDto> Transactions, ImmutableArray<PayeeMatchingRuleEditDto> Rules)
{
    /// <summary>Gets sample-only allocations keyed by transaction ExternalId; absent entries use the normal single split.</summary>
    public ImmutableDictionary<string, ImmutableArray<SampleSplitDto>> TransactionSplits { get; init; }
        = ImmutableDictionary<string, ImmutableArray<SampleSplitDto>>.Empty;
}

/// <summary>A generated sample allocation, in its containing collection's display order.</summary>
/// <param name="Amount">Signed, cent-rounded allocation.</param>
/// <param name="Category">Category to sanitize at persistence.</param>
/// <param name="Memo">Optional split-specific memo.</param>
public record SampleSplitDto(decimal Amount, string Category, string? Memo = null);

/// <summary>An OFX attachment ready for the caller to return or consume.</summary>
/// <param name="Stream">Readable stream positioned at its beginning. The consumer owns disposal.</param>
/// <param name="FileName">Suggested attachment name identifying the scenario and requested dates.</param>
public record SampleOfxDownload(Stream Stream, string FileName)
{
    /// <summary>Gets the media type for an OFX download.</summary>
    public string ContentType => "application/x-ofx";
}
