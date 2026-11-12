import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import TransactionFilterPanel from '~/components/TransactionFilterPanel.vue'
import type { TransactionAdvancedFilters } from '~/types/transactionFilters'

const initialFilters: TransactionAdvancedFilters = {
  fromDate: '',
  toDate: '',
  payee: '',
  category: '',
  memo: '',
  amount: '',
  uncategorizedOnly: false,
}

afterEach(() => {
  vi.useRealTimers()
})

describe('TransactionFilterPanel', () => {
  it.each(['42', '$42', '+42', '+$42', '-42', '$-42', '1,234.56', '.5', '0'])(
    'accepts amount syntax %s without losing its sign',
    async (amount) => {
      const wrapper = mount(TransactionFilterPanel, {
        props: { expanded: true, filters: initialFilters },
      })

      await wrapper.get('[data-test-id="amount-filter"]').setValue(amount)
      await wrapper.get('[data-test-id="amount-filter"]').trigger('keyup.enter')

      expect(wrapper.emitted('commit')?.[0]?.[0]).toEqual({
        ...initialFilters,
        amount,
      })
    },
  )

  it.each(['12,34', '$+', '$$42', '1e3', '79228162514264337593543950336'])(
    'rejects malformed or out-of-range amount %s',
    async (amount) => {
      const wrapper = mount(TransactionFilterPanel, {
        props: { expanded: true, filters: initialFilters },
      })

      await wrapper.get('[data-test-id="amount-filter"]').setValue(amount)
      await wrapper.get('[data-test-id="amount-filter"]').trigger('keyup.enter')

      expect(wrapper.get('[data-test-id="amount-filter"]').attributes('aria-invalid')).toBe('true')
      expect(wrapper.emitted('commit')).toBeUndefined()
    },
  )

  it('keeps text edits as drafts until commit and suppresses Enter-plus-blur duplicates', async () => {
    vi.useFakeTimers()
    const wrapper = mount(TransactionFilterPanel, {
      props: { expanded: true, filters: initialFilters },
    })
    const payee = wrapper.get('[data-test-id="payee-filter"]')

    await payee.setValue(' Cafe ')
    expect(wrapper.emitted('commit')).toBeUndefined()
    await payee.trigger('keyup.enter')
    await payee.trigger('blur')
    await vi.advanceTimersByTimeAsync(60)

    expect(wrapper.emitted('commit')).toEqual([[{ ...initialFilters, payee: 'Cafe' }]])
  })

  it('validates amount drafts and keeps invalid input out of committed filters', async () => {
    const wrapper = mount(TransactionFilterPanel, {
      props: { expanded: true, filters: initialFilters },
    })

    await wrapper.get('[data-test-id="amount-filter"]').setValue('not-money')
    await wrapper.get('[data-test-id="amount-filter"]').trigger('keyup.enter')
    expect(wrapper.get('#amount-filter-error').text()).toContain('valid amount')
    expect(wrapper.emitted('commit')).toBeUndefined()
  })

  it('applies date presets without changing other criteria', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 0, 31, 12))
    const filters = {
      ...initialFilters,
      fromDate: '2026-01-01',
      toDate: '2026-01-31',
      payee: 'Cafe',
      category: 'Food',
      amount: '+42',
    }
    const wrapper = mount(TransactionFilterPanel, {
      props: { expanded: true, filters },
    })

    await wrapper.get('[data-test-id="date-preset-allTime"]').trigger('click')

    expect(wrapper.emitted('commit')).toEqual([[{ ...filters, fromDate: '', toDate: '' }]])
  })

  it('emits a separate Clear All intent', async () => {
    const wrapper = mount(TransactionFilterPanel, {
      props: { expanded: true, filters: { ...initialFilters, amount: 'invalid' } },
    })

    await wrapper.get('[data-test-id="clear-filters-button"]').trigger('click')

    expect(wrapper.emitted('clear-all')).toHaveLength(1)
    expect(wrapper.emitted('commit')).toBeUndefined()
  })
})
