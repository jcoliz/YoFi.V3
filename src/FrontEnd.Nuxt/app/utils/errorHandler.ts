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
 * @param fallbackTitle - Optional custom title to use if API error has no title, or for unexpected errors
 * @param fallbackDetail - Optional custom detail message to prepend to API error details for better context
 * @returns IProblemDetails object ready for ErrorDisplay component
 *
 * @example Basic usage (uses API error details if available)
 * ```typescript
 * try {
 *   await apiClient.someMethod()
 * } catch (err) {
 *   error.value = handleApiError(err)
 *   showError.value = true
 * }
 * ```
 *
 * @example With context-aware fallback messages (combines context with API error details)
 * ```typescript
 * try {
 *   await apiClient.deleteItem(id)
 * } catch (err) {
 *   // If API returns detail "Item does not exist", user sees:
 *   // "Could not delete the item. Item does not exist"
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
    const result = err.result
    return {
      ...result,
      title: result.title || fallbackTitle,
      detail: combineDetails(fallbackDetail, result.detail, result.status),
    }
  }

  // Handle direct ProblemDetails instance
  if (err instanceof ProblemDetails) {
    return {
      ...err,
      title: err.title || fallbackTitle,
      detail: combineDetails(fallbackDetail, err.detail, err.status),
    }
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

/**
 * Gets a friendly message based on HTTP status code
 * @param status - HTTP status code
 * @returns User-friendly message for the status code
 */
function getFriendlyMessageForStatus(status: number | undefined): string | undefined {
  if (!status) return undefined

  const friendlyMessages: Record<number, string> = {
    400: 'Please check the information you provided and try again.',
    401: 'You need to be logged in to access this resource.',
    403: 'You do not have permission to access this resource.',
    404: 'The requested resource could not be found.',
    409: 'This operation conflicts with the current state of the resource.',
    500: 'An internal server error occurred. Please try again later.',
    502: 'The server received an invalid response from an upstream server.',
    503: 'The service is temporarily unavailable. Please try again later.',
  }

  return friendlyMessages[status] || 'An error occurred while processing your request.'
}

/**
 * Combines fallback detail with API error detail and friendly status message
 * @param fallbackDetail - The context-specific fallback message
 * @param apiDetail - The detail from the API error
 * @param status - HTTP status code for friendly message
 * @returns Combined detail string with appropriate friendly messages
 */
function combineDetails(
  fallbackDetail: string | undefined,
  apiDetail: string | undefined,
  status: number | undefined,
): string {
  const friendlyMessage = getFriendlyMessageForStatus(status)

  // If we have an API detail, use it (it's the most specific)
  if (apiDetail) {
    // If we also have a fallback, prepend it
    return fallbackDetail ? `${fallbackDetail}. ${apiDetail}` : apiDetail
  }

  // No API detail - combine fallback and friendly message when both exist
  if (fallbackDetail && friendlyMessage) {
    return `${fallbackDetail}. ${friendlyMessage}`
  }

  // If we have a fallback, use it
  if (fallbackDetail) {
    return fallbackDetail
  }

  // If we have a friendly message based on status, use it
  if (friendlyMessage) {
    return friendlyMessage
  }

  // Last resort default
  return 'An error occurred while performing the operation'
}
