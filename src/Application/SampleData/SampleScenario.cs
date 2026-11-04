using System.Collections.Immutable;

namespace YoFi.V3.Application.SampleData;

/// <summary>Immutable, validated definition of a bundled sample scenario.</summary>
/// <remarks>
/// Constructed by <see cref="Services.SampleScenarioLoader"/> after schema and semantic validation.
/// Display metadata and revision do not affect deterministic generation; the scenario ID
/// namespaces occurrence identities.
/// </remarks>
public sealed class SampleScenario
{
    /// <summary>Initializes a scenario from values already validated by the loader.</summary>
    /// <param name="id">Unique stable catalog ID.</param>
    /// <param name="revision">Positive revision used to reject stale requests.</param>
    /// <param name="name">User-facing scenario name.</param>
    /// <param name="description">User-facing scenario description.</param>
    /// <param name="patterns">Nonempty immutable collection with unique pattern IDs.</param>
    internal SampleScenario(string id, int revision, string name, string description, ImmutableArray<SamplePattern> patterns)
    {
        Id = id;
        Revision = revision;
        Name = name;
        Description = description;
        Patterns = patterns;
    }

    /// <summary>Gets the stable catalog ID used to namespace synthetic occurrence IDs.</summary>
    public string Id { get; }
    /// <summary>Gets the positive revision required by generation requests.</summary>
    public int Revision { get; }
    /// <summary>Gets the user-facing scenario name.</summary>
    public string Name { get; }
    /// <summary>Gets the user-facing description of the scenario's contents.</summary>
    public string Description { get; }
    /// <summary>Gets the validated recurrence patterns; their ordering does not affect occurrence generation.</summary>
    public ImmutableArray<SamplePattern> Patterns { get; }
}

/// <summary>Immutable, validated recurrence and variability settings for one financial pattern.</summary>
/// <remarks>
/// All properties participate in the generation fingerprint, including merchant order.
/// Annual amounts are targets distributed across periods and repeats, not guaranteed realized totals.
/// </remarks>
public sealed class SamplePattern
{
    /// <summary>Initializes a pattern from values already validated by the loader.</summary>
    /// <param name="id">Stable ID unique within the scenario.</param>
    /// <param name="category">Valid category assigned to transactions and merchant rules.</param>
    /// <param name="payees">Nonempty merchant alternatives in fingerprint-significant order.</param>
    /// <param name="annualAmount">Nonzero signed annual target parsed directly as a decimal.</param>
    /// <param name="frequency">Supported recurrence frequency.</param>
    /// <param name="repeats">Positive number of occurrences per canonical period.</param>
    /// <param name="amountJitter">Supported amount jitter level.</param>
    /// <param name="dateJitter">Supported date jitter level.</param>
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

    /// <summary>Gets the stable pattern ID, unique within its scenario.</summary>
    public string Id { get; }
    /// <summary>Gets the category used for direct-loaded transactions and merchant rules.</summary>
    public string Category { get; }
    /// <summary>Gets merchant alternatives in deterministic-choice order.</summary>
    public ImmutableArray<string> Payees { get; }
    /// <summary>Gets the annual target: negative for spending, positive for income.</summary>
    public decimal AnnualAmount { get; }
    /// <summary>Gets the recurrence: weekly, semi-monthly, monthly, quarterly, or yearly.</summary>
    public string Frequency { get; }
    /// <summary>Gets the number of occurrences per period; repeats divide rather than multiply the annual target.</summary>
    public int Repeats { get; }
    /// <summary>Gets the amount variability level: none, low, moderate, or high.</summary>
    public string AmountJitter { get; }
    /// <summary>Gets the within-period date variability level: none, low, moderate, or high.</summary>
    public string DateJitter { get; }
    /// <summary>Gets the canonical annual period count: 52, 24, 12, 4, or 1, respectively.</summary>
    /// <exception cref="InvalidOperationException">The frequency was not validated as a supported value.</exception>
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
