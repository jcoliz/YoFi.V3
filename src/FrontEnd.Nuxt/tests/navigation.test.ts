import { describe, expect, it } from 'vitest'
import type { RouteMeta } from 'vue-router'
import { isNavigationActive } from '~/utils/navigation'

describe('navigation section matching', () => {
  it.each([
    ['/transactions', '/transactions', true],
    ['/transactions/example', '/transactions', true],
    ['/workspaces/example', '/workspaces', true],
    ['/profile/settings', '/profile', true],
    ['/transactions-other', '/transactions', false],
    ['/workspaces-other/example', '/workspaces', false],
    ['/reports', '/transactions', false],
    ['/', '/', true],
    ['/transactions', '/', false],
    ['/transactions/', '/transactions', true],
    ['/transactions/example', '/transactions/', true],
  ])('matches %s against %s: %s', (path, target, expected) => {
    expect(isNavigationActive({ path, meta: {} }, target)).toBe(expected)
  })

  it.each([
    ['/transactions/example', '/transactions', 'Transactions'],
    ['/workspaces/example', '/workspaces', 'Workspaces'],
    ['/identity/tokens', '/profile', 'Profile'],
    ['/new-section/details', '/existing-section', 'Existing Section'],
  ])('recognizes the breadcrumb parent of %s', (path, target, title) => {
    const meta: RouteMeta = { breadcrumbs: [{ title, to: target }] }

    expect(isNavigationActive({ path, meta }, target)).toBe(true)
    expect(isNavigationActive({ path, meta }, '/unrelated')).toBe(false)
  })

  it('recognizes every breadcrumb ancestor, not just the immediate parent', () => {
    const route = {
      path: '/details/example',
      meta: {
        breadcrumbs: [
          { title: 'Section', to: '/section' },
          { title: 'Group', to: '/groups/example' },
        ],
      },
    }

    expect(isNavigationActive(route, '/section')).toBe(true)
    expect(isNavigationActive(route, '/groups/example')).toBe(true)
    expect(isNavigationActive(route, '/groups')).toBe(false)
  })

  it('normalizes trailing slashes in breadcrumb destinations', () => {
    const route = {
      path: '/identity/tokens',
      meta: { breadcrumbs: [{ title: 'Profile', to: '/profile/' }] },
    }

    expect(isNavigationActive(route, '/profile')).toBe(true)
  })

  it('does not use breadcrumb titles or partial paths to select a section', () => {
    const route = {
      path: '/details',
      meta: { breadcrumbs: [{ title: 'Profile', to: '/profile-other' }] },
    }

    expect(isNavigationActive(route, '/profile')).toBe(false)
  })

  it('handles an empty breadcrumb list', () => {
    expect(isNavigationActive({ path: '/reports', meta: { breadcrumbs: [] } }, '/profile')).toBe(
      false,
    )
  })
})
