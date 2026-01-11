namespace YoFi.V3.Application.Dto;

/// <summary>
/// Generic wrapper for collection requests that require validation of each item.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
/// <param name="Items">The collection of items to validate.</param>
/// <remarks>
/// Use this wrapper for API endpoints that accept collections of DTOs and need automatic
/// validation of each item. When paired with <see cref="Validation.CollectionRequestValidator{T}"/>,
/// FluentValidation will automatically validate each item in the collection before the
/// controller action executes.
/// </remarks>
public record CollectionRequest<T>(IReadOnlyCollection<T> Items);
