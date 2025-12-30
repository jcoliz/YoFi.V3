namespace YoFi.V3.Application.Dto;

/// <summary>
/// Result of completing the import review workflow.
/// </summary>
/// <param name="AcceptedCount">Number of transactions successfully accepted and copied to the main transaction table.</param>
/// <param name="RejectedCount">Number of transactions rejected (not selected for import).</param>
public record ImportReviewCompleteDto(
    int AcceptedCount,
    int RejectedCount
);
