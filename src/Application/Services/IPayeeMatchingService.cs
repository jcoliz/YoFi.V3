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
    /// Applies matching rules to a collection of transactions, returning new DTOs with matched categories.
    /// </summary>
    /// <param name="transactions">Transactions to categorize.</param>
    /// <returns>New collection of transactions with Category field populated from matching rules.</returns>
    /// <remarks>
    /// Since ImportReviewTransactionDto is an immutable record, this method returns a new collection
    /// with updated Category values rather than modifying in-place.
    /// </remarks>
    Task<IReadOnlyCollection<ImportReviewTransactionDto>> ApplyMatchingRulesAsync(
        IReadOnlyCollection<ImportReviewTransactionDto> transactions);
}
