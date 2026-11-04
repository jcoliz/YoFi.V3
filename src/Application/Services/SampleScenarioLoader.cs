using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.SampleData;

namespace YoFi.V3.Application.Services;

/// <summary>Loads only bundled definitions. Invalid assets prevent application registration.</summary>
/// <remarks>
/// Eagerly validates embedded SampleData/Scenarios TOML assets during application
/// registration and exposes an immutable catalog. Tomlyn 2.10.1 (BSD-2-Clause, native
/// net10.0 target) parses TOML directly rather than through application configuration.
/// Explicit table-to-type binding rejects unknown fields and non-string annual amounts.
/// Syntax diagnostics retain source positions; semantic diagnostics identify the asset,
/// scenario, pattern, and field.
/// </remarks>
public sealed partial class SampleScenarioLoader
{
    /// <summary>Loads and validates every embedded scenario in the Application assembly.</summary>
    /// <exception cref="InvalidOperationException">Assets are missing, invalid, empty, or contain duplicate scenario IDs.</exception>
    public SampleScenarioLoader() : this(LoadResources()) { }

    /// <summary>Loads an explicit collection of scenario documents for catalog validation tests.</summary>
    /// <param name="documents">TOML text paired with source labels used only in diagnostics, not opened as paths.</param>
    /// <exception cref="InvalidOperationException">The catalog is empty or contains invalid documents or duplicate IDs.</exception>
    internal SampleScenarioLoader(IEnumerable<(string Source, string Text)> documents)
    {
        var scenarios = documents.Select(d => Parse(d.Text, d.Source)).ToImmutableArray();
        if (scenarios.IsEmpty)
        {
            throw new InvalidOperationException("No bundled sample scenarios were found.");
        }

        var duplicate = scenarios.GroupBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"Duplicate sample scenario ID '{duplicate.Key}'.");
        }

        Scenarios = scenarios;
    }

    /// <summary>Gets the immutable, nonempty catalog with unique scenario IDs and validated patterns.</summary>
    public ImmutableArray<SampleScenario> Scenarios { get; }

    /// <summary>Parses and semantically validates one scenario without accessing the filesystem.</summary>
    /// <param name="text">Complete TOML scenario text with quoted invariant decimal annual amounts.</param>
    /// <param name="source">Diagnostic label for the asset; never interpreted as an input path.</param>
    /// <returns>An immutable typed scenario ready for deterministic generation.</returns>
    /// <exception cref="InvalidOperationException">
    /// TOML syntax, required fields, supported values, identifiers, merchant/category consistency,
    /// DTO length constraints, or safe nonzero amount bounds are invalid.
    /// </exception>
    internal static SampleScenario Parse(string text, string source)
    {
        TomlTable table;
        try
        {
            table = TomlSerializer.Deserialize<TomlTable>(text, new TomlSerializerOptions { SourceName = source })
                ?? throw new InvalidOperationException($"{source}: Missing scenario document.");
        }
        catch (TomlException ex)
        {
            throw new InvalidOperationException($"{source}: Invalid sample scenario TOML: {ex.Message}", ex);
        }

        RejectUnknown(table, source, "id", "revision", "name", "description", "patterns");
        var id = ReadString(table, "id", source);
        var context = $"{source}, scenario '{id}'";
        ValidateId(id, context);
        var revision = ReadPositiveInt(table, "revision", context);
        var name = ReadString(table, "name", context);
        var description = ReadString(table, "description", context);
        if (!table.TryGetValue("patterns", out var rawPatterns) || rawPatterns is not TomlTableArray patterns || patterns.Count == 0)
        {
            throw Invalid(context, "patterns", "Expected a nonempty array of pattern tables.");
        }

        var result = ImmutableArray.CreateBuilder<SamplePattern>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var merchants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in patterns)
        {
            var patternId = ReadString(pattern, "id", context);
            var location = $"{context}, pattern '{patternId}'";
            RejectUnknown(pattern, location, "id", "category", "payees", "annual_amount", "frequency",
                "repeats", "amount_jitter", "date_jitter");
            ValidateId(patternId, location);
            if (!ids.Add(patternId))
            {
                throw Invalid(location, "id", "Duplicate pattern ID.");
            }

            var category = ReadString(pattern, "category", location);
            if (category.Length > 200 || CategoryHelper.SanitizeCategory(category).Length == 0)
            {
                throw Invalid(location, "category", "Expected a valid category of at most 200 characters.");
            }

            if (!pattern.TryGetValue("payees", out var rawPayees) || rawPayees is not TomlArray payees || payees.Count == 0)
            {
                throw Invalid(location, "payees", "Expected a nonempty merchant array.");
            }
            var typedPayees = ImmutableArray.CreateBuilder<string>();
            foreach (var value in payees)
            {
                if (value is not string payee || string.IsNullOrWhiteSpace(payee) || payee.Length > 200 || payee.Any(char.IsControl))
                {
                    throw Invalid(location, "payees", "Merchants must be nonempty strings of at most 200 characters without control characters.");
                }
                var sanitizedCategory = CategoryHelper.SanitizeCategory(category);
                if (merchants.TryGetValue(payee, out var existing) && existing != sanitizedCategory)
                {
                    throw Invalid(location, "payees", $"Merchant '{payee}' has conflicting categories.");
                }
                merchants[payee] = sanitizedCategory;
                typedPayees.Add(payee);
            }

            var annualText = ReadString(pattern, "annual_amount", location);
            if (!decimal.TryParse(annualText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var annual) || annual == 0)
            {
                throw Invalid(location, "annual_amount", "Expected a quoted nonzero invariant decimal.");
            }
            if (Math.Abs(annual) > decimal.MaxValue / SampleDataGenerator.MaxTransactions / 2)
            {
                throw Invalid(location, "annual_amount", "Amount exceeds safe decimal arithmetic bounds for a maximum-sized statement.");
            }
            var frequency = ReadChoice(pattern, "frequency", location, "weekly", "semi-monthly", "monthly", "quarterly", "yearly");
            var repeats = ReadPositiveInt(pattern, "repeats", location);
            if (repeats > SampleDataGenerator.MaxTransactions)
            {
                throw Invalid(location, "repeats", $"Cannot exceed {SampleDataGenerator.MaxTransactions}.");
            }
            var amountJitter = ReadChoice(pattern, "amount_jitter", location, "none", "low", "moderate", "high");
            var dateJitter = ReadChoice(pattern, "date_jitter", location, "none", "low", "moderate", "high");
            var definition = new SamplePattern(patternId, category, typedPayees.ToImmutable(), annual, frequency, repeats, amountJitter, dateJitter);
            var minimumAmount = Math.Abs(annual / definition.PeriodsPerYear / repeats) * (1 - SampleDataGenerator.JitterProportion(amountJitter));
            if (decimal.Round(minimumAmount, 2, MidpointRounding.AwayFromZero) == 0)
            {
                throw Invalid(location, "annual_amount", "Amount must remain nonzero after jitter and cent rounding, and within decimal arithmetic bounds.");
            }
            result.Add(definition);
        }

        return new SampleScenario(id, revision, name, description, result.ToImmutable());
    }

    private static IEnumerable<(string Source, string Text)> LoadResources()
    {
        var assembly = typeof(SampleScenarioLoader).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".SampleData.Scenarios.", StringComparison.Ordinal) && n.EndsWith(".toml", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Missing bundled scenario '{resource}'.");
            using var reader = new StreamReader(stream);
            yield return (resource, reader.ReadToEnd());
        }
    }

    private static string ReadString(TomlTable table, string field, string context)
    {
        if (!table.TryGetValue(field, out var value) || value is not string text || string.IsNullOrWhiteSpace(text))
        {
            throw Invalid(context, field, "Expected a nonempty string.");
        }
        return text;
    }

    private static int ReadPositiveInt(TomlTable table, string field, string context)
    {
        if (!table.TryGetValue(field, out var value) || value is not long number || number <= 0 || number > int.MaxValue)
        {
            throw Invalid(context, field, "Expected a positive 32-bit integer.");
        }
        return (int)number;
    }

    private static string ReadChoice(TomlTable table, string field, string context, params string[] choices)
    {
        var value = ReadString(table, field, context);
        if (!choices.Contains(value, StringComparer.Ordinal))
        {
            throw Invalid(context, field, $"Unsupported value '{value}'. Expected {string.Join(", ", choices)}.");
        }
        return value;
    }

    private static void RejectUnknown(TomlTable table, string context, params string[] fields)
    {
        foreach (var field in table.Keys)
        {
            if (!fields.Contains(field, StringComparer.Ordinal))
            {
                throw Invalid(context, field, "Unknown or unsupported field.");
            }
        }
    }

    private static void ValidateId(string id, string context)
    {
        if (id.Length > 64 || !StableId().IsMatch(id))
        {
            throw Invalid(context, "id", "Expected a stable lowercase hyphenated ID of at most 64 characters.");
        }
    }

    private static InvalidOperationException Invalid(string context, string field, string message)
        => new($"{context}, field '{field}': {message}");

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex StableId();
}
