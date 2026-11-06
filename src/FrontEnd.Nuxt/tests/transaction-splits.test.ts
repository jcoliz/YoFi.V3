/**
 * Tests for split transaction behavior in the quick-edit modal.
 *
 * Verifies that transactions with multiple splits protect their category
 * field (disabled input, informational help text) while still allowing
 * payee and memo edits. Also verifies that the category is omitted from
 * the API call when splits are present.
 *
 * Page-level split display (transactions/index.vue, transactions/[key].vue)
 * is covered by functional tests — see tests/Functional/ — because those
 * pages depend on deeply Nuxt-specific composables that cannot be meaningfully
 * mocked in a unit test environment.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { h } from 'vue'
import EditModal from '~/components/transaction/EditModal.vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'

// useUserPreferencesStore is explicitly imported in EditModal from this path.
vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: vi.fn(),
}))

// useApiClient, handleApiError, navigateTo are Nuxt auto-imports.
const useApiClientMock = vi.fn()
vi.stubGlobal('useApiClient', useApiClientMock)
vi.stubGlobal('handleApiError', (err: unknown, title: string, detail: string) => ({ title, detail }))
vi.stubGlobal('navigateTo', vi.fn())

import { useUserPreferencesStore } from '~/stores/userPreferences'

// Stub child components not under test
const FeatherIcon = { template: '<i />' }
const BaseSpinner = { template: '<span />' }

/** Stub for ModalDialog — renders slots inline, no Teleport */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const ModalDialogStub = {
  props: ['show', 'testId', 'primaryButtonTestId', 'secondaryButtonTestId', 'loading'],
  emits: ['primary', 'update:show'],
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  setup(props: any, { slots, emit }: any) {
    return () =>
      props.show
        ? h('div', { 'data-test-id': props.testId }, [
            slots.default?.(),
            slots.footer?.(),
            h('button', {
              'data-test-id': props.primaryButtonTestId,
              disabled: props.loading ? true : undefined,
              onClick: () => emit('primary'),
            }),
          ])
        : null
  },
}

const globalConfig = {
  components: { ModalDialog: ModalDialogStub, ErrorDisplay, FeatherIcon, BaseSpinner },
}

describe('transaction/EditModal – split category protection', () => {
  let quickEditTransaction: ReturnType<typeof vi.fn>

  beforeEach(() => {
    quickEditTransaction = vi.fn().mockResolvedValue(undefined)
    useApiClientMock.mockReturnValue({ quickEditTransaction })
    vi.mocked(useUserPreferencesStore).mockReturnValue({
      getCurrentTenantKey: 'workspace',
    } as ReturnType<typeof useUserPreferencesStore>)
  })

  it('disables the category field when the transaction has multiple splits', () => {
    // Given: a transaction with multiple splits
    const wrapper = mount(EditModal, {
      props: {
        show: true,
        transaction: { key: 'tx', payee: 'Store', category: 'Food', hasMultipleSplits: true },
      },
      global: globalConfig,
    })

    // Then: category input is disabled
    const categoryInput = wrapper.find('[data-test-id="edit-transaction-category"]')
    expect(categoryInput.attributes('disabled')).toBeDefined()
  })

  it('enables the category field when the transaction has no splits', () => {
    // Given: a transaction without splits
    const wrapper = mount(EditModal, {
      props: {
        show: true,
        transaction: { key: 'tx', payee: 'Store', category: 'Food', hasMultipleSplits: false },
      },
      global: globalConfig,
    })

    // Then: category input is enabled
    const categoryInput = wrapper.find('[data-test-id="edit-transaction-category"]')
    expect(categoryInput.attributes('disabled')).toBeUndefined()
  })

  it('shows split help text when the transaction has multiple splits', () => {
    // Given: a transaction with multiple splits
    const wrapper = mount(EditModal, {
      props: {
        show: true,
        transaction: { key: 'tx', payee: 'Store', hasMultipleSplits: true },
      },
      global: globalConfig,
    })

    // Then: help text mentions splits
    const help = wrapper.find('[data-test-id="edit-transaction-category-help"]')
    expect(help.text()).toContain('split across multiple categories')
  })

  it('shows normal help text when the transaction has no splits', () => {
    // Given: a transaction without splits
    const wrapper = mount(EditModal, {
      props: {
        show: true,
        transaction: { key: 'tx', payee: 'Store', hasMultipleSplits: false },
      },
      global: globalConfig,
    })

    // Then: help text is the normal optional category message
    const help = wrapper.find('[data-test-id="edit-transaction-category-help"]')
    expect(help.text()).toContain('Optional category')
  })

  it('omits category from the API call when the transaction has multiple splits', async () => {
    // Given: a transaction with multiple splits
    const wrapper = mount(EditModal, {
      props: {
        show: true,
        transaction: { key: 'tx', payee: 'Store', category: 'Food', hasMultipleSplits: true },
      },
      global: globalConfig,
    })

    // When: payee is updated and form is submitted
    await wrapper.find('[data-test-id="edit-transaction-payee"]').setValue('Updated Store')
    await wrapper.find('[data-test-id="edit-submit-button"]').trigger('click')
    await wrapper.vm.$nextTick()

    // Then: API was called without a category
    expect(quickEditTransaction).toHaveBeenCalledOnce()
    const [, , dto] = quickEditTransaction.mock.calls[0]
    expect(dto.payee).toBe('Updated Store')
    expect(dto.category).toBeUndefined()
  })

  it('includes category in the API call when the transaction has no splits', async () => {
    // Given: a transaction without splits
    const wrapper = mount(EditModal, {
      props: {
        show: true,
        transaction: { key: 'tx', payee: 'Store', category: 'Food', hasMultipleSplits: false },
      },
      global: globalConfig,
    })

    // When: category is updated and form is submitted
    await wrapper.find('[data-test-id="edit-transaction-category"]').setValue('Groceries')
    await wrapper.find('[data-test-id="edit-submit-button"]').trigger('click')
    await wrapper.vm.$nextTick()

    // Then: API was called with the updated category
    expect(quickEditTransaction).toHaveBeenCalledOnce()
    const [, , dto] = quickEditTransaction.mock.calls[0]
    expect(dto.category).toBe('Groceries')
  })
})
