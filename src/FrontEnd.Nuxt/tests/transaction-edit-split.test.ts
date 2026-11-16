import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import EditSplitDialog from '~/components/transaction/EditSplitDialog.vue'
import ModalDialog from '~/components/ModalDialog.vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'
import {
  ProblemDetails,
  SplitEditDto,
  TransactionDetailDto,
  TransactionSplitDto,
} from '~/utils/apiclient'

const updateSplit = vi.fn()
vi.stubGlobal('useApiClient', () => ({ updateSplit }))
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
  splits: [
    new TransactionSplitDto({ key: 'first', amount: -50, order: 0 }),
    new TransactionSplitDto({ key: 'split', amount: -50, order: 1 }),
  ],
})

function mountDialog(props: Partial<InstanceType<typeof EditSplitDialog>['$props']> = {}) {
  return mount(EditSplitDialog, {
    attachTo: document.body,
    props: { show: true, tenantKey: 'workspace', transactionKey: 'transaction', split, ...props },
    global: {
      components: { ModalDialog, ErrorDisplay },
      stubs: { teleport: true, BaseSpinner: true, FeatherIcon: true },
    },
  })
}

describe('transaction split edit dialog', () => {
  beforeEach(() => {
    updateSplit.mockReset().mockResolvedValue(updated)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('initializes an independent draft from the selected split', async () => {
    const wrapper = mountDialog()
    await flushPromises()

    expect(wrapper.get<HTMLInputElement>('[data-test-id="edit-split-amount"]').element.value).toBe(
      '-40.25',
    )
    expect(
      wrapper.get<HTMLInputElement>('[data-test-id="edit-split-category"]').element.value,
    ).toBe('Household')
    expect(wrapper.get<HTMLTextAreaElement>('[data-test-id="edit-split-memo"]').element.value).toBe(
      'Original memo',
    )
    await wrapper.get('[data-test-id="edit-split-amount"]').setValue('-50')
    expect(split.amount).toBe(-40.25)
    expect(updateSplit).not.toHaveBeenCalled()
  })

  it('submits the selected keys and emits the complete server result', async () => {
    const wrapper = mountDialog()
    await wrapper.get('[data-test-id="edit-split-amount"]').setValue('-50')
    await wrapper.get('[data-test-id="edit-split-category"]').setValue('  home : supplies ')
    await wrapper.get('[data-test-id="edit-split-memo"]').setValue('Edited memo')
    await wrapper.get('[data-test-id="edit-split-save-button"]').trigger('click')
    await flushPromises()

    expect(updateSplit).toHaveBeenCalledExactlyOnceWith(
      'transaction',
      'split',
      'workspace',
      new SplitEditDto({ amount: -50, category: '  home : supplies ', memo: 'Edited memo' }),
    )
    expect(wrapper.emitted('updated')).toEqual([[updated]])
    expect(wrapper.emitted('update:show')).toEqual([[false]])
  })

  it.each(['0', '40.25', '-40.25', '.25', '1.2300', '-999999999', '999999999'])(
    'accepts the signed currency amount %s',
    async (amount) => {
      const wrapper = mountDialog()
      await wrapper.get('[data-test-id="edit-split-amount"]').setValue(amount)
      await wrapper.get('[data-test-id="edit-split-category"]').setValue('')
      await wrapper.get('[data-test-id="edit-split-memo"]').setValue('')
      await wrapper.get('form').trigger('submit')
      await flushPromises()

      expect(updateSplit).toHaveBeenCalledWith(
        'transaction',
        'split',
        'workspace',
        new SplitEditDto({ amount: Number(amount), category: '', memo: undefined }),
      )
    },
  )

  it.each(['', ' ', 'not money', 'NaN', 'Infinity', '1e2', '1.005', '-1000000000', '1000000000'])(
    'rejects invalid amount %j without closing',
    async (amount) => {
      const wrapper = mountDialog()
      await wrapper.get('[data-test-id="edit-split-amount"]').setValue(amount)
      await wrapper.get('form').trigger('submit')

      expect(updateSplit).not.toHaveBeenCalled()
      expect(wrapper.emitted('update:show')).toBeUndefined()
      expect(wrapper.get('[data-test-id="validation-errors"]').text()).toContain('Amount')
    },
  )

  it.each([
    ['category', 201],
    ['memo', 501],
  ] as const)('validates %s length', async (field, length) => {
    const wrapper = mountDialog()
    await wrapper.get(`[data-test-id="edit-split-${field}"]`).setValue('x'.repeat(length))
    await wrapper.get('form').trigger('submit')

    expect(updateSplit).not.toHaveBeenCalled()
    expect(wrapper.get('[data-test-id="validation-errors"]').text()).toContain('cannot exceed')
  })

  it.each(['tenantKey', 'transactionKey', 'split'] as const)(
    'reports missing %s instead of silently ignoring save',
    async (field) => {
      const wrapper = mountDialog({ [field]: field === 'split' ? null : '' })
      await wrapper.get('form').trigger('submit')

      expect(updateSplit).not.toHaveBeenCalled()
      expect(wrapper.text()).toContain('No split selected')
    },
  )

  it.each([400, 403, 404, 409, 500])(
    'retains the draft and displays a %s API failure',
    async (status) => {
      vi.spyOn(console, 'error').mockImplementation(() => {})
      updateSplit.mockRejectedValue(
        new ProblemDetails({
          status,
          title: 'Update rejected',
          detail: 'Reload and try again.',
          errors: status === 400 ? { Amount: ['Server validation failed'] } : undefined,
        }),
      )
      const wrapper = mountDialog()
      await wrapper.get('[data-test-id="edit-split-amount"]').setValue('-50')
      await wrapper.get('form').trigger('submit')
      await flushPromises()

      expect(wrapper.emitted('updated')).toBeUndefined()
      expect(wrapper.emitted('update:show')).toBeUndefined()
      expect(
        wrapper.get<HTMLInputElement>('[data-test-id="edit-split-amount"]').element.value,
      ).toBe('-50')
      expect(wrapper.text()).toContain('Reload and try again.')
      if (status === 400) expect(wrapper.text()).toContain('Server validation failed')

      updateSplit.mockResolvedValue(updated)
      await wrapper.get('form').trigger('submit')
      await flushPromises()
      expect(wrapper.emitted('updated')).toEqual([[updated]])
    },
  )

  it('cancels without saving', async () => {
    const wrapper = mountDialog()
    await wrapper.get('[data-test-id="edit-split-amount"]').setValue('-50')
    await wrapper.get('[data-test-id="edit-split-cancel-button"]').trigger('click')

    expect(wrapper.emitted('update:show')).toEqual([[false]])
    expect(wrapper.emitted('updated')).toBeUndefined()
    expect(updateSplit).not.toHaveBeenCalled()
    expect(split.amount).toBe(-40.25)
  })

  it('ignores repeated saves and dismissal while saving', async () => {
    let finish!: (value: TransactionDetailDto) => void
    updateSplit.mockReturnValue(
      new Promise<TransactionDetailDto>((resolve) => {
        finish = resolve
      }),
    )
    const wrapper = mountDialog()
    await wrapper.get('form').trigger('submit')
    await wrapper.get('form').trigger('submit')
    wrapper.getComponent(ModalDialog).vm.$emit('update:show', false)
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))

    expect(updateSplit).toHaveBeenCalledOnce()
    expect(wrapper.emitted('update:show')).toBeUndefined()
    expect(wrapper.get<HTMLFieldSetElement>('fieldset').element.disabled).toBe(true)
    finish(updated)
    await flushPromises()
    expect(wrapper.emitted('updated')).toEqual([[updated]])
    expect(wrapper.emitted('update:show')).toEqual([[false]])
  })

  it('resets the draft and errors on reopening or selecting a different split', async () => {
    const wrapper = mountDialog()
    await wrapper.get('[data-test-id="edit-split-amount"]').setValue('')
    await wrapper.get('form').trigger('submit')
    await wrapper.setProps({ show: false })
    await wrapper.setProps({ show: true })

    expect(wrapper.find('[data-test-id="error-display"]').exists()).toBe(false)
    expect(wrapper.get<HTMLInputElement>('[data-test-id="edit-split-amount"]').element.value).toBe(
      '-40.25',
    )
    await wrapper.setProps({
      split: new TransactionSplitDto({ key: 'first', amount: -59.75, category: 'Food' }),
    })
    expect(wrapper.get<HTMLInputElement>('[data-test-id="edit-split-amount"]').element.value).toBe(
      '-59.75',
    )
    expect(
      wrapper.get<HTMLInputElement>('[data-test-id="edit-split-category"]').element.value,
    ).toBe('Food')
  })

  it('supports Escape without intercepting memo newlines and removes its listener on unmount', () => {
    const wrapper = mountDialog()
    const newline = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })
    wrapper.get('[data-test-id="edit-split-memo"]').element.dispatchEvent(newline)
    expect(newline.defaultPrevented).toBe(false)
    const escape = new KeyboardEvent('keydown', { key: 'Escape', cancelable: true })
    document.dispatchEvent(escape)
    expect(escape.defaultPrevented).toBe(true)
    expect(wrapper.emitted('update:show')).toEqual([[false]])
    wrapper.unmount()
    const afterUnmount = new KeyboardEvent('keydown', { key: 'Escape', cancelable: true })
    document.dispatchEvent(afterUnmount)
    expect(afterUnmount.defaultPrevented).toBe(false)
  })
})
