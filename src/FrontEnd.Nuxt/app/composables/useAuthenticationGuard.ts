/**
 * useAuthenticationGuard — Page-local authentication guards for SSG pages
 *
 * ## Problem
 *
 * This app is statically generated (SSG). On a hard reload, the nuxt-auth plugin
 * defers `getSession()` to `app:mounted` (via `disableServerSideAuth: true` in
 * nuxt.config.ts), which means `auth.status` starts as `'loading'` and resolves
 * asynchronously. The global nuxt-auth middleware has no handling for the `'loading'`
 * state — it falls through to redirect-to-login — causing a visible flash of the
 * login screen before bouncing back to the protected page.
 *
 * See: plans/bug-2035-login-flash-on-refresh.md for the full investigation and fix
 * hypothesis.
 *
 * ## Solution
 *
 * Pages opt out of the global middleware (`auth: false` in `definePageMeta`) and use
 * this composable instead. Both guards wait for `auth.status` to resolve after mount:
 *
 * 1. `requireAuthenticatedUser(action?)`:
 *    - Authenticated: runs the optional async `action`.
 *    - Unauthenticated: redirects to login with a `?redirect=` param so the user
 *      returns to the protected page after signing in.
 *    - Loading: does nothing until the watcher receives the resolved status.
 * 2. `requireGuestUser()`:
 *    - Authenticated: redirects to the named `profile` route.
 *    - Unauthenticated: allows the guest-only page to remain accessible.
 *    - Loading: does nothing until the watcher receives the resolved status.
 *
 * The `onMounted` check handles an already-resolved status during normal client-side
 * navigation. The `watch` handles the SSG reload case where `getSession()` resolves
 * from `'loading'` after mount, as well as later authentication status changes such
 * as a failed background token refresh.
 *
 * ## Usage
 *
 * In a protected page component:
 *
 * ```ts
 * definePageMeta({
 *   // Opt out of global middleware — this composable manages auth for this page.
 *   // See: plans/bug-2035-login-flash-on-refresh.md
 *   auth: false,
 * })
 *
 * async function getData() {
 *   // No auth guard needed here — this is called only when authenticated.
 *   ...
 * }
 *
 * const { requireAuthenticatedUser } = useAuthenticationGuard()
 * requireAuthenticatedUser(getData)
 * ```
 *
 * In a guest-only page component:
 *
 * ```ts
 * definePageMeta({ auth: false })
 *
 * const { requireGuestUser } = useAuthenticationGuard()
 * requireGuestUser()
 * ```
 *
 * The optional authenticated action must be idempotent because it may run more than
 * once if auth status transitions through `'loading'`, such as during a background
 * token refresh.
 */
export function useAuthenticationGuard() {
  const { status } = useAuth()
  const route = useRoute()
  const userPreferences = useUserPreferencesStore()

  const previousPreviousStatus = ref(status.value)

  // Set minimumLevel: 'debug' to re-enable extensive debug logging here if needed
  // for some future problem.
  const { logger } = useDebugLog('AuthenticationGuard', { minimumLevel: 'info' })

  const redirectToLogin = (redirect?: string) => {
    if (redirect) {
      logger.debug('redirectToPath', { redirect })
      navigateTo({ path: redirect })
      return
    }

    logger.debug('redirectToLogin', {
      redirect: route.path,
    })
    navigateTo({
      name: 'login',
      query: { redirect: route.path },
    })
  }

  const redirectAsDesiredOrToBrowse = (redirect?: string) => {
    logger.debug('redirectAsDesired', { redirect })

    if (redirect) {
      navigateTo({ path: redirect })
      return
    }
    navigateTo({ name: 'transactions' })
  }

  /**
   * Runs an optional action for authenticated users and redirects everyone
   * else to login, preserving the current path for the post-login return.
   */
  const requireAuthenticatedUser = (
    authenticatedAction?: () => Promise<void>,
    unauthenticatedRedirect?: string,
  ) => {
    logger.debug('starting watcher', {
      source: 'requireAuthenticatedUser',
      bug: 2108,
    })

    const shouldRunAuthenticatedAction = (
      currentStatus: typeof status.value,
      previousStatus: typeof status.value,
      was: typeof status.value,
    ) => {
      if (currentStatus !== 'authenticated') {
        return false
      }

      // Bug 2108: when an already-authenticated app briefly revalidates auth via
      // a loading state, we must not re-run the page load action. The SPAs that
      // keeps the page mounted while the browser resumes from the background can
      // produce: authenticated -> loading -> authenticated.
      if (previousStatus === 'loading' && was === 'authenticated') {
        logger.debug('skipping authenticated action', {
          source: 'watcher',
          bug: 2108,
          previousStatus,
          was,
        })
        return false
      }

      return true
    }

    watch(status, (currentStatus, previousStatus) => {
      const was = previousPreviousStatus.value
      previousPreviousStatus.value = previousStatus

      logger.debug('edge', {
        source: 'watcher',
        was,
        from: previousStatus,
        to: currentStatus,
        bug: 2108,
      })
      if (currentStatus === 'authenticated') {
        if (!shouldRunAuthenticatedAction(currentStatus, previousStatus, was)) {
          return
        }
        logger.debug('launching authenticated action', {
          source: 'watcher',
          bug: 2108,
        })
        authenticatedAction?.()
      } else if (currentStatus === 'unauthenticated') {
        redirectToLogin(unauthenticatedRedirect)
      } else if (currentStatus === 'loading') {
        logger.debug('loading', { source: 'watcher', bug: 2108 })
      }
    })

    // See Bug 2046. `authenticatedAction` may run twice if the status transitions quickly.
    // Keep the mounted path unconditional; the watcher is responsible for filtering the
    // transient authenticated -> loading -> authenticated revalidation case.
    onMounted(() => {
      if (status.value === 'authenticated') {
        logger.debug('launching authenticated action', { source: 'onMounted', bug: 2108 })
        authenticatedAction?.()
      } else if (status.value === 'unauthenticated') {
        redirectToLogin(unauthenticatedRedirect)
      } else if (status.value === 'loading') {
        logger.debug('loading', { source: 'onMounted', bug: 2108 })
      }
    })
  }

  /**
   * Allows unauthenticated users to remain on the page and sends
   * authenticated users to the browse page (main app functionality).
   *
   * Clears the persisted current list when the user is confirmed unauthenticated,
   * so that a subsequent login as a different user fetches fresh list data and
   * entitlements rather than reusing the previous session's cached list.
   */
  const requireGuestUser = (redirect?: string) => {
    watch(status, (currentStatus) => {
      if (currentStatus === 'authenticated') {
        redirectAsDesiredOrToBrowse(redirect)
      } else if (currentStatus === 'unauthenticated') {
        //TODO: ?? userPreferences.clearCurrentList()
      }
    })

    onMounted(() => {
      if (status.value === 'authenticated') {
        redirectAsDesiredOrToBrowse(redirect)
      } else if (status.value === 'unauthenticated') {
        //TODO: ?? userPreferences.clearCurrentList()
      }
    })
  }

  return {
    requireAuthenticatedUser,
    requireGuestUser,
  }
}
