/**
 * Pre-auth token refresh plugin (client-side only)
 *
 * Fixes two related issues where a user with a valid refresh token is redirected
 * to the login page instead of being automatically authenticated on cold start.
 *
 * ## Problem
 * With SSG, all auth runs client-side. On cold start (e.g., opening a fresh browser
 * or tapping the PWA home screen icon), nuxt-auth's `getSession()` is called on
 * `app:mounted`. This fails to authenticate in two scenarios:
 *
 * 1. **No access token present** — `getSession()` exits early without making a network
 *    call (see `useAuth.ts` token guard). The user appears unauthenticated even though
 *    a valid refresh token cookie exists.
 *
 * 2. **Expired access token** — `getSession()` sends the expired JWT to the session
 *    endpoint, the server returns 401, and `getSession()` clears all auth state without
 *    attempting a token refresh.
 *
 * In both cases, the auth middleware sees "unauthenticated" and redirects to login —
 * even though a valid refresh token is still available.
 *
 * Note: `enableOnWindowFocus` does NOT help here. The `visibilitychange` event does not
 * fire on fresh navigation (the page starts already visible — no transition occurs), and
 * the handler also requires an existing session (`data.value`) to be truthy.
 *
 * Note: See ADO Bug 2053 for more exhaustive documentation on the problem and
 * investigation.
 *
 * ## Fix
 * This plugin runs BEFORE the nuxt-auth plugin (`enforce: 'pre'`). It detects when a
 * refresh token cookie exists and proactively calls `refresh()` to obtain fresh tokens.
 * Because `setup()` completes before `app:mounted` fires, the nuxt-auth plugin's
 * subsequent `getSession()` call finds a valid access token already in place and
 * successfully establishes the session.
 *
 * ## When this runs
 * - Only on the client (`.client.ts` suffix)
 * - Only on full page loads (cold start), not SPA navigations
 * - Only when a refresh token cookie exists and the user is not already authenticated
 */
export default defineNuxtPlugin({
  name: 'pre-auth',
  enforce: 'pre',

  async setup() {
    // Only run on client-side
    if (!import.meta.client) {
      return
    }

    // Retrieve the refresh token
    const cookies = document.cookie.split(';').map((c) => c.trim())
    const refreshTokenCookie = cookies.find((c) => c.startsWith('auth.refresh-token='))

    // extract the logging-safe GUID from the refresh token cookie for logging purposes
    const refreshT0kenGuid = refreshTokenCookie?.split('=')[1]?.split('.')[0] ?? 'none'

    // Check if refresh token cookie exists
    const hasRefreshT0ken = cookies.some(
      (c) => c.startsWith('auth.refresh-token=') && c.length > 'auth.refresh-token='.length,
    )
    const hasAuthT0ken = cookies.some(
      (c) => c.startsWith('auth.token=') && c.length > 'auth.token='.length,
    )

    // Detect environment for logging
    const isStandalone =
      window.matchMedia('(display-mode: standalone)').matches ||
      ('standalone' in window.navigator && window.navigator.standalone === true)

    const { logger } = useDebugLog('pre-auth-refresh')

    logger.debug('Plugin starting', {
      hasRefreshT0ken,
      hasAuthT0ken: hasAuthT0ken,
      refreshT0kenGuid: refreshT0kenGuid,
      isStandalone,
      url: window.location.href,
      visibilityState: document.visibilityState,
      navigationEntryType: performance?.getEntriesByType?.('navigation')?.[0]
        ? (performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming).type
        : 'unknown',
    })

    if (!hasRefreshT0ken) {
      // No refresh token — nothing to refresh, let normal auth flow proceed
      logger.debug('No refresh token cookie found, skipping')
      return
    }

    // Attempt to refresh tokens before the auth plugin runs getSession()
    try {
      const { refresh, status } = useAuth()

      // Only refresh if not already authenticated (e.g., from a prior hydration)
      if (status.value === 'authenticated') {
        logger.debug('Already authenticated, skipping', {
          status: status.value,
        })
        return
      }

      logger.debug('Attempting token refresh', {
        status: status.value,
      })

      await refresh()

      logger.debug('Token refresh succeeded', {
        status: status.value,
      })
    } catch (error) {
      // Refresh failed — let the normal auth flow handle it (redirect to login)
      logger.debug('Token refresh failed', {
        error: error instanceof Error ? error.message : String(error),
      })
    }
  },
})
