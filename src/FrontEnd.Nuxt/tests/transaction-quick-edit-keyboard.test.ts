import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import EditModal from '~/components/transaction/EditModal.vue'
import ModalDialog from '~/components/ModalDialog.vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'
import { TransactionResultDto } from '~/utils/apiclient'

vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: () => ({ getCurrentTenantKey: 'workspace' }),
}))

const quickEditTransaction = vi.fn()
vi.stubGlobal('useApiClient', () => ({ quickEditTransaction }))

enableAutoUnmount(afterEach)

function mountDialog(show = true) {
  return mount(EditModal, {
    attachTo: document.body,
    props: {
      show,
      transaction: new TransactionResultDto({
        key: 'transaction',
        payee: 'Store',
        category: 'Food',
        memo: 'Notes',
      }),
    },
    global: {
      components: { ModalDialog, ErrorDisplay },
      stubs: {
        teleport: true,
        FeatherIcon: true,
        BaseSpinner: true,
      },
    },
  })
}

function pressKey(target: EventTarget, key: string, options: KeyboardEventInit = {}) {
  const event = new KeyboardEvent('keydown', {
    key,
    bubbles: true,
    cancelable: true,
    ...options,
  })
  target.dispatchEvent(event)
  return event
}

describe('quick-edit keyboard shortcuts', () => {
  beforeEach(() => {
    quickEditTransaction.mockReset().mockResolvedValue(undefined)
  })

  it.each(['payee', 'category'])('uses native form submission from the %s field', async (field) => {
    const wrapper = mountDialog()
    const input = wrapper.get<HTMLInputElement>(
      `[data-test-id="edit-transaction-${field}"]`,
    ).element
    const submitButton = wrapper.get<HTMLButtonElement>(
      '[data-test-id="edit-submit-button"]',
    ).element

    expect(input.form).not.toBeNull()
    expect(submitButton.form).toBe(input.form)
    expect(submitButton.type).toBe('submit')
    expect(pressKey(input, 'Enter').defaultPrevented).toBe(false)

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(quickEditTransaction).toHaveBeenCalledOnce()
    expect(wrapper.emitted('updated')).toEqual([[]])
  })

  it('submits through the Update button', async () => {
    const wrapper = mountDialog()
    await wrapper.get('[data-test-id="edit-submit-button"]').trigger('click')
    await flushPromises()

    expect(quickEditTransaction).toHaveBeenCalledOnce()
    expect(wrapper.emitted('updated')).toEqual([[]])
  })

  it.each([false, true])('preserves memo newline behavior with shiftKey=%s', (shiftKey) => {
    const wrapper = mountDialog()
    const event = pressKey(wrapper.get('[data-test-id="edit-transaction-memo"]').element, 'Enter', {
      shiftKey,
    })

    expect(event.defaultPrevented).toBe(false)
    expect(quickEditTransaction).not.toHaveBeenCalled()
    expect(wrapper.emitted('update:show')).toBeUndefined()
  })

  it('cancels Escape without saving changes', () => {
    const wrapper = mountDialog()
    const event = pressKey(wrapper.get('[data-test-id="edit-transaction-memo"]').element, 'Escape')

    expect(event.defaultPrevented).toBe(true)
    expect(wrapper.emitted('update:show')).toEqual([[false]])
    expect(quickEditTransaction).not.toHaveBeenCalled()
    expect(wrapper.emitted('updated')).toBeUndefined()
  })

  it('retains validation when submitting with Enter', async () => {
    const wrapper = mountDialog()
    await wrapper.get('[data-test-id="edit-transaction-payee"]').setValue(' ')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(quickEditTransaction).not.toHaveBeenCalled()
    expect(wrapper.emitted('update:show')).toBeUndefined()
    expect(wrapper.text()).toContain('Payee is required')
  })

  it('does not save again or cancel while an update is in progress', async () => {
    let finishUpdate!: () => void
    quickEditTransaction.mockReturnValue(
      new Promise<void>((resolve) => {
        finishUpdate = resolve
      }),
    )
    const wrapper = mountDialog()

    await wrapper.get('form').trigger('submit')
    await wrapper.get('form').trigger('submit')
    pressKey(document, 'Escape')

    expect(quickEditTransaction).toHaveBeenCalledOnce()
    expect(wrapper.emitted('update:show')).toBeUndefined()

    finishUpdate()
    await flushPromises()
    expect(wrapper.emitted('update:show')).toEqual([[false]])
  })

  it.each(['edit-cancel-button', 'edit-more-button', 'edit-submit-button'])(
    'preserves native Enter activation on %s',
    (testId) => {
      const wrapper = mountDialog()
      const event = pressKey(wrapper.get(`[data-test-id="${testId}"]`).element, 'Enter')

      expect(event.defaultPrevented).toBe(false)
      expect(quickEditTransaction).not.toHaveBeenCalled()
    },
  )

  it('does not handle shortcuts while hidden or after unmounting', async () => {
    const wrapper = mountDialog(false)
    pressKey(document, 'Enter')
    pressKey(document, 'Escape')
    expect(wrapper.emitted('update:show')).toBeUndefined()

    await wrapper.setProps({ show: true })
    wrapper.unmount()
    pressKey(document, 'Enter')
    pressKey(document, 'Escape')

    expect(quickEditTransaction).not.toHaveBeenCalled()
    expect(wrapper.emitted('update:show')).toBeUndefined()
  })
})
