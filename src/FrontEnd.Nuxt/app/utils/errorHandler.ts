/**
 * API Error Handler Utility
 *
 * Provides a DRY helper function to consistently handle errors from API client calls.
 * Converts all error types (ApiException, ProblemDetails, or unexpected errors) into
 * IProblemDetails format for display with the ErrorDisplay component.
 */

import { ApiException, ProblemDetails, type IProblemDetails } from '~/utils/apiclient'

/**
 * Converts any error from an API client call into IProblemDetails format
 *
 * @param err - The error caught from an API call
 * @param fallbackTitle - Optional custom title for unexpected errors (default: "Unexpected Error")
 * @param fallbackDetail - Optional custom detail message for unexpected errors
 * @returns IProblemDetails object ready for ErrorDisplay component
 *
 * @example
 * ```typescript
 * try {
 *   await apiClient.someMethod()
 * } catch (err) {
 *   error.value = handleApiError(err)
 *   showError.value = true
 * }
 * ```
 *
 * @example With custom fallback messages
 * ```typescript
 * try {
 *   await apiClient.deleteItem(id)
 * } catch (err) {
 *   error.value = handleApiError(err, 'Delete Failed', 'Could not delete the item')
 *   showError.value = true
 * }
 * ```
 */
export function handleApiError(
  err: unknown,
  fallbackTitle: string = 'Unexpected Error',
  fallbackDetail?: string,
): IProblemDetails {
  // Log the error for debugging
  console.error('API error:', err)

  // Handle ApiException (most common case from auto-generated client)
  if (ApiException.isApiException(err)) {
    return err.result
  }

  // Handle direct ProblemDetails instance
  if (err instanceof ProblemDetails) {
    return err
  }

  // Handle unexpected errors (network failures, etc.)
  return {
    title: fallbackTitle,
    detail:
      fallbackDetail ||
      (err instanceof Error
        ? err.message
        : 'An unexpected error occurred while performing the operation'),
  }
}
