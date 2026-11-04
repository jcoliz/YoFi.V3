using System.Collections.Immutable;

namespace YoFi.V3.Application.SampleData;

public sealed class SampleScenario
{
    internal SampleScenario(string id, int revision, string name, string description, ImmutableArray<SamplePattern> patterns)
    {
        Id = id;
        Revision = revision;
        Name = name;
        Description = description;
        Patterns = patterns;
    }

    public string Id { get; }
    public int Revision { get; }
    public string Name { get; }
    public string Description { get; }
    public ImmutableArray<SamplePattern> Patterns { get; }
}

public sealed class SamplePattern
{
    internal SamplePattern(string id, string category, ImmutableArray<string> payees, decimal annualAmount,
        string frequency, int repeats, string amountJitter, string dateJitter)
    {
        Id = id;
        Category = category;
        Payees = payees;
        AnnualAmount = annualAmount;
        Frequency = frequency;
        Repeats = repeats;
        AmountJitter = amountJitter;
        DateJitter = dateJitter;
    }

    public string Id { get; }
    public string Category { get; }
    public ImmutableArray<string> Payees { get; }
    public decimal AnnualAmount { get; }
    public string Frequency { get; }
    public int Repeats { get; }
    public string AmountJitter { get; }
    public string DateJitter { get; }
    public int PeriodsPerYear => Frequency switch
    {
        "weekly" => 52,
        "semi-monthly" => 24,
        "monthly" => 12,
        "quarterly" => 4,
        "yearly" => 1,
        _ => throw new InvalidOperationException($"Unsupported frequency '{Frequency}'.")
    };
}
