/**
 * Component contracts for the unbalanced-split warning UI.
 *
 * Verifies badge and alert behavior, authoritative rendered values, and that
 * neither component emits events or interferes with surrounding controls.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import TransactionUnbalancedBadge from '~/components/transaction/UnbalancedBadge.vue'
import TransactionSplitBalanceAlert from '~/components/transaction/SplitBalanceAlert.vue'

enableAutoUnmount(afterEach)

const FeatherIconStub = {
  props: ['icon', 'size'],
  template: '<span :data-icon="icon" />',
}

const BaseAlertStub = {
  props: ['variant'],
  template: '<div role="alert" :data-variant="variant"><slot /><slot name="detail" /></div>',
}

const badgeGlobalConfig = {
  stubs: {
    FeatherIcon: FeatherIconStub,
  },
}

const alertGlobalConfig = {
  components: {
    BaseAlert: BaseAlertStub,
  },
}

describe('TransactionUnbalancedBadge', () => {
  it('renders the compact badge, alert icon, label, and tooltip without amount props', () => {
    const wrapper = mount(TransactionUnbalancedBadge, { global: badgeGlobalConfig })

    const badge = wrapper.get('[data-test-id="transaction-unbalanced-badge"]')
    expect(badge.text()).toContain('Unbalanced')
    expect(badge.attributes('title')).toBe('The split amounts do not match the transaction amount.')
    expect(badge.get('[data-icon="alert-triangle"]').attributes('data-icon')).toBe('alert-triangle')
  })

  it('does not emit events or disable surrounding controls', () => {
    const wrapper = mount(
      {
        components: { TransactionUnbalancedBadge },
        template:
          '<div><TransactionUnbalancedBadge /><button data-test-id="save">Save</button></div>',
      },
      { global: badgeGlobalConfig },
    )

    expect(wrapper.emitted()).toEqual({})
    expect(wrapper.get<HTMLButtonElement>('[data-test-id="save"]').element.disabled).toBe(false)
  })
})

describe('TransactionSplitBalanceAlert', () => {
  it('renders authoritative transaction amount, split total, and positive signed difference', () => {
    const wrapper = mount(TransactionSplitBalanceAlert, {
      props: { transactionAmount: 100, splitsTotal: 95 },
      global: alertGlobalConfig,
    })

    const alert = wrapper.get('[data-test-id="transaction-split-balance-alert"]')
    expect(alert.attributes('data-variant')).toBe('danger')
    expect(alert.text()).toContain('Splits do not match the transaction amount.')
    expect(alert.text()).toContain('Transaction amount: $100.00.')
    expect(wrapper.get('[data-test-id="transaction-split-total"]').text()).toContain('$95.00')
    expect(wrapper.get('[data-test-id="transaction-split-difference"]').text()).toContain('+$5.00')
  })

  it('renders a negative signed difference', () => {
    const wrapper = mount(TransactionSplitBalanceAlert, {
      props: { transactionAmount: -100, splitsTotal: -95 },
      global: alertGlobalConfig,
    })

    expect(wrapper.get('[data-test-id="transaction-split-difference"]').text()).toContain('-$5.00')
  })

  it('does not emit events or disable surrounding controls', () => {
    const wrapper = mount(
      {
        components: { TransactionSplitBalanceAlert },
        template:
          '<div><TransactionSplitBalanceAlert :transactionAmount="-100" :splitsTotal="-95" /><button data-test-id="save">Save</button></div>',
      },
      { global: alertGlobalConfig },
    )

    expect(wrapper.emitted()).toEqual({})
    expect(wrapper.get<HTMLButtonElement>('[data-test-id="save"]').element.disabled).toBe(false)
  })
})
