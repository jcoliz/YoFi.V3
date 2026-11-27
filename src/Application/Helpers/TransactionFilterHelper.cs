using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Application.Helpers;

/// <summary>
/// Applies the advanced transaction filters without materializing transactions or splits.
/// </summary>
internal static class TransactionFilterHelper
{
    /// <summary>
    /// Adds the supplied advanced filter criteria to a transaction query.
    /// </summary>
    public static IQueryable<Transaction> Apply(
        IQueryable<Transaction> query,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? payee,
        string? category,
        string? memo,
        string? amount,
        bool uncategorizedOnly)
    {
        ValidateDateRange(fromDate, toDate);
        query = ApplyDateRange(query, fromDate, toDate);
        query = ApplyTextFilters(query, payee, category, memo);
        query = ApplyAmountFilter(query, amount);

        if (uncategorizedOnly)
            query = query.Where(transaction => transaction.Splits.Any(split => split.Category == string.Empty));

        return query;
    }

    private static void ValidateDateRange(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ValidationException(nameof(fromDate), "From date cannot be later than to date.");
    }

    private static IQueryable<Transaction> ApplyDateRange(
        IQueryable<Transaction> query,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (fromDate.HasValue)
            query = query.Where(transaction => transaction.Date >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(transaction => transaction.Date <= toDate.Value);
        return query;
    }

    private static IQueryable<Transaction> ApplyTextFilters(
        IQueryable<Transaction> query,
        string? payee,
        string? category,
        string? memo)
    {
        var normalizedPayee = payee?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedPayee))
            query = query.Where(transaction => transaction.Payee.ToLower().Contains(normalizedPayee));

        var normalizedCategory = CategoryHelper.SanitizeCategory(category).ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedCategory))
            query = query.Where(transaction =>
                transaction.Splits.Any(split => split.Category.ToLower().Contains(normalizedCategory)));

        var normalizedMemo = memo?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedMemo))
            query = query.Where(transaction =>
                (transaction.Memo != null && transaction.Memo.ToLower().Contains(normalizedMemo))
                || transaction.Splits.Any(split => split.Memo != null && split.Memo.ToLower().Contains(normalizedMemo)));

        return query;
    }

    private static IQueryable<Transaction> ApplyAmountFilter(IQueryable<Transaction> query, string? amount)
    {
        var normalizedAmount = amount?.Trim();
        if (string.IsNullOrEmpty(normalizedAmount))
            return query;
        if (!TransactionSearchHelper.TryParseAmount(normalizedAmount, out var amountSearch))
            throw new ValidationException("amount", "Amount must be a valid transaction amount.");

        return amountSearch.HasExplicitSign
            ? query.Where(transaction => transaction.Amount == amountSearch.Amount)
            : query.Where(transaction =>
                transaction.Amount == amountSearch.Amount || transaction.Amount == -amountSearch.Amount);
    }
}
