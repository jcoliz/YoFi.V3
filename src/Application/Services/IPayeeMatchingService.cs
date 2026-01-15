using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Services;

/// <summary>
/// Provides payee matching rule operations for transaction categorization.
/// </summary>
/// <remarks>
/// This interface follows the Interface Segregation Principle by exposing only
/// the matching operations needed by bank import, not the full CRUD operations
/// available in PayeeMatchingRuleFeature.
/// </remarks>
public interface IPayeeMatchingService
{
    /// <summary>
    /// Applies matching rules to a collection of transactions, returning matched categories in parallel order.
    /// </summary>
    /// <param name="transactions">Transactions to categorize.</param>
    /// <returns>Parallel array of categories (null if no match). Order matches input transactions.</returns>
    /// <remarks>
    /// Returns categories in the same order as the input transactions. This allows the caller to zip
    /// the results with the original transaction data. Null is returned for transactions
    /// that don't match any rules.
    /// </remarks>
    Task<IReadOnlyList<string?>> ApplyMatchingRulesAsync(
        IReadOnlyCollection<IMatchableTransaction> transactions);
}
