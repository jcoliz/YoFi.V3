namespace YoFi.V3.Application.Dto;

/// <summary>
/// Provides summary statistics for pending import review transactions.
/// </summary>
/// <param name="TotalCount">Total number of pending import review transactions.</param>
/// <param name="SelectedCount">Number of transactions selected for import (IsSelected = true).</param>
/// <param name="NewCount">Number of new transactions (not duplicates).</param>
/// <param name="ExactDuplicateCount">Number of exact duplicate transactions.</param>
/// <param name="PotentialDuplicateCount">Number of potential duplicate transactions.</param>
/// <remarks>
/// Used by the frontend to display statistics and enable/disable the Import button.
/// Import button should be enabled when SelectedCount > 0.
/// </remarks>
public record ImportReviewSummaryDto(
    int TotalCount,
    int SelectedCount,
    int NewCount,
    int ExactDuplicateCount,
    int PotentialDuplicateCount
);
