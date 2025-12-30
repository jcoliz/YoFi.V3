using YoFi.V3.Application.Helpers;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Generic paginated result container for API responses.
/// </summary>
/// <typeparam name="T">The type of items in the paginated collection.</typeparam>
/// <param name="Items">The collection of items for the current page.</param>
/// <param name="Metadata">Pagination metadata including page numbers, counts, and navigation flags.</param>
public record PaginatedResultDto<T>(
    IReadOnlyCollection<T> Items,
    PaginationMetadata Metadata
);
