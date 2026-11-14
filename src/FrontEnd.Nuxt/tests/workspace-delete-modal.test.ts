/**
 * Tests for workspace/DeleteModal component.
 *
 * Verifies the name-confirmation guard before deletion and the data-deletion
 * flow. Tests behavioral contracts: given a workspace prop and user input,
 * the correct API calls are made and events are emitted.
 *
 * ModalDialog is stubbed with a simple inline version because the real
 * ModalDialog uses <Teleport to="body">, which renders outside the wrapper
 * and makes element queries unreliable in the test environment.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { h } from 'vue'
import DeleteModal from '~/components/workspace/DeleteModal.vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'
import type { TenantRoleResultDto } from '~/utils/apiclient'

// useUserPreferencesStore is explicitly imported in DeleteModal from this path.
// Mock the module so Pinia is never initialized.
vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: vi.fn(),
}))

// useApiClient and handleApiError are Nuxt auto-imports (no explicit import).
// Stub them as globals.
const useApiClientMock = vi.fn()
vi.stubGlobal('useApiClient', useApiClientMock)
vi.stubGlobal('handleApiError', (err: unknown) => err)

import { useUserPreferencesStore } from '~/stores/userPreferences'

// Stub child components not under test
const FeatherIcon = { template: '<i />' }
const CopyTextButton = { template: '<span />' }
const BaseSpinner = { template: '<span />' }

/**
 * Stub for ModalDialog that renders slots inline (no Teleport).
 * Exposes the primary button with the test-id from props so tests can click it.
 */
const ModalDialogStub = {
  props: [
    'show',
    'testId',
    'primaryButtonTestId',
    'secondaryButtonTestId',
    'primaryButtonDisabled',
    'loading',
  ],
  emits: ['primary', 'update:show'],
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  setup(props: any, { slots, emit }: any) {
    return () =>
      props.show
        ? h('div', { 'data-test-id': props.testId }, [
            slots.default?.(),
            h('button', {
              'data-test-id': props.primaryButtonTestId,
              disabled: props.loading || props.primaryButtonDisabled ? true : undefined,
              onClick: () => emit('primary'),
            }),
            h('button', {
              'data-test-id': props.secondaryButtonTestId,
              onClick: () => emit('update:show', false),
            }),
          ])
        : null
  },
}

const workspace = { key: 'second', name: 'Second Workspace' } as unknown as TenantRoleResultDto

/** Shared global config for all DeleteModal mounts */
const globalConfig = {
  components: {
    ModalDialog: ModalDialogStub,
    ErrorDisplay,
    CopyTextButton,
    FeatherIcon,
    BaseSpinner,
  },
}

// ---------------------------------------------------------------------------
// Workspace deletion (scope = 'workspace')
// ---------------------------------------------------------------------------

describe('DeleteModal – workspace scope', () => {
  let deleteTenant: ReturnType<typeof vi.fn>
  let store: { getCurrentTenantKey: string; cleared: boolean; clearPreferences: () => void }

  beforeEach(() => {
    deleteTenant = vi.fn().mockResolvedValue(undefined)
    store = {
      getCurrentTenantKey: 'second',
      cleared: false,
      clearPreferences() {
        this.cleared = true
      },
    }
    useApiClientMock.mockReturnValue({ deleteTenant })
    vi.mocked(useUserPreferencesStore).mockReturnValue(
      store as unknown as ReturnType<typeof useUserPreferencesStore>,
    )
  })

  it('submit button is disabled until the exact workspace name is typed', async () => {
    // Given: the delete modal is shown
    const wrapper = mount(DeleteModal, {
      props: { show: true, workspace },
      global: globalConfig,
    })

    // Then: submit is disabled initially
    expect(
      wrapper.find('[data-test-id="delete-submit-button"]').attributes('disabled'),
    ).toBeDefined()

    // When: wrong names are typed
    for (const wrong of ['second workspace', 'Second Workspace ', 'Second']) {
      await wrapper.find('[data-test-id="confirmation-name-input"]').setValue(wrong)
      expect(
        wrapper.find('[data-test-id="delete-submit-button"]').attributes('disabled'),
        `Expected disabled for "${wrong}"`,
      ).toBeDefined()
    }

    // When: exact name is typed
    await wrapper.find('[data-test-id="confirmation-name-input"]').setValue('Second Workspace')

    // Then: submit is enabled
    expect(
      wrapper.find('[data-test-id="delete-submit-button"]').attributes('disabled'),
    ).toBeUndefined()
  })

  it('deletes the workspace and clears preferences when confirmed', async () => {
    // Given: the delete modal is shown with the correct name typed
    const wrapper = mount(DeleteModal, {
      props: { show: true, workspace },
      global: globalConfig,
    })
    await wrapper.find('[data-test-id="confirmation-name-input"]').setValue('Second Workspace')

    // When: submit is clicked
    await wrapper.find('[data-test-id="delete-submit-button"]').trigger('click')
    await wrapper.vm.$nextTick()

    // Then: deleteTenant was called with the workspace key
    expect(deleteTenant).toHaveBeenCalledWith('second')

    // And: preferences were cleared (current workspace was deleted)
    expect(store.cleared).toBe(true)

    // And: update:show false and deleted events were emitted
    expect(wrapper.emitted('update:show')).toEqual([[false]])
    expect(wrapper.emitted('deleted')).toBeTruthy()
  })

  it('does not call the API when the name does not match', async () => {
    // Given: the delete modal is shown with a wrong name typed
    const wrapper = mount(DeleteModal, {
      props: { show: true, workspace },
      global: globalConfig,
    })
    await wrapper.find('[data-test-id="confirmation-name-input"]').setValue('wrong name')

    // When: submit is clicked (button is disabled, handler should not fire)
    await wrapper.find('[data-test-id="delete-submit-button"]').trigger('click')
    await wrapper.vm.$nextTick()

    // Then: API was not called
    expect(deleteTenant).not.toHaveBeenCalled()
  })
})

// ---------------------------------------------------------------------------
// Data deletion (scope = 'data')
// ---------------------------------------------------------------------------

describe('DeleteModal – data scope', () => {
  let deleteAllData: ReturnType<typeof vi.fn>

  beforeEach(() => {
    deleteAllData = vi.fn().mockResolvedValue({ deleted: { transactions: 5 } })
    useApiClientMock.mockReturnValue({ deleteAllData })
    vi.mocked(useUserPreferencesStore).mockReturnValue({
      getCurrentTenantKey: 'second',
      clearPreferences: vi.fn(),
    } as unknown as ReturnType<typeof useUserPreferencesStore>)
  })

  it('shows the data-deletion modal (not the workspace-deletion modal)', () => {
    // Given: scope is 'data'
    const wrapper = mount(DeleteModal, {
      props: { show: true, workspace, scope: 'data' },
      global: globalConfig,
    })

    // Then: the data modal test-id is present
    expect(wrapper.find('[data-test-id="delete-data-modal"]').exists()).toBe(true)
    expect(wrapper.find('[data-test-id="delete-modal"]').exists()).toBe(false)
  })

  it('sends the typed confirmation name and emits the deletion result', async () => {
    // Given: the data-deletion modal is shown with the correct name typed
    const wrapper = mount(DeleteModal, {
      props: { show: true, workspace, scope: 'data' },
      global: globalConfig,
    })
    await wrapper.find('[data-test-id="confirmation-name-input"]').setValue('Second Workspace')

    // When: submit is clicked
    await wrapper.find('[data-test-id="delete-submit-button"]').trigger('click')
    await wrapper.vm.$nextTick()

    // Then: deleteAllData was called with the workspace key and confirmation name
    expect(deleteAllData).toHaveBeenCalledWith(
      'second',
      expect.objectContaining({ confirmationWorkspaceName: 'Second Workspace' }),
    )

    // And: update:show false and deleted events were emitted with the result
    expect(wrapper.emitted('update:show')).toEqual([[false]])
    const deletedEvents = wrapper.emitted('deleted') as unknown[][]
    expect(deletedEvents[0][0]).toEqual({ deleted: { transactions: 5 } })
  })

  it('does not clear preferences after data deletion (workspace still exists)', async () => {
    // Given: the data-deletion modal is shown
    const clearPreferences = vi.fn()
    vi.mocked(useUserPreferencesStore).mockReturnValue({
      getCurrentTenantKey: 'second',
      clearPreferences,
    } as unknown as ReturnType<typeof useUserPreferencesStore>)
    const wrapper = mount(DeleteModal, {
      props: { show: true, workspace, scope: 'data' },
      global: globalConfig,
    })
    await wrapper.find('[data-test-id="confirmation-name-input"]').setValue('Second Workspace')

    // When: submit is clicked
    await wrapper.find('[data-test-id="delete-submit-button"]').trigger('click')
    await wrapper.vm.$nextTick()

    // Then: preferences were NOT cleared (workspace still exists)
    expect(clearPreferences).not.toHaveBeenCalled()
  })
})
