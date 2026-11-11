import { afterEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { reactive, ref } from 'vue'
import LoginState from '~/components/LoginState.vue'

enableAutoUnmount(afterEach)

const route = reactive({ path: '/profile' })
const status = ref('authenticated')
vi.stubGlobal('useRoute', () => route)
vi.stubGlobal('useAuth', () => ({
  data: ref({ name: 'User' }),
  status,
  signOut: vi.fn(),
}))
vi.stubGlobal('useUserPreferencesStore', () => ({ clearPreferences: vi.fn() }))
vi.stubGlobal('useTransactionSearchStore', () => ({ reset: vi.fn() }))

function mountLoginState() {
  return mount(LoginState, {
    global: {
      components: {
        DropDownPortable: { template: '<div><slot name="trigger" /><slot /></div>' },
        FeatherIcon: { template: '<span />' },
        NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
      },
    },
  })
}

describe('Login state profile navigation', () => {
  it('highlights Profile on profile and token pages, but not unrelated pages', async () => {
    const wrapper = mountLoginState()
    const profile = wrapper.get('[data-test-id="Profile"]')

    for (const path of ['/profile', '/identity/tokens', '/identity/tokens/']) {
      route.path = path
      await wrapper.vm.$nextTick()
      expect(profile.classes(), path).toContain('active')
      expect(profile.attributes('href')).toBe('/profile')
    }

    for (const path of ['/workspaces/example', '/identity/tokens-other', '/transactions']) {
      route.path = path
      await wrapper.vm.$nextTick()
      expect(profile.classes(), path).not.toContain('active')
    }
  })
})
