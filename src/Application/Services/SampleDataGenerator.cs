using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.SampleData;
using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.Application.Services;

/// <summary>Clock-free, tenant-free deterministic sample generation.</summary>
/// <remarks>
/// <para>
/// Has no database dependencies or shared random state. Requests are limited to one
/// rolling calendar year (inclusive end strictly before the start anniversary) and
/// 10,000 transactions. Deterministic in-range occurrences are counted before allocating
/// the dataset. Leap years and ranges crossing December are supported; oversized requests
/// fail rather than truncate.
/// </para>
/// <para>
/// Annual targets divide by periods and repeats. Weekly periods are the first 52
/// seven-day periods from January 1; semi-monthly anchors are the 1st and 15th.
/// Monthly, quarterly, and yearly periods start at their calendar boundaries.
/// Date jitter chooses a forward offset from zero through
/// floor((periodDays - 1) * proportion), within the canonical period.
/// Jitter proportions are none=0%, low=10%, moderate=40%, high=90%.
/// Amount jitter is symmetric about the base amount; cent rounding uses midpoint
/// away from zero. Annual totals are targets, not reconciled balances.
/// </para>
/// <para>
/// Fingerprints use SHA-256 over ordered UTF-8 fields, each prefixed by its four-byte
/// little-endian byte length: pattern ID, category, merchant count, merchant alternatives
/// in order, invariant G29 decimal, frequency, invariant repeats, amount jitter, date jitter.
/// Occurrence keys hash fingerprint/year/zero-based period/repeat with the same encoding.
/// Choice hashes add purpose (date, amount, payee); the first eight bytes form a big-endian
/// unsigned integer, mapped by modulo for dates/payees or divided by UInt64.MaxValue for
/// amount jitter. FITIDs are "sample:" plus the SHA-256 hex hash of scenario ID and
/// occurrence key. Formatting, display metadata, and revisions do not enter these hashes.
/// Generation-relevant edits intentionally create distinct FITIDs.
/// </para>
/// </remarks>
public sealed class SampleDataGenerator
{
    public const int MaxTransactions = 10_000;
    public const string Source = "YoFi Sample Bank - Checking (SAMPLE0001)";

    public SampleDataSet Generate(SampleScenario scenario, DateOnly start, DateOnly end)
    {
        ValidateRange(start, end);
        var definitions = scenario.Patterns.Select(p => (Pattern: p, Fingerprint: Fingerprint(p))).ToArray();

        // Count before allocating the dataset; the same date choices are used by both passes.
        var count = 0;
        foreach (var definition in definitions)
        {
            foreach (var occurrence in Occurrences(definition.Pattern, definition.Fingerprint, start, end))
            {
                if (++count > MaxTransactions)
                {
                    throw new ValidationException(nameof(start), $"Sample requests cannot exceed {MaxTransactions:N0} transactions.");
                }
            }
        }

        var transactions = ImmutableArray.CreateBuilder<TransactionEditDto>(count);
        foreach (var (pattern, fingerprint) in definitions)
        {
            foreach (var (year, period, repeat, date) in Occurrences(pattern, fingerprint, start, end))
            {
                var key = OccurrenceKey(fingerprint, year, period, repeat);
                var factor = 1 + (2 * UnitChoice(key, "amount") - 1) * JitterProportion(pattern.AmountJitter);
                var amount = decimal.Round(pattern.AnnualAmount / pattern.PeriodsPerYear / pattern.Repeats * factor,
                    2, MidpointRounding.AwayFromZero);
                if (amount == 0)
                {
                    throw new ValidationException($"Scenario '{scenario.Id}', pattern '{pattern.Id}' generated a zero amount.");
                }
                var payee = pattern.Payees[(int)(Choice(key, "payee") % (ulong)pattern.Payees.Length)];
                var externalId = "sample:" + Hash(Canonical(scenario.Id, key));
                transactions.Add(new TransactionEditDto(date, amount, payee, null, Source, externalId, pattern.Category));
            }
        }

        var rules = scenario.Patterns.SelectMany(p => p.Payees.Select(payee =>
                new PayeeMatchingRuleEditDto(payee, false, p.Category)))
            .DistinctBy(r => (r.PayeePattern.ToUpperInvariant(), CategoryHelper.SanitizeCategory(r.Category)))
            .OrderBy(r => r.PayeePattern, StringComparer.Ordinal).ToImmutableArray();
        return new SampleDataSet(transactions.OrderBy(t => t.Date).ThenBy(t => t.ExternalId, StringComparer.Ordinal).ToImmutableArray(), rules);
    }

    public static void ValidateRange(DateOnly start, DateOnly end)
    {
        if (start == DateOnly.MinValue || end == DateOnly.MinValue || start > end)
        {
            throw new ValidationException(nameof(start), "Explicit start and end dates are required, with start on or before end.");
        }
        if (start.Year == 9999 || end >= start.AddYears(1))
        {
            throw new ValidationException(nameof(end), "Sample requests cannot exceed one calendar year (inclusive end before the start anniversary).");
        }
    }

    internal static decimal JitterProportion(string jitter) => jitter switch
    {
        "none" => 0m,
        "low" => .1m,
        "moderate" => .4m,
        "high" => .9m,
        _ => throw new InvalidOperationException($"Unsupported jitter '{jitter}'.")
    };

    internal static string Fingerprint(SamplePattern pattern)
        => Hash(Canonical(
            [pattern.Id, pattern.Category, pattern.Payees.Length.ToString(CultureInfo.InvariantCulture),
            .. pattern.Payees, pattern.AnnualAmount.ToString("G29", CultureInfo.InvariantCulture),
            pattern.Frequency, pattern.Repeats.ToString(CultureInfo.InvariantCulture), pattern.AmountJitter, pattern.DateJitter]));

    private static IEnumerable<(int Year, int Period, int Repeat, DateOnly Date)> Occurrences(
        SamplePattern pattern, string fingerprint, DateOnly start, DateOnly end)
    {
        for (var year = start.Year; year <= end.Year; year++)
        {
            for (var period = 0; period < pattern.PeriodsPerYear; period++)
            {
                var (anchor, days) = Period(pattern.Frequency, year, period);
                var maxOffset = (int)decimal.Floor((days - 1) * JitterProportion(pattern.DateJitter));
                for (var repeat = 0; repeat < pattern.Repeats; repeat++)
                {
                    var key = OccurrenceKey(fingerprint, year, period, repeat);
                    var offset = (int)(Choice(key, "date") % (ulong)(maxOffset + 1));
                    var date = anchor.AddDays(offset);
                    if (date >= start && date <= end)
                    {
                        yield return (year, period, repeat, date);
                    }
                }
            }
        }
    }

    private static (DateOnly Anchor, int Days) Period(string frequency, int year, int period)
    {
        var jan = new DateOnly(year, 1, 1);
        return frequency switch
        {
            "weekly" => (jan.AddDays(period * 7), 7),
            "semi-monthly" => (new DateOnly(year, period / 2 + 1, period % 2 == 0 ? 1 : 15),
                period % 2 == 0 ? 14 : DateTime.DaysInMonth(year, period / 2 + 1) - 14),
            "monthly" => (jan.AddMonths(period), DateTime.DaysInMonth(year, period + 1)),
            "quarterly" => (jan.AddMonths(period * 3),
                Enumerable.Range(period * 3 + 1, 3).Sum(month => DateTime.DaysInMonth(year, month))),
            "yearly" => (jan, DateTime.IsLeapYear(year) ? 366 : 365),
            _ => throw new InvalidOperationException($"Unsupported frequency '{frequency}'.")
        };
    }

    private static string OccurrenceKey(string fingerprint, int year, int period, int repeat)
        => Hash(Canonical(fingerprint, year.ToString(CultureInfo.InvariantCulture),
            period.ToString(CultureInfo.InvariantCulture), repeat.ToString(CultureInfo.InvariantCulture)));

    private static ulong Choice(string key, string purpose)
        => BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Canonical(key, purpose)));

    private static decimal UnitChoice(string key, string purpose) => Choice(key, purpose) / (decimal)ulong.MaxValue;

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] Canonical(params string[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        writer.Flush();
        return stream.ToArray();
    }
}
