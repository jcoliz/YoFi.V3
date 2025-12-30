namespace YoFi.V3.Application.Helpers;

/// <summary>
/// Helper class for calculating pagination metadata.
/// </summary>
public static class PaginationHelper
{
    /// <summary>
    /// Calculates pagination metadata for a given page configuration.
    /// </summary>
    /// <param name="pageNumber">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <returns>A <see cref="PaginationMetadata"/> object containing all calculated pagination values.</returns>
    public static PaginationMetadata Calculate(int pageNumber, int pageSize, int totalCount)
    {
        var totalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;
        var hasPreviousPage = pageNumber > 1;
        var hasNextPage = pageNumber < totalPages;
        var firstItem = totalCount > 0 ? (pageNumber - 1) * pageSize + 1 : 0;
        var lastItem = totalCount > 0 ? Math.Min(pageNumber * pageSize, totalCount) : 0;

        return new PaginationMetadata(
            PageNumber: pageNumber,
            PageSize: pageSize,
            TotalCount: totalCount,
            TotalPages: totalPages,
            HasPreviousPage: hasPreviousPage,
            HasNextPage: hasNextPage,
            FirstItem: firstItem,
            LastItem: lastItem
        );
    }
}

/// <summary>
/// Contains all calculated pagination metadata values.
/// </summary>
/// <param name="PageNumber">The current page number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
/// <param name="TotalPages">The total number of pages available.</param>
/// <param name="HasPreviousPage">Indicates whether a previous page exists.</param>
/// <param name="HasNextPage">Indicates whether a next page exists.</param>
/// <param name="FirstItem">The index of the first item on the current page (1-based), or 0 if no items exist.</param>
/// <param name="LastItem">The index of the last item on the current page (1-based), or 0 if no items exist.</param>
public record PaginationMetadata(
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage,
    int FirstItem,
    int LastItem
);
