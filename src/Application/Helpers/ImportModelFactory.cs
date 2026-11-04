using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Application.Helpers;

/// <summary>Shared create-branch construction for interactive and imported records.</summary>
internal static class ImportModelFactory
{
    internal static Transaction Transaction(TransactionEditDto input, long tenantId)
    {
        var transaction = new Transaction
        {
            Date = input.Date,
            Amount = input.Amount,
            Payee = input.Payee,
            Memo = input.Memo,
            Source = input.Source,
            ExternalId = input.ExternalId,
            TenantId = tenantId
        };
        transaction.Splits.Add(new Split
        {
            Amount = input.Amount,
            Category = CategoryHelper.SanitizeCategory(input.Category),
            Memo = null,
            Order = 0
        });
        return transaction;
    }

    internal static PayeeMatchingRule Rule(PayeeMatchingRuleEditDto input, long tenantId, DateTimeOffset now)
        => new()
        {
            PayeePattern = input.PayeePattern,
            PayeeIsRegex = input.PayeeIsRegex,
            Category = CategoryHelper.SanitizeCategory(input.Category),
            TenantId = tenantId,
            CreatedAt = now,
            ModifiedAt = now,
            MatchCount = 0,
            LastUsedAt = null
        };
}
