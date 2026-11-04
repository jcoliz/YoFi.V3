using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Application.Helpers;

/// <summary>Shared create-branch construction for interactive and imported records.</summary>
/// <remarks>
/// Accepts previously validated DTOs and creates untracked entities with fresh Keys.
/// Performs no authorization, persistence, or cache invalidation.
/// </remarks>
internal static class ImportModelFactory
{
    /// <summary>Constructs a transaction with one Order=0 split and a sanitized category.</summary>
    /// <param name="input">Validated transaction input.</param>
    /// <param name="tenantId">Authorized tenant's internal database ID.</param>
    /// <returns>A new transaction whose single split has the same amount as its parent.</returns>
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

    /// <summary>Constructs a rule with sanitized category, common audit time, and unused usage state.</summary>
    /// <param name="input">Validated matching-rule input.</param>
    /// <param name="tenantId">Authorized tenant's internal database ID.</param>
    /// <param name="now">Timestamp assigned to both CreatedAt and ModifiedAt.</param>
    /// <returns>A new rule with zero matches and no last-used timestamp.</returns>
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
