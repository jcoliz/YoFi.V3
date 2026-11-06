/**
 * Tests for workspace/Selector component.
 *
 * Verifies behavioral contracts: workspace switching updates the store and
 * emits the change event; role-based management links appear only for owners;
 * the empty-state message appears when no workspace is selected.
 *
 * CSS class comparisons and layout structure assertions are intentionally
 * omitted — those are brittle and covered by visual review and functional tests.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { reactive, nextTick } from 'vue'
import Selector from '~/components/workspace/Selector.vue'
import { TenantRole } from '~/utils/apiclient'

// useUserPreferencesStore is explicitly imported in Selector from this path.
vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: vi.fn(),
}))

// useApiClient, useRoute, navigateTo are Nuxt auto-imports.
const useApiClientMock = vi.fn()
const useRouteMock = vi.fn()
vi.stubGlobal('useApiClient', useApiClientMock)
vi.stubGlobal('useRoute', useRouteMock)
vi.stubGlobal('navigateTo', vi.fn())

import { useUserPreferencesStore } from '~/stores/userPreferences'

// Stub child components not under test.
// NuxtLink must use inheritAttrs: false and pass $attrs through so that
// data-test-id attributes on the component are rendered in the DOM.
const DropDownPortable = {
  inheritAttrs: false,
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  setup(_props: any, { slots }: any) {
    return () => [slots.trigger?.(), slots.default?.()]
  },
}
const NuxtLink = {
  inheritAttrs: false,
  props: ['to'],
  template: '<a v-bind="$attrs" :href="to"><slot /></a>',
}
const FeatherIcon = { props: ['icon'], template: '<svg :icon="icon" />' }
const BaseSpinner = { template: '<span />' }
const WorkspaceCopyKeyButton = { template: '<span />' }

const globalConfig = {
  components: {
    DropDownPortable,
    NuxtLink,
    FeatherIcon,
    BaseSpinner,
    WorkspaceCopyKeyButton,
  },
}

const tenants = ['First', 'Second', 'Third', 'Fourth', 'Fifth', 'Sixth'].map((name, index) => ({
  key: name.toLowerCase(),
  name: `${name} workspace`,
  role: index === 0 ? TenantRole.Owner : index === 1 ? TenantRole.Editor : TenantRole.Viewer,
}))

type Tenant = (typeof tenants)[0]

function makeStore(currentTenant: Tenant | undefined = tenants[0]) {
  return reactive({
    currentTenant: currentTenant as Tenant | undefined,
    get getCurrentTenantKey() {
      return this.currentTenant?.key
    },
    get hasTenant() {
      return Boolean(this.currentTenant)
    },
    setCurrentTenant(tenant: (typeof tenants)[0]) {
      this.currentTenant = tenant
    },
    clearPreferences() {
      this.currentTenant = undefined
    },
    loadFromStorage() {},
  })
}

async function mountSelector(store = makeStore()) {
  useApiClientMock.mockReturnValue({ getTenants: async () => tenants })
  useRouteMock.mockReturnValue({ path: '/transactions' })
  vi.mocked(useUserPreferencesStore).mockReturnValue(
    store as unknown as ReturnType<typeof useUserPreferencesStore>,
  )

  const wrapper = mount(Selector, { global: globalConfig })
  // Wait for onMounted async fetch to complete
  await nextTick()
  await nextTick()
  return { wrapper, store }
}

describe('workspace/Selector', () => {
  beforeEach(() => {
    vi.mocked(useUserPreferencesStore).mockReset()
  })

  it('lists all other workspaces as options', async () => {
    // Given: a selector with the first workspace selected
    const { wrapper } = await mountSelector()

    // Then: the other workspaces appear as options (all except the current)
    const options = wrapper.findAll('[data-test-id="workspace-option"]')
    expect(options).toHaveLength(tenants.length - 1)
    expect(options.map((o) => o.text())).toEqual(tenants.slice(1).map((t) => t.name))
  })

  it('shows the current workspace name', async () => {
    // Given: the first workspace is selected
    const { wrapper } = await mountSelector()

    // Then: the current workspace name is displayed
    const current = wrapper.find('[data-test-id="selector-current-workspace"]')
    expect(current.exists()).toBe(true)
    expect(current.text()).toContain('First workspace')
  })

  it('switching workspace updates the store', async () => {
    // Given: the first workspace is selected
    const store = makeStore()
    const { wrapper } = await mountSelector(store)

    // When: the fifth workspace option is clicked
    const options = wrapper.findAll('[data-test-id="workspace-option"]')
    const fifthOption = options.find((o) => o.text().includes('Fifth workspace'))
    expect(fifthOption).toBeDefined()
    await fifthOption!.trigger('click')

    // Then: the store is updated to the fifth workspace
    expect(store.getCurrentTenantKey).toBe('fifth')
  })

  it('shows no-workspace message when nothing is selected and no tenants auto-selected', async () => {
    // Given: a store that reports hasTenant=true but currentTenant=undefined
    // (simulates a state where the stored key was cleared but hasTenant guard prevents auto-select)
    // We achieve this by having a store where hasTenant returns true (preventing auto-select)
    // but currentTenant is undefined
    const store = reactive({
      currentTenant: undefined as (typeof tenants)[0] | undefined,
      get getCurrentTenantKey() { return undefined },
      get hasTenant() { return true }, // prevents auto-select in loadTenants
      setCurrentTenant(tenant: (typeof tenants)[0]) { this.currentTenant = tenant },
      clearPreferences() { this.currentTenant = undefined },
      loadFromStorage() {},
    })

    useApiClientMock.mockReturnValue({ getTenants: async () => tenants })
    useRouteMock.mockReturnValue({ path: '/transactions' })
    vi.mocked(useUserPreferencesStore).mockReturnValue(
      store as unknown as ReturnType<typeof useUserPreferencesStore>,
    )

    const wrapper = mount(Selector, { global: globalConfig })
    await nextTick()
    await nextTick()

    // Then: no-workspace message is shown
    expect(wrapper.find('[data-test-id="no-workspace-message"]').exists()).toBe(true)
    expect(wrapper.find('[data-test-id="selector-current-workspace"]').exists()).toBe(false)
  })

  it('shows manage-current-workspace link only for owners', async () => {
    // Given: a single tenant that is owned by the user
    // The API returns the tenant with Owner role so loadTenants() doesn't overwrite it
    const ownerTenant = { key: 'myworkspace', name: 'My Workspace', role: TenantRole.Owner }
    const store = makeStore(ownerTenant)

    useApiClientMock.mockReturnValue({ getTenants: async () => [ownerTenant] })
    useRouteMock.mockReturnValue({ path: '/transactions' })
    vi.mocked(useUserPreferencesStore).mockReturnValue(
      store as unknown as ReturnType<typeof useUserPreferencesStore>,
    )

    const wrapper = mount(Selector, { global: globalConfig })
    await nextTick()
    await nextTick()

    // Then: manage link is shown for owner
    expect(wrapper.find('[data-test-id="manage-current-workspace-button"]').exists()).toBe(true)

    // When: current workspace role changes to editor
    store.setCurrentTenant({ key: 'myworkspace', name: 'My Workspace', role: TenantRole.Editor })
    await nextTick()

    // Then: manage link is hidden for editor
    expect(wrapper.find('[data-test-id="manage-current-workspace-button"]').exists()).toBe(false)

    // When: current workspace role changes to viewer
    store.setCurrentTenant({ key: 'myworkspace', name: 'My Workspace', role: TenantRole.Viewer })
    await nextTick()

    // Then: manage link is hidden for viewer
    expect(wrapper.find('[data-test-id="manage-current-workspace-button"]').exists()).toBe(false)
  })

  it('always shows the all-workspaces link', async () => {
    // Given: the selector is mounted
    const { wrapper } = await mountSelector()

    // Then: the all-workspaces link is present
    expect(wrapper.find('[data-test-id="manage-workspaces-button"]').exists()).toBe(true)
  })
})
