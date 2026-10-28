import { jwtDecode } from 'jwt-decode'

/** Buffer in seconds before actual expiry to trigger a proactive refresh */
const REFRESH_BUFFER_SECONDS = 30

/** Maximum time to wait for an API response before treating it as unavailable. */
const API_REQUEST_TIMEOUT_MS = 30_000

/**
 * Shared promise for in-flight token refresh.
 *
 * When multiple API calls happen concurrently, only the first triggers a
 * refresh; the rest await this same promise instead of starting duplicate
 * refresh requests.
 */
let refreshPromise: Promise<void> | null = null

/**
 * Check whether a JWT is expired or will expire within the buffer window.
 *
 * @param token - Raw token string (may include "Bearer " prefix)
 * @returns `true` if the token is missing, unparseable, or expiring soon
 */
function isTokenExpiredOrExpiring(token: string | null): boolean {
  if (!token) return true

  const clean = token.startsWith('Bearer ') ? token.substring(7) : token

  try {
    const decoded = jwtDecode(clean)
    if (!decoded.exp) return true

    const nowSeconds = Math.floor(Date.now() / 1000)
    return decoded.exp - nowSeconds <= REFRESH_BUFFER_SECONDS
  } catch {
    // Unparseable token — treat as expired so we attempt a refresh
    return true
  }
}

/** Returns whether ofetch aborted the request because its timeout elapsed. */
function isRequestTimeout(error: unknown): boolean {
  if (!(error instanceof Error)) return false

  return (
    error.name === 'TimeoutError' ||
    (error.cause instanceof Error && error.cause.name === 'TimeoutError')
  )
}

/**
 * Composable to create an auth-aware fetch wrapper for NSwag-generated API clients
 *
 * This wraps Nuxt's $fetch to work with the generated API clients, ensuring:
 * - Authentication tokens are automatically included
 * - Tokens are proactively refreshed before each call if expired or expiring within 30 s
 * - Concurrent refresh requests are deduplicated to avoid race conditions
 * - Network errors are properly handled
 * - Binary responses (file downloads) are handled correctly
 *
 * TODO: Consider whether 30 seconds is long enough for large uploads.
 * May need to allow an optional longer timeout for these cases.
 */
export function useAuthFetch(useTokenCheck = true) {
  const { token, refresh } = useAuth()
  const { logger } = useDebugLog('AuthFetch')

  /**
   * Ensure the current access token is fresh.
   *
   * If the token is expired (or about to expire), trigger a refresh via
   * `@sidebase/nuxt-auth`. Concurrent callers share the same in-flight
   * promise so only one refresh request is made at a time.
   *
   * If the refresh fails the error is swallowed — the subsequent API call
   * will proceed with the stale token and the backend will return 401,
   * which is handled by existing error handling.
   */
  async function ensureFreshToken(url: string): Promise<void> {
    if (!isTokenExpiredOrExpiring(token.value)) return

    // Deduplicate: if a refresh is already in flight, wait for it
    if (refreshPromise) {
      await refreshPromise
      return
    }

    refreshPromise = refresh()
      .then(() => {
        logger.debug('Access token refreshed OK')
      })
      .catch(() => {
        logger.warn('Proactive token refresh failed; proceeding with current token')
      })
      .finally(() => {
        refreshPromise = null
      })

    logger.debug('Refreshing access token proactively before API call', { url })
    await refreshPromise
  }

  // Create a wrapper that matches the interface expected by NSwag clients
  const authFetch = {
    fetch: async (url: string | Request, init?: globalThis.RequestInit): Promise<Response> => {
      // Convert Request object to string URL if needed
      const urlString = typeof url === 'string' ? url : url.url

      // Check if the request expects binary data based on Accept header
      // NSwag clients set Accept: application/octet-stream for file downloads
      const headers = init?.headers as Record<string, string> | undefined
      const acceptHeader = headers?.['Accept'] || headers?.['accept']
      const expectsBinary = acceptHeader === 'application/octet-stream'

      // Proactively refresh the token if it is expired or about to expire
      if (useTokenCheck) {
        await ensureFreshToken(urlString)
      }

      // Use Nuxt's $fetch which handles auth automatically
      try {
        const response = await $fetch.raw(urlString, {
          method: (init?.method || 'GET') as any,
          body: init?.body as any,
          headers: {
            ...init?.headers,
            // @sidebase/nuxt-auth automatically adds Authorization header
            // but we can ensure it's included explicitly if needed
            ...(useTokenCheck && token.value ? { Authorization: token.value } : {}),
          },
          // For binary responses, tell $fetch not to parse the body
          // This returns a Blob instead of trying to parse as JSON
          responseType: expectsBinary ? 'blob' : undefined,
          timeout: API_REQUEST_TIMEOUT_MS,
        })

        // Convert Nuxt's response to standard Response object
        // Handle binary vs JSON responses differently
        let body: BodyInit | undefined

        if (expectsBinary) {
          // For binary data, _data is already a Blob - use it directly
          body = response._data as Blob
        } else if (response._data === undefined || response._data === null) {
          // No body
          body = undefined
        } else if (typeof response._data === 'string') {
          // Already a string, use as-is
          body = response._data
        } else {
          // JSON data - stringify it
          body = JSON.stringify(response._data)
        }

        return new Response(body, {
          status: response.status,
          statusText: response.statusText,
          headers: response.headers,
        })
      } catch (error: any) {
        // Handle fetch errors and convert to Response for NSwag client
        const timedOut = isRequestTimeout(error)
        const status = error.response?.status || (timedOut ? 408 : 500)
        const statusText =
          error.response?.statusText || (timedOut ? 'Request Timeout' : 'Internal Server Error')

        // If there's a response body, use it; otherwise create RFC 7807 Problem Details
        let body = error.response?._data || error.data
        if (!body) {
          // Network error or no response - create proper Problem Details
          body = {
            type: 'about:blank',
            title: timedOut ? 'Request timed out' : 'Network Error',
            status,
            detail: timedOut
              ? 'The server did not respond before the request timed out.'
              : error.message || 'Failed to connect to the server',
            instance: urlString,
          }
        }

        return new Response(JSON.stringify(body), {
          status,
          statusText,
          headers: error.response?.headers || {},
        })
      }
    },
  }

  return authFetch
}
