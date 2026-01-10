namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Represents import statistics displayed in the import confirmation modal.
/// </summary>
/// <param name="SelectedCount">Number of transactions selected for import.</param>
/// <param name="DiscardedCount">Number of transactions that will be discarded (null if not displayed).</param>
/// <param name="PotentialDuplicateCount">Number of potential duplicate transactions detected (null if not displayed).</param>
/// <remarks>
/// This DTO captures statistics scraped from the import confirmation modal's test elements:
/// - import-selected-count (always present)
/// - import-discarded-count (optional - only displayed when there are discarded transactions)
/// - import-potential-duplicate-count (optional - only displayed when there are potential duplicates)
///
/// Used by ImportPage.GetImportStatisticsAsync() to return structured data from the UI.
/// </remarks>
public record ImportStatisticsDto(
    int SelectedCount,
    int? DiscardedCount,
    int? PotentialDuplicateCount
);
