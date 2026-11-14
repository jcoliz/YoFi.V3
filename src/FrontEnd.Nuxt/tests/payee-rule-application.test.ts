import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { onMounted } from 'vue'
import { createPinia, setActivePinia, storeToRefs } from 'pinia'
import PayeeRulesDialog from '~/components/payee-rules/Dialog.vue'
import ModalDialog from '~/components/ModalDialog.vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'
import TransactionsPage from '~/pages/transactions/index.vue'
import {
  PayeeMatchingRulesClient,
  PayeeMatchingRuleCreateDto,
  PayeeMatchingRuleCreateResultDto,
  PayeeMatchingRuleEditDto,
  TenantRole,
  TenantRoleResultDto,
  TransactionResultDto,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { useTransactionSearchStore } from '~/stores/transactionSearch'
import { useToastStore } from '~/stores/toast'

enableAutoUnmount(afterEach)
vi.stubGlobal('definePageMeta', vi.fn())
vi.stubGlobal('storeToRefs', storeToRefs)
vi.stubGlobal('useAuthenticationGuard', () => ({
  requireAuthenticatedUser: (action: () => Promise<void>) => onMounted(action),
}))

const getTransactions = vi.fn()
const quickEditTransaction = vi.fn()
const createRule = vi.fn()
vi.stubGlobal('useApiClient', (Client: unknown) =>
  Client === PayeeMatchingRulesClient ? { createRule } : { getTransactions, quickEditTransaction },
)

const globalConfig = {
  components: {
    ModalDialog,
    ErrorDisplay,
    PayeeRulesDialog,
    FeatherIcon: { template: '<i />' },
    BaseSpinner: { template: '<span />' },
    WorkspaceSelector: { template: '<div />' },
    SearchFilterBar: { template: '<div><slot /></div>' },
    TransactionFilterPanel: { template: '<div />', methods: { resetDraft() {} } },
    PaginationBar: { template: '<div />' },
    TransactionCreateModal: { template: '<div />' },
    TransactionEditModal: { template: '<div />' },
  },
  stubs: { teleport: true },
}

const source = () =>
  new TransactionResultDto({
    key: 'source',
    payee: 'Amazon Marketplace',
    category: '',
    memo: 'Preserve memo',
    amount: -20,
    date: new Date('2026-01-01'),
    hasMultipleSplits: false,
  })

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: Error) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

async function setupPage(transaction = source()) {
  const store = useUserPreferencesStore()
  store.currentTenant = new TenantRoleResultDto({
    key: 'workspace',
    name: 'Workspace',
    role: TenantRole.Editor,
  })
  vi.spyOn(store, 'loadFromStorage').mockImplementation(() => {})
  getTransactions.mockResolvedValue({ items: [transaction] })
  const wrapper = mount(TransactionsPage, { attachTo: document.body, global: globalConfig })
  await flushPromises()
  await wrapper.get('[data-test-id="create-rule-button"]').trigger('click')
  await wrapper.get('[data-test-id="payee-pattern-input"]').setValue('Amazon')
  await wrapper.get('[data-test-id="category-input"]').setValue(' shopping : online ')
  return { wrapper, store }
}

beforeEach(() => {
  setActivePinia(createPinia())
  getTransactions.mockReset()
  quickEditTransaction.mockReset().mockResolvedValue({})
  createRule
    .mockReset()
    .mockResolvedValue(PayeeMatchingRuleCreateResultDto.fromJS({ transactionsCategorized: 3 }))
  vi.spyOn(console, 'error').mockImplementation(() => {})
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('Payee rule dialog application option', () => {
  it.each(['create', 'edit'] as const)(
    'uses native form submission and Escape cancellation in %s mode',
    async (mode) => {
      const onCancel = vi.fn()
      const wrapper = mount(PayeeRulesDialog, {
        attachTo: document.body,
        props: {
          show: true,
          mode,
          initialPayeePattern: 'Amazon',
          initialCategory: 'Shopping',
          onCancel,
        },
        global: globalConfig,
      })
      const button = wrapper.get<HTMLButtonElement>('[data-test-id="save-button"]').element
      expect(button.type).toBe('submit')
      for (const testId of ['payee-pattern-input', 'category-input']) {
        const input = wrapper.get<HTMLInputElement>(`[data-test-id="${testId}"]`).element
        expect(input.form).not.toBeNull()
        expect(input.form).toBe(button.form)
        const event = new KeyboardEvent('keydown', {
          key: 'Enter',
          bubbles: true,
          cancelable: true,
        })
        input.dispatchEvent(event)
        expect(event.defaultPrevented).toBe(false)
      }
      await wrapper.get('#payee-rule-form').trigger('submit')
      expect(wrapper.emitted('save')).toEqual([
        [
          new PayeeMatchingRuleEditDto({
            payeePattern: 'Amazon',
            payeeIsRegex: false,
            category: 'Shopping',
          }),
          false,
        ],
      ])

      await wrapper.setProps({ loading: true })
      await wrapper.get('[data-test-id="category-input"]').trigger('keydown', { key: 'Escape' })
      expect(wrapper.emitted('cancel')).toBeUndefined()
      expect(wrapper.emitted('update:show')).toBeUndefined()

      await wrapper.setProps({ loading: false })
      await wrapper.get('[data-test-id="category-input"]').trigger('keydown', { key: 'Escape' })
      expect(wrapper.emitted('cancel')).toEqual([[]])
      expect(wrapper.emitted('update:show')).toEqual([[false]])
      expect(wrapper.emitted('save')).toHaveLength(1)

      await wrapper.setProps({ show: false })
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
      await wrapper.setProps({ show: true })
      wrapper.unmount()
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
      expect(onCancel).toHaveBeenCalledOnce()
    },
  )

  it.each([
    ['create', false],
    ['edit', false],
    ['edit', true],
  ] as const)(
    'hides the checkbox in %s mode with transaction context %s',
    (mode, fromTransaction) => {
      const wrapper = mount(PayeeRulesDialog, {
        attachTo: document.body,
        props: { show: true, mode, fromTransaction },
        global: globalConfig,
      })
      expect(wrapper.find('[data-test-id="apply-rule-now-checkbox"]').exists()).toBe(false)
    },
  )

  it('defaults ON, emits final edited fields and checkbox value, and resets on reopening', async () => {
    const wrapper = mount(PayeeRulesDialog, {
      attachTo: document.body,
      props: {
        show: true,
        mode: 'create',
        fromTransaction: true,
        initialPayeePattern: 'Amazon Marketplace',
        initialCategory: 'Shopping',
      },
      global: globalConfig,
    })
    const checkbox = wrapper.get<HTMLInputElement>('[data-test-id="apply-rule-now-checkbox"]')
    expect(checkbox.element.checked).toBe(true)
    await checkbox.setValue(false)
    await wrapper.get('[data-test-id="payee-pattern-input"]').setValue(' Amazon ')
    await wrapper.get('[data-test-id="category-input"]').setValue(' Online ')
    await wrapper.get('[data-test-id="regex-checkbox"]').setValue(true)
    await wrapper.get('[data-test-id="save-button"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([
      [
        new PayeeMatchingRuleEditDto({
          payeePattern: 'Amazon',
          payeeIsRegex: true,
          category: 'Online',
        }),
        false,
      ],
    ])
    await wrapper.setProps({ show: false })
    await wrapper.setProps({ show: true })
    expect(
      wrapper.get<HTMLInputElement>('[data-test-id="apply-rule-now-checkbox"]').element.checked,
    ).toBe(true)
  })

  it('ordinary creation still emits rule data without requesting workspace application', async () => {
    const wrapper = mount(PayeeRulesDialog, {
      attachTo: document.body,
      props: {
        show: true,
        mode: 'create',
        initialPayeePattern: 'Amazon',
        initialCategory: 'Shopping',
      },
      global: globalConfig,
    })
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    expect(wrapper.emitted('save')?.[0]?.[1]).toBe(false)
  })

  it('validates input before emitting and disables submission and dismissal while saving', async () => {
    const wrapper = mount(PayeeRulesDialog, {
      attachTo: document.body,
      props: { show: true, mode: 'create', fromTransaction: true },
      global: globalConfig,
    })
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    expect(wrapper.emitted('save')).toBeUndefined()
    expect(wrapper.get('[data-test-id="validation-error"]').text()).toContain(
      'Category is required',
    )

    await wrapper.get('[data-test-id="payee-pattern-input"]').setValue('Amazon')
    await wrapper.get('[data-test-id="category-input"]').setValue('Shopping')
    await wrapper.setProps({ loading: true })
    expect(
      wrapper.get('[data-test-id="apply-rule-now-checkbox"]').attributes('disabled'),
    ).toBeDefined()
    expect(wrapper.get('[data-test-id="save-button"]').attributes('disabled')).toBeDefined()
    await wrapper.get('#payee-rule-form').trigger('submit')
    await wrapper.get('[data-test-id="rule-create-dialog"]').trigger('click')
    expect(wrapper.emitted('save')).toBeUndefined()
    expect(wrapper.emitted('update:show')).toBeUndefined()
  })
})

describe('Create rule from transaction', () => {
  it('awaits source update before creating with the final rule and ON option', async () => {
    const update = deferred<object>()
    quickEditTransaction.mockReturnValue(update.promise)
    const { wrapper } = await setupPage()
    await wrapper.get('[data-test-id="save-button"]').trigger('click')

    expect(quickEditTransaction).toHaveBeenCalledWith(
      'source',
      'workspace',
      expect.objectContaining({
        payee: 'Amazon Marketplace',
        memo: 'Preserve memo',
        category: 'shopping : online',
      }),
    )
    expect(createRule).not.toHaveBeenCalled()
    await wrapper.get('#payee-rule-form').trigger('submit')
    expect(quickEditTransaction).toHaveBeenCalledOnce()

    update.resolve({})
    await flushPromises()
    expect(createRule).toHaveBeenCalledOnce()
    expect(createRule).toHaveBeenCalledWith(
      'workspace',
      PayeeMatchingRuleCreateDto.fromJS({
        payeePattern: 'Amazon',
        payeeIsRegex: false,
        category: 'shopping : online',
        applyToUncategorizedTransactions: true,
      }),
    )
    expect(createRule.mock.calls[0]![1].toJSON()).toEqual({
      payeePattern: 'Amazon',
      payeeIsRegex: false,
      category: 'shopping : online',
      applyToUncategorizedTransactions: true,
    })
    expect(wrapper.find('[data-test-id="rule-create-dialog"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="apply-rule-result"]').exists()).toBe(false)
    expect(useToastStore().toasts).toEqual([
      expect.objectContaining({
        testId: 'rule-categorized-3',
        variant: 'success',
      }),
    ])
    expect(getTransactions).toHaveBeenCalledTimes(2)
  })

  it('OFF still updates an already categorized source when the final pattern no longer matches', async () => {
    const transaction = source()
    transaction.category = 'Old category'
    const { wrapper } = await setupPage(transaction)
    await wrapper.get('[data-test-id="payee-pattern-input"]').setValue('Different payee')
    await wrapper.get('[data-test-id="apply-rule-now-checkbox"]').setValue(false)
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()

    expect(quickEditTransaction).toHaveBeenCalledOnce()
    expect(createRule).toHaveBeenCalledWith(
      'workspace',
      expect.objectContaining({
        payeePattern: 'Different payee',
        applyToUncategorizedTransactions: false,
      }),
    )
    expect(wrapper.find('[data-test-id="apply-rule-result"]').exists()).toBe(false)
    expect(useToastStore().toasts[0]).toEqual(
      expect.objectContaining({
        testId: 'rule-created',
        variant: 'success',
      }),
    )
  })

  it('skips source Quick Edit for multi-split transactions but still applies the new rule', async () => {
    const transaction = source()
    transaction.hasMultipleSplits = true
    const { wrapper } = await setupPage(transaction)
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()

    expect(quickEditTransaction).not.toHaveBeenCalled()
    expect(createRule).toHaveBeenCalledWith(
      'workspace',
      expect.objectContaining({
        applyToUncategorizedTransactions: true,
      }),
    )
  })

  it.each([0, 1, 496])('reports %s updates accurately', async (count) => {
    createRule.mockResolvedValue(
      PayeeMatchingRuleCreateResultDto.fromJS({ transactionsCategorized: count }),
    )
    const { wrapper } = await setupPage()
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-test-id="apply-rule-result"]').exists()).toBe(false)
    expect(useToastStore().toasts[0]?.testId).toBe(`rule-categorized-${count}`)
  })

  it('retains the dialog and OFF selection and stops if source Quick Edit fails', async () => {
    quickEditTransaction.mockRejectedValue(new Error('Source update failed'))
    const { wrapper } = await setupPage()
    await wrapper.get('[data-test-id="apply-rule-now-checkbox"]').setValue(false)
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()

    expect(createRule).not.toHaveBeenCalled()
    expect(getTransactions).toHaveBeenCalledOnce()
    expect(wrapper.get('[data-test-id="rule-create-dialog"]').text()).toContain(
      'No rule was created',
    )
    expect(
      wrapper.get<HTMLInputElement>('[data-test-id="apply-rule-now-checkbox"]').element.checked,
    ).toBe(false)
    expect(
      wrapper.get<HTMLInputElement>('[data-test-id="payee-pattern-input"]').element.value,
    ).toBe('Amazon')
    expect(wrapper.find('[data-test-id="apply-rule-result"]').exists()).toBe(false)
  })

  it('reports the committed source update if rule creation fails, refreshes, and supports retry', async () => {
    createRule.mockRejectedValueOnce(new Error('Invalid regex'))
    const { wrapper } = await setupPage()
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-test-id="rule-create-dialog"]').text()).toContain(
      'The source transaction was updated, but the rule could not be created',
    )
    expect(getTransactions).toHaveBeenCalledTimes(2)
    expect(wrapper.get<HTMLInputElement>('[data-test-id="category-input"]').element.value).toBe(
      ' shopping : online ',
    )
    expect(wrapper.find('[data-test-id="apply-rule-result"]').exists()).toBe(false)
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()
    expect(createRule).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[data-test-id="rule-create-dialog"]').exists()).toBe(false)
  })

  it('does not claim the source was updated if creation fails for a multi-split source', async () => {
    createRule.mockRejectedValue(new Error('Invalid rule'))
    const transaction = source()
    transaction.hasMultipleSplits = true
    const { wrapper } = await setupPage(transaction)
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-test-id="rule-create-dialog"]').text()).toContain(
      'Failed to create payee matching rule',
    )
    expect(wrapper.text()).not.toContain('The source transaction was updated')
  })

  it('distinguishes a post-save refresh failure and keeps the dialog closed without creating again', async () => {
    const { wrapper } = await setupPage()
    getTransactions.mockRejectedValueOnce(new Error('Refresh failed'))
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test-id="rule-create-dialog"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="apply-rule-result"]').exists()).toBe(false)
    expect(useToastStore().toasts[0]?.testId).toBe('rule-categorized-3')
    expect(wrapper.text()).toContain(
      'The rule was created, but refreshing the transaction list failed',
    )
    expect(createRule).toHaveBeenCalledOnce()
    await wrapper.get('[data-test-id="retry-transactions-button"]').trigger('click')
    await flushPromises()
    expect(createRule).toHaveBeenCalledOnce()
  })

  it('reports an invalid server count without claiming zero or inviting duplicate creation', async () => {
    createRule.mockResolvedValue(PayeeMatchingRuleCreateResultDto.fromJS({}))
    const { wrapper } = await setupPage()
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test-id="rule-create-dialog"]').exists()).toBe(false)
    expect(wrapper.text()).toContain(
      'The rule was created, but the server did not return a valid categorized transaction count',
    )
    expect(wrapper.find('[data-test-id="apply-rule-result"]').exists()).toBe(false)
    expect(getTransactions).toHaveBeenCalledTimes(2)
    expect(createRule).toHaveBeenCalledOnce()
  })

  it('retains the current search, filters and page on refresh without sending them to creation', async () => {
    const search = useTransactionSearchStore()
    search.currentPage = 3
    search.searchText = 'Amazon'
    search.filters.uncategorizedOnly = true
    const { wrapper } = await setupPage()
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    await flushPromises()

    expect(getTransactions.mock.calls.at(-1)).toEqual([
      3,
      undefined,
      undefined,
      'Amazon',
      undefined,
      undefined,
      undefined,
      undefined,
      true,
      'workspace',
    ])
    expect(Object.keys(createRule.mock.calls[0]![1])).not.toContain('sourceTransactionKey')
  })

  it('keeps requests scoped to the original workspace if the selection changes during a save', async () => {
    const update = deferred<object>()
    quickEditTransaction.mockReturnValue(update.promise)
    const { wrapper, store } = await setupPage()
    await wrapper.get('[data-test-id="save-button"]').trigger('click')
    store.currentTenant = new TenantRoleResultDto({ key: 'other', role: TenantRole.Editor })
    await flushPromises()
    update.resolve({})
    await flushPromises()

    expect(createRule).toHaveBeenCalledWith('workspace', expect.any(PayeeMatchingRuleCreateDto))
    expect(wrapper.find('[data-test-id="apply-rule-result"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="rule-create-dialog"]').exists()).toBe(false)
  })
})
