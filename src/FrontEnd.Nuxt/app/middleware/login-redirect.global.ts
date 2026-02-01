/**
 * Redirects authenticated users away from the login page.
 *
 * Unlike `redirectIfAuthenticated` (which only supports static paths), this middleware
 * preserves the `?redirect=` query parameter for deep-link flows. When nuxt-auth sends
 * unauthenticated users to `/login?redirect=/original-page`, this middleware ensures
 * they return to their original destination after login—not a hardcoded fallback.
 */
export default defineNuxtRouteMiddleware((to) => {
  const { status } = useAuth()

  // Only handle the login page
  if (to.path === '/login' && status.value === 'authenticated') {
    // Redirect authenticated users, respecting the redirect query param
    const redirectTo = (to.query.redirect as string) || '/profile'
    return navigateTo(redirectTo)
  }
})
