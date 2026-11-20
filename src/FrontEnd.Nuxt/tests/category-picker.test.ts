import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import CategoryPicker from '~/components/CategoryPicker.vue'
import CreateModal from '~/components/transaction/CreateModal.vue'
import ModalDialog from '~/components/ModalDialog.vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'

vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: () => ({ getCurrentTenantKey: 'workspace' }),
}))

const getCategorySuggestions = vi.fn()
const createTransaction = vi.fn()
vi.stubGlobal('useApiClient', () => ({ getCategorySuggestions, createTransaction }))
enableAutoUnmount(afterEach)

function mountPicker(props: Record<string, unknown> = {}) {
  return mount(CategoryPicker, {
    props: {
      modelValue: '',
      inputId: 'category',
      inputTestId: 'category-input',
      tenantKey: 'workspace',
      ...props,
    },
  })
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise
    reject = rejectPromise
  })
  return { promise, resolve, reject }
}

beforeEach(() => {
  vi.useFakeTimers()
  getCategorySuggestions.mockReset()
  createTransaction.mockReset().mockResolvedValue(undefined)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('CategoryPicker', () => {
  it('does not look up or display suggestions for an initially populated model', () => {
    const wrapper = mountPicker({ modelValue: 'Groceries' })

    expect(getCategorySuggestions).not.toHaveBeenCalled()
    expect(wrapper.find('[data-test-id="suggestions"]').exists()).toBe(false)
  })

  it('debounces the first non-blank lookup through the generated client', async () => {
    getCategorySuggestions.mockResolvedValue(['Groceries'])
    const wrapper = mountPicker()

    await wrapper.get('[data-test-id="category-input"]').setValue('g')
    await vi.advanceTimersByTimeAsync(199)
    expect(getCategorySuggestions).not.toHaveBeenCalled()

    await vi.advanceTimersByTimeAsync(1)
    await flushPromises()

    expect(getCategorySuggestions).toHaveBeenCalledExactlyOnceWith('g', 'workspace')
    expect(wrapper.find('[data-test-id="suggestions"]').exists()).toBe(true)
  })

  it('clears immediately and suppresses an older result', async () => {
    const lookup = deferred<string[]>()
    getCategorySuggestions.mockReturnValue(lookup.promise)
    const wrapper = mountPicker()

    await wrapper.get('[data-test-id="category-input"]').setValue('g')
    await vi.advanceTimersByTimeAsync(200)
    await wrapper.get('[data-test-id="category-input"]').setValue('')
    lookup.resolve(['Groceries'])
    await flushPromises()

    expect(wrapper.find('[data-test-id="suggestions"]').exists()).toBe(false)
  })

  it('refreshes on later input and only accepts the latest response', async () => {
    const first = deferred<string[]>()
    const second = deferred<string[]>()
    getCategorySuggestions
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
    const wrapper = mountPicker()

    await wrapper.get('[data-test-id="category-input"]').setValue('g')
    await vi.advanceTimersByTimeAsync(200)
    await wrapper.get('[data-test-id="category-input"]').setValue('gr')
    await vi.advanceTimersByTimeAsync(200)

    second.resolve(['Groceries'])
    await flushPromises()
    first.resolve(['Garden'])
    await flushPromises()

    expect(wrapper.text()).toContain('Groceries')
    expect(wrapper.text()).not.toContain('Garden')
  })

  it('selects by mouse and Enter without submitting a surrounding form', async () => {
    getCategorySuggestions.mockResolvedValue(['Groceries'])
    const wrapper = mountPicker()

    await wrapper.get('[data-test-id="category-input"]').setValue('g')
    await vi.advanceTimersByTimeAsync(200)
    await flushPromises()

    await wrapper.get('[role="option"]').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual(['Groceries'])

    await wrapper.get('[data-test-id="category-input"]').setValue('g')
    await vi.advanceTimersByTimeAsync(200)
    await flushPromises()
    await wrapper.get('[data-test-id="category-input"]').trigger('keydown', { key: 'Enter' })

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual(['Groceries'])
  })

  it('closes on Escape and blur while preserving the entered value', async () => {
    getCategorySuggestions.mockResolvedValue(['Groceries'])
    const wrapper = mountPicker()
    const input = wrapper.get('[data-test-id="category-input"]')

    await input.setValue('grocery')
    await wrapper.setProps({ modelValue: 'grocery' })
    await vi.advanceTimersByTimeAsync(200)
    await flushPromises()

    await input.trigger('keydown', { key: 'Escape' })
    expect(wrapper.find('[data-test-id="suggestions"]').exists()).toBe(false)

    await vi.advanceTimersByTimeAsync(200)
    await input.trigger('blur')
    expect(wrapper.find('[data-test-id="suggestions"]').exists()).toBe(false)
    expect((input.element as HTMLInputElement).value).toBe('grocery')
  })

  it('clears prior suggestions when the tenant changes', async () => {
    getCategorySuggestions.mockResolvedValue(['Groceries'])
    const wrapper = mountPicker()

    await wrapper.get('[data-test-id="category-input"]').setValue('g')
    await vi.advanceTimersByTimeAsync(200)
    await flushPromises()
    expect(wrapper.find('[data-test-id="suggestions"]').exists()).toBe(true)

    await wrapper.setProps({ tenantKey: 'other-workspace' })

    expect(wrapper.find('[data-test-id="suggestions"]').exists()).toBe(false)
  })

  it('keeps the value editable and emits no form error after lookup failure', async () => {
    getCategorySuggestions.mockRejectedValue(new Error('Lookup failed'))
    const wrapper = mountPicker()

    await wrapper.get('[data-test-id="category-input"]').setValue('grocery')
    await vi.advanceTimersByTimeAsync(200)
    await flushPromises()

    expect((wrapper.get('[data-test-id="category-input"]').element as HTMLInputElement).value).toBe(
      'grocery',
    )
    expect(wrapper.find('[data-test-id="suggestions"]').exists()).toBe(false)
    expect(wrapper.emitted('form-error')).toBeUndefined()
  })

  it('wires the picker into transaction creation', async () => {
    getCategorySuggestions.mockResolvedValue(['Groceries'])
    const wrapper = mount(CreateModal, {
      attachTo: document.body,
      props: { show: true },
      global: {
        components: { CategoryPicker, ModalDialog, ErrorDisplay },
        stubs: { teleport: true, FeatherIcon: true, BaseSpinner: true },
      },
    })

    await wrapper.get('[data-test-id="create-transaction-payee"]').setValue('Store')
    await wrapper.get('[data-test-id="create-transaction-category"]').setValue('Groceries')
    await wrapper.get('[data-test-id="create-submit-button"]').trigger('click')
    await flushPromises()

    expect(createTransaction).toHaveBeenCalledOnce()
    expect(createTransaction.mock.calls[0]![1].category).toBe('Groceries')
  })

  it('forwards maxlength, disabled, invalid, and aria-describedby states', () => {
    const wrapper = mountPicker({
      maxlength: 100,
      disabled: true,
      invalid: true,
      describedBy: 'category-help',
    })
    const input = wrapper.get<HTMLInputElement>('[data-test-id="category-input"]')

    expect(input.attributes('maxlength')).toBe('100')
    expect(input.attributes('disabled')).toBeDefined()
    expect(input.attributes('aria-invalid')).toBe('true')
    expect(input.attributes('aria-describedby')).toBe('category-help')
  })
})
