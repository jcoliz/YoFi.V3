import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import DeleteSplitDialog from '~/components/transaction/DeleteSplitDialog.vue'
import ModalDialog from '~/components/ModalDialog.vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'
import { ProblemDetails, TransactionDetailDto, TransactionSplitDto } from '~/utils/apiclient'

const deleteSplit = vi.fn()
vi.stubGlobal('useApiClient', () => ({ deleteSplit }))
enableAutoUnmount(afterEach)
const split = new TransactionSplitDto({
  key: 'split',
  amount: -40.25,
  category: 'Household',
  memo: 'Original memo',
  order: 1,
})
const updated = new TransactionDetailDto({
  key: 'transaction',
  amount: -100,
  category: 'Groceries',
  splits: [],
})

function mountDialog(props: Partial<InstanceType<typeof DeleteSplitDialog>['$props']> = {}) {
  return mount(DeleteSplitDialog, {
    attachTo: document.body,
    props: { show: true, tenantKey: 'workspace', transactionKey: 'transaction', split, ...props },
    global: {
      components: { ModalDialog, ErrorDisplay },
      stubs: { teleport: true, BaseSpinner: true, FeatherIcon: true },
    },
  })
}

function deferredResult() {
  let resolve!: (value: TransactionDetailDto) => void
  const promise = new Promise<TransactionDetailDto>((done) => {
    resolve = done
  })
  deleteSplit.mockReturnValue(promise)
  return resolve
}

describe('split deletion confirmation', () => {
  beforeEach(() => {
    deleteSplit.mockReset().mockResolvedValue(updated)
  })
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('identifies the selection and focuses Cancel instead of destructive confirmation', async () => {
    const wrapper = mountDialog()
    await flushPromises()
    expect(wrapper.get('[data-test-id="delete-split-details"]').text()).toContain('Household')
    expect(wrapper.text()).toContain('-$40.25')
    expect(wrapper.text()).toContain('Original memo')
    expect(document.activeElement).toBe(
      wrapper.get('[data-test-id="delete-split-cancel-button"]').element,
    )
    expect(deleteSplit).not.toHaveBeenCalled()
  })

  it('labels an uncategorized allocation', () => {
    const wrapper = mountDialog({ split: new TransactionSplitDto({ key: 'split', amount: 0 }) })
    expect(wrapper.get('[data-test-id="delete-split-details"]').text()).toContain('Uncategorized')
  })

  it('submits the three keys and emits the complete single-split server result', async () => {
    const wrapper = mountDialog()
    await wrapper.get('[data-test-id="delete-split-confirm-button"]').trigger('click')
    await flushPromises()
    expect(deleteSplit).toHaveBeenCalledExactlyOnceWith('transaction', 'split', 'workspace')
    expect(wrapper.emitted('updated')).toEqual([[updated]])
    expect(wrapper.emitted('update:show')).toEqual([[false]])
    expect(split.category).toBe('Household')
  })

  it.each(['cancel', 'escape', 'backdrop', 'close'])(
    'dismisses by %s without a request',
    async (action) => {
      const wrapper = mountDialog()
      if (action === 'cancel')
        await wrapper.get('[data-test-id="delete-split-cancel-button"]').trigger('click')
      if (action === 'escape')
        document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
      if (action === 'backdrop')
        await wrapper.get('[data-test-id="delete-split-dialog"]').trigger('click')
      if (action === 'close') wrapper.getComponent(ModalDialog).vm.$emit('update:show', false)
      expect(wrapper.emitted('update:show')).toEqual([[false]])
      expect(deleteSplit).not.toHaveBeenCalled()
    },
  )

  it('blocks duplicate submission and all dismissal routes while pending', async () => {
    const resolve = deferredResult()
    const wrapper = mountDialog()
    await wrapper.get('[data-test-id="delete-split-confirm-button"]').trigger('click')
    await wrapper.get('[data-test-id="delete-split-confirm-button"]').trigger('click')
    await wrapper.get('[data-test-id="delete-split-cancel-button"]').trigger('click')
    await wrapper.get('[data-test-id="delete-split-dialog"]').trigger('click')
    wrapper.getComponent(ModalDialog).vm.$emit('update:show', false)
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    expect(wrapper.emitted('update:show')).toBeUndefined()
    expect(deleteSplit).toHaveBeenCalledOnce()
    expect(wrapper.text()).toContain('Deleting...')
    resolve(updated)
    await flushPromises()
    expect(wrapper.emitted('updated')).toEqual([[updated]])
  })

  it.each([400, 404, 409, 500])(
    'retains the selection after HTTP %i and allows retry',
    async (status) => {
      vi.spyOn(console, 'error').mockImplementation(() => {})
      deleteSplit.mockRejectedValueOnce(
        new ProblemDetails({
          status,
          title: 'Delete failed',
          detail: 'Reload and retry.',
        }),
      )
      const wrapper = mountDialog()
      await wrapper.get('[data-test-id="delete-split-confirm-button"]').trigger('click')
      await flushPromises()
      expect(wrapper.emitted('updated')).toBeUndefined()
      expect(wrapper.emitted('update:show')).toBeUndefined()
      expect(wrapper.text()).toContain('Reload and retry.')
      expect(wrapper.text()).toContain('Household')
      await wrapper.get('[data-test-id="delete-split-confirm-button"]').trigger('click')
      await flushPromises()
      expect(wrapper.emitted('updated')).toEqual([[updated]])
    },
  )

  it.each(['tenantKey', 'transactionKey', 'split'] as const)(
    'reports missing %s',
    async (field) => {
      const wrapper = mountDialog({ [field]: field === 'split' ? null : '' })
      await wrapper.get('[data-test-id="delete-split-confirm-button"]').trigger('click')
      expect(wrapper.text()).toContain('No split selected')
      expect(deleteSplit).not.toHaveBeenCalled()
    },
  )

  it.each(['tenantKey', 'transactionKey', 'split', 'show', 'unmount'])(
    'ignores a late result after changing %s',
    async (field) => {
      const resolve = deferredResult()
      const wrapper = mountDialog()
      await wrapper.get('[data-test-id="delete-split-confirm-button"]').trigger('click')
      if (field === 'unmount') wrapper.unmount()
      else if (field === 'split')
        await wrapper.setProps({ split: new TransactionSplitDto({ key: 'other' }) })
      else if (field === 'show') await wrapper.setProps({ show: false })
      else await wrapper.setProps({ [field]: 'other' })
      resolve(updated)
      await flushPromises()
      expect(wrapper.emitted('updated')).toBeUndefined()
      expect(wrapper.emitted('update:show')).toBeUndefined()
    },
  )

  it('clears errors on reopening', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    deleteSplit.mockRejectedValueOnce(new Error('Failure'))
    const wrapper = mountDialog()
    await wrapper.get('[data-test-id="delete-split-confirm-button"]').trigger('click')
    await flushPromises()
    expect(wrapper.getComponent(ErrorDisplay).props('show')).toBe(true)
    await wrapper.setProps({ show: false })
    await wrapper.setProps({ show: true })
    expect(wrapper.getComponent(ErrorDisplay).props('show')).toBe(false)
  })
})
