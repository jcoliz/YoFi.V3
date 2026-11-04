using System.Collections.Immutable;

namespace YoFi.V3.Application.Dto;

public record SampleDataRequest(string ScenarioId, int Revision, DateOnly? StartDate, DateOnly? EndDate);

public record SampleScenarioDto(string Id, int Revision, string Name, string Description, ImmutableArray<string> EntityTypes);

public record SampleDataCounts(int Transactions, int PayeeMatchingRules)
{
    public int Total => Transactions + PayeeMatchingRules;
}

public record SampleDataPreviewDto(SampleDataRequest Inputs, SampleDataCounts Counts, int ExistingTransactionCount);

public record SampleDataLoadResultDto(SampleDataRequest Inputs, SampleDataCounts Created, SampleDataCounts Updated);

public record SampleDataSet(ImmutableArray<TransactionEditDto> Transactions, ImmutableArray<PayeeMatchingRuleEditDto> Rules);

public record SampleOfxDownload(Stream Stream, string FileName)
{
    public string ContentType => "application/x-ofx";
}
