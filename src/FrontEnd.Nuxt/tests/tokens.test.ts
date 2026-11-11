import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { ref } from 'vue'
import InfoCard from '~/components/InfoCard.vue'
import TokensPage from '~/pages/identity/tokens.vue'
import ProfilePage from '~/pages/profile.vue'

enableAutoUnmount(afterEach)

const accessToken = ref<string | null>(null)
const refreshToken = ref<string | null>(null)
const status = ref('authenticated')
const refresh = vi.fn()
const clearError = vi.fn()
const setError = vi.fn()
const handleApiError = vi.fn()
const requireAuthenticatedUser = vi.fn()

vi.stubGlobal('definePageMeta', vi.fn())
vi.stubGlobal('useAuth', () => ({
  token: accessToken,
  refreshToken,
  status,
  refresh,
  data: ref({ email: 'user@example.com', name: 'User' }),
  signOut: vi.fn(),
}))
vi.stubGlobal('useAuthenticationGuard', () => ({ requireAuthenticatedUser }))
vi.stubGlobal('useProblemDetails', () => ({ clearError, setError, handleApiError }))

function jwt(payload: Record<string, unknown>, header = { alg: 'HS256' }) {
  const encode = (value: object) =>
    btoa(JSON.stringify(value)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_')
  return `${encode(header)}.${encode(payload)}.signature`
}

function mountTokens() {
  return mount(TokensPage, {
    global: {
      components: {
        InfoCard,
        ProblemDetailsViewer: { template: '<div />' },
      },
    },
  })
}

beforeEach(() => {
  accessToken.value = null
  refreshToken.value = null
  status.value = 'authenticated'
  vi.clearAllMocks()
  refresh.mockResolvedValue(undefined)
  handleApiError.mockResolvedValue(undefined)
})

describe('Token inspection', () => {
  it('displays decoded token information, claims, and the token without its Bearer prefix', () => {
    const raw = jwt({
      sub: 'user-123',
      exp: Math.floor(Date.now() / 1000) + 3600,
      iat: 1700000000,
      role: ['admin', 'user'],
      custom: { nested: true },
    })
    accessToken.value = `Bearer ${raw}`
    refreshToken.value = 'refresh-value'
    const wrapper = mountTokens()

    expect(requireAuthenticatedUser).toHaveBeenCalledOnce()
    expect(wrapper.text()).toContain('Not expired')
    expect(wrapper.text()).toContain('HS256')
    expect(wrapper.text()).toContain('Subject - who the token is about')
    expect(wrapper.text()).toContain('user-123')
    expect(wrapper.text()).toContain('admin')
    expect(wrapper.text()).toContain('"nested": true')
    expect(wrapper.text()).toContain('signature is not verified')
    expect(
      (wrapper.get('textarea[aria-label="Raw JWT token"]').element as HTMLTextAreaElement).value,
    ).toBe(raw)
    expect(
      (wrapper.get('textarea[aria-label="Refresh token"]').element as HTMLTextAreaElement).value,
    ).toBe('refresh-value')
  })

  it('shows missing, malformed, expired, and no-expiration states and updates reactively', async () => {
    const wrapper = mountTokens()
    expect(wrapper.text()).toContain('No JWT token found')
    expect(wrapper.text()).toContain('No refresh token available')

    accessToken.value = 'malformed'
    await wrapper.vm.$nextTick()
    expect(wrapper.text()).toContain('Failed to decode JWT token')
    expect(wrapper.find('textarea[aria-label="Raw JWT token"]').exists()).toBe(false)

    accessToken.value = jwt({ exp: 1 })
    await wrapper.vm.$nextTick()
    expect(wrapper.text()).toContain('Expired')

    accessToken.value = jwt({ sub: 'user' })
    await wrapper.vm.$nextTick()
    expect(wrapper.text()).toContain('No expiration')
  })

  it('copies both tokens and reports clipboard failures without exposing token values', async () => {
    const raw = jwt({ sub: 'user' })
    accessToken.value = `Bearer ${raw}`
    refreshToken.value = 'refresh-value'
    const writeText = vi.fn().mockResolvedValue(undefined)
    const originalNavigator = navigator
    vi.stubGlobal('navigator', { clipboard: { writeText } })
    const wrapper = mountTokens()

    await wrapper.get('button[title="Copy token"]').trigger('click')
    await wrapper.get('button[title="Copy refresh token"]').trigger('click')
    expect(writeText).toHaveBeenNthCalledWith(1, raw)
    expect(writeText).toHaveBeenNthCalledWith(2, 'refresh-value')

    writeText.mockRejectedValueOnce(new Error('secret'))
    await wrapper.get('button[title="Copy token"]').trigger('click')
    await flushPromises()
    expect(setError).toHaveBeenCalledWith({
      title: 'Copy failed',
      detail: 'Could not copy the token to the clipboard.',
    })
    vi.stubGlobal('navigator', originalNavigator)
  })

  it('shows refresh progress and reports refresh failures', async () => {
    let rejectRefresh!: (reason: Error) => void
    refresh.mockReturnValueOnce(
      new Promise((_resolve, reject) => {
        rejectRefresh = reject
      }),
    )
    const wrapper = mountTokens()
    const button = wrapper.get('button').element as HTMLButtonElement
    await wrapper.get('button').trigger('click')
    expect(button.disabled).toBe(true)
    expect(wrapper.text()).toContain('Refreshing...')
    const error = new Error('refresh failed')
    rejectRefresh(error)
    await flushPromises()
    expect(handleApiError).toHaveBeenCalledWith(
      error,
      'Refresh Token Failed',
      'Failed to refresh token',
    )
    expect(button.disabled).toBe(false)
  })

  it('updates the displayed tokens after a successful refresh and respects auth loading', async () => {
    accessToken.value = jwt({ sub: 'before' })
    refreshToken.value = 'old-refresh'
    const newAccessToken = jwt({ sub: 'after' })
    refresh.mockImplementationOnce(async () => {
      accessToken.value = `Bearer ${newAccessToken}`
      refreshToken.value = 'new-refresh'
    })
    const wrapper = mountTokens()
    const refreshButton = wrapper.get('[data-test-id="refresh-tokens"]')
    const button = refreshButton.element as HTMLButtonElement

    status.value = 'loading'
    await wrapper.vm.$nextTick()
    expect(button.disabled).toBe(true)
    status.value = 'authenticated'
    await wrapper.vm.$nextTick()
    await refreshButton.trigger('click')
    await flushPromises()

    expect(refresh).toHaveBeenCalledOnce()
    expect(wrapper.text()).toContain('after')
    expect(
      (wrapper.get('textarea[aria-label="Raw JWT token"]').element as HTMLTextAreaElement).value,
    ).toBe(newAccessToken)
    expect(
      (wrapper.get('textarea[aria-label="Refresh token"]').element as HTMLTextAreaElement).value,
    ).toBe('new-refresh')
    expect(handleApiError).not.toHaveBeenCalled()
  })

  it('links to token inspection without displaying tokens or refresh controls on the profile', () => {
    accessToken.value = 'access-token-value'
    refreshToken.value = 'refresh-token-value'
    const wrapper = mount(ProfilePage, {
      global: {
        components: {
          InfoCard,
          NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
        },
      },
    })
    expect(wrapper.get('[data-test-id="InspectTokens"]').attributes('href')).toBe(
      '/identity/tokens',
    )
    expect(wrapper.text()).toContain('user@example.com')
    expect(wrapper.text()).not.toContain('access-token-value')
    expect(wrapper.text()).not.toContain('refresh-token-value')
    expect(wrapper.text()).not.toContain('Refresh Token')
    expect(wrapper.find('[data-test-id="refresh-token"]').exists()).toBe(false)
  })
})
