namespace YoFi.V3.Application.Dto;

/// <summary>
/// Generic paginated result container for API responses.
/// </summary>
/// <typeparam name="T">The type of items in the paginated collection.</typeparam>
/// <param name="Items">The collection of items for the current page.</param>
/// <param name="PageNumber">The current page number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
/// <param name="TotalPages">The total number of pages available.</param>
/// <param name="HasPreviousPage">Indicates whether a previous page exists.</param>
/// <param name="HasNextPage">Indicates whether a next page exists.</param>
public record PaginatedResultDto<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage
);
