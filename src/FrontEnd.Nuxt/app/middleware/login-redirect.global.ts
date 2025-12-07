export default defineNuxtRouteMiddleware((to) => {
  const { status } = useAuth()

  // Only handle the login page
  if (to.path === '/login' && status.value === 'authenticated') {
    // Redirect authenticated users, respecting the redirect query param
    const redirectTo = (to.query.redirect as string) || '/profile'
    return navigateTo(redirectTo)
  }
})
