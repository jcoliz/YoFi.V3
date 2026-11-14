import type { RouteLocationNormalizedLoaded } from 'vue-router'

/**
 * Determines whether a navigation section contains the current page.
 *
 * Matches the section's own path, nested paths (except beneath `/`), or any
 * breadcrumb destination. Breadcrumbs connect pages outside the section's URL
 * hierarchy, such as `/identity/tokens` belonging to `/profile`, without
 * page-specific checks or separate navigation metadata.
 *
 * Trailing slashes are ignored; breadcrumb titles do not affect matching.
 *
 * @param route - Current reactive route, including its breadcrumb metadata.
 * @param target - Section link's route path.
 * @returns Whether the section link should be highlighted.
 */
export function isNavigationActive(
  route: Pick<RouteLocationNormalizedLoaded, 'path' | 'meta'>,
  target: string,
): boolean {
  const section = target.replace(/\/+$/, '') || '/'
  const path = route.path.replace(/\/+$/, '') || '/'

  return (
    path === section ||
    (section !== '/' && path.startsWith(`${section}/`)) ||
    (route.meta.breadcrumbs?.some((item) => (item.to.replace(/\/+$/, '') || '/') === section) ??
      false)
  )
}
