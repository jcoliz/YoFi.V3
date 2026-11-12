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
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            throw new ValidationException(nameof(fromDate), "From date cannot be later than to date.");
        }

        if (fromDate.HasValue)
        {
            query = query.Where(transaction => transaction.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(transaction => transaction.Date <= toDate.Value);
        }

        var normalizedPayee = payee?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedPayee))
        {
            query = query.Where(transaction => transaction.Payee.ToLower().Contains(normalizedPayee));
        }

        var normalizedCategory = CategoryHelper.SanitizeCategory(category).ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedCategory))
        {
            query = query.Where(transaction =>
                transaction.Splits.Any(split => split.Category.ToLower().Contains(normalizedCategory)));
        }

        var normalizedMemo = memo?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedMemo))
        {
            query = query.Where(transaction =>
                (transaction.Memo != null && transaction.Memo.ToLower().Contains(normalizedMemo))
                || transaction.Splits.Any(split => split.Memo != null && split.Memo.ToLower().Contains(normalizedMemo)));
        }

        var normalizedAmount = amount?.Trim();
        if (!string.IsNullOrEmpty(normalizedAmount))
        {
            if (!TransactionSearchHelper.TryParseAmount(normalizedAmount, out var amountSearch))
            {
                throw new ValidationException("amount", "Amount must be a valid transaction amount.");
            }

            query = amountSearch.HasExplicitSign
                ? query.Where(transaction => transaction.Amount == amountSearch.Amount)
                : query.Where(transaction =>
                    transaction.Amount == amountSearch.Amount || transaction.Amount == -amountSearch.Amount);
        }

        if (uncategorizedOnly)
        {
            query = query.Where(transaction => transaction.Splits.Any(split => split.Category == string.Empty));
        }

        return query;
    }
}
