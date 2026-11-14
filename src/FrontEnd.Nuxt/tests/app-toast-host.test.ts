import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import AppToastHost from '~/components/AppToastHost.vue'
import { useToastStore } from '~/stores/toast'

enableAutoUnmount(afterEach)
vi.stubGlobal('useToastStore', useToastStore)

beforeEach(() => {
  setActivePinia(createPinia())
})

describe('App toast test hooks', () => {
  it('renders an optional test ID on the body inside AppToast without depending on wording', async () => {
    const store = useToastStore()
    const wrapper = mount(AppToastHost)
    store.success('Any notification wording', 'Any title', 'rule-categorized-496')
    await nextTick()

    const toast = wrapper.get('[data-test-id="AppToast"]')
    expect(toast.get('[data-test-id="rule-categorized-496"]').classes()).toContain('toast-body')
    await toast.get('[data-test-id="DismissToast"]').trigger('click')
    expect(store.toasts).toHaveLength(0)
  })

  it('passes test IDs through add and leaves untagged toast bodies without a test ID', async () => {
    const store = useToastStore()
    const wrapper = mount(AppToastHost)
    store.add({ title: 'Title', message: 'Message', testId: 'custom-toast' })
    store.success('Existing success call')
    await nextTick()

    const toasts = wrapper.findAll('[data-test-id="AppToast"]')
    expect(toasts[0]!.get('.toast-body').attributes('data-test-id')).toBe('custom-toast')
    expect(toasts[1]!.get('.toast-body').attributes('data-test-id')).toBeUndefined()
    expect(store.toasts[1]).toEqual(
      expect.objectContaining({ title: 'Success', variant: 'success', duration: 5000 }),
    )
  })
})
