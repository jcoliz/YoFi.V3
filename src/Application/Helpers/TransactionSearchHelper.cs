using System.Globalization;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Application.Helpers;

/// <summary>
/// Represents a parsed transaction amount search and its sign restriction.
/// </summary>
/// <param name="Amount">The parsed amount, including any explicit negative sign.</param>
/// <param name="HasExplicitSign">Whether the search restricts matching to the specified sign.</param>
internal readonly record struct TransactionAmountSearch(decimal Amount, bool HasExplicitSign);

/// <summary>
/// Builds transaction quick-search queries and parses optional amount comparisons.
/// </summary>
/// <remarks>
/// Text matching uses provider-translatable lowercase and literal substring expressions.
/// SQLite's built-in lowercase handling supports ASCII case matching only.
/// </remarks>
internal static partial class TransactionSearchHelper
{
    /// <summary>
    /// Adds a search restriction without executing or materializing the transaction query.
    /// </summary>
    /// <param name="query">The transaction query with any existing tenant, date, and ordering constraints.</param>
    /// <param name="search">The search term; null, empty, or whitespace-only values leave the query unchanged.</param>
    /// <returns>A query matching any searchable text field or the parsed transaction amount.</returns>
    /// <remarks>
    /// Trims the term and searches payee, transaction memo, and every split's category and memo.
    /// Numeric terms also match transaction amounts: unsigned terms match either sign,
    /// while explicitly signed terms require exact signed equality. Invalid amounts remain text-only searches.
    /// </remarks>
    public static IQueryable<Transaction> Apply(IQueryable<Transaction> query, string? search)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return query;
        }

        // Keep matching in the provider; SQLite's built-in lower() is ASCII-only.
        var normalizedTerm = term.ToLowerInvariant();
        var hasAmountSearch = TryParseAmount(term, out var amountSearch);

        return query.Where(transaction =>
            transaction.Payee.ToLower().Contains(normalizedTerm)
            || (transaction.Memo != null && transaction.Memo.ToLower().Contains(normalizedTerm))
            || transaction.Splits.Any(split => split.Category.ToLower().Contains(normalizedTerm))
            || transaction.Splits.Any(split => split.Memo != null && split.Memo.ToLower().Contains(normalizedTerm))
            || (hasAmountSearch
                && (transaction.Amount == amountSearch.Amount
                    || (!amountSearch.HasExplicitSign && transaction.Amount == -amountSearch.Amount))));
    }

    /// <summary>
    /// Attempts to parse an invariant-culture decimal search term with optional currency and sign prefixes.
    /// </summary>
    /// <param name="term">The already-trimmed search term.</param>
    /// <param name="amountSearch">The parsed amount and sign restriction, or the default value if parsing fails.</param>
    /// <returns>True when the term has an accepted numeric format and fits within the decimal range.</returns>
    /// <remarks>
    /// Accepts one optional dollar prefix and one optional sign, with the sign before or after the dollar.
    /// Numbers use ASCII digits, an optional decimal point, and optional comma-separated groups of three digits.
    /// Other currency symbols, exponents, whitespace, repeated prefixes, and malformed grouping are rejected.
    /// </remarks>
    public static bool TryParseAmount(string term, out TransactionAmountSearch amountSearch)
    {
        amountSearch = default;

        var index = 0;
        char? sign = null;
        var hasCurrencyPrefix = false;

        if (index < term.Length && IsSign(term[index]))
        {
            sign = term[index++];
        }

        if (index < term.Length && term[index] == '$')
        {
            hasCurrencyPrefix = true;
            index++;
        }

        if (sign is null && hasCurrencyPrefix && index < term.Length && IsSign(term[index]))
        {
            sign = term[index++];
        }

        var number = term[index..];
        if (!IsValidNumber(number))
        {
            return false;
        }

        var normalizedNumber = number.Replace(",", string.Empty, StringComparison.Ordinal);
        if (sign == '-')
        {
            normalizedNumber = string.Concat("-", normalizedNumber);
        }
        else if (sign == '+')
        {
            normalizedNumber = string.Concat("+", normalizedNumber);
        }

        if (!decimal.TryParse(
                normalizedNumber,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return false;
        }

        amountSearch = new TransactionAmountSearch(amount, sign.HasValue);
        return true;
    }

    /// <summary>
    /// Validates the unsigned numeric portion before invariant decimal parsing.
    /// </summary>
    /// <param name="number">The numeric portion after removing any currency and sign prefixes.</param>
    /// <returns>True for ASCII digits with an optional decimal point and correctly grouped thousands separators.</returns>
    private static bool IsValidNumber(string number)
    {
        if (number.Length == 0)
        {
            return false;
        }

        var decimalIndex = number.IndexOf('.');
        if (decimalIndex != number.LastIndexOf('.'))
        {
            return false;
        }

        var integerPart = decimalIndex < 0 ? number : number[..decimalIndex];
        var fractionalPart = decimalIndex < 0 ? string.Empty : number[(decimalIndex + 1)..];
        if (decimalIndex >= 0 && fractionalPart.Any(character => !char.IsAsciiDigit(character)))
        {
            return false;
        }

        if (integerPart.Length == 0)
        {
            return decimalIndex >= 0 && fractionalPart.Length > 0;
        }

        var groups = integerPart.Split(',');
        if (groups.Any(group => group.Length == 0 || group.Any(character => !char.IsAsciiDigit(character))))
        {
            return false;
        }

        if (groups.Length > 1
            && (groups[0].Length is < 1 or > 3 || groups.Skip(1).Any(group => group.Length != 3)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a character is an explicit amount sign.
    /// </summary>
    /// <param name="character">The character to inspect.</param>
    /// <returns>True for a plus or minus sign.</returns>
    private static bool IsSign(char character) => character is '+' or '-';
}
