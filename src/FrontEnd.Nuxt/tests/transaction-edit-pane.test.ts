import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import EditPane from '~/components/transaction/EditPane.vue'
import { TransactionDetailDto, TransactionSplitDto } from '~/utils/apiclient'

vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: () => ({ getCurrentTenantKey: 'workspace' }),
}))

const updateTransaction = vi.fn()
vi.stubGlobal('useApiClient', () => ({ updateTransaction }))

enableAutoUnmount(afterEach)

function transaction(splitCount: number) {
  return new TransactionDetailDto({
    key: 'transaction',
    date: new Date(2026, 0, 1),
    amount: -100,
    payee: 'Store',
    category: 'Groceries',
    splits: Array.from(
      { length: splitCount },
      (_, order) =>
        new TransactionSplitDto({
          key: `split-${order}`,
          order,
          amount: -100 / splitCount,
          category: order === 0 ? 'Groceries' : 'Household',
        }),
    ),
  })
}

function mountPane(splitCount: number) {
  return mount(EditPane, {
    props: { transaction: transaction(splitCount) },
    global: { stubs: { ErrorDisplay: true, FeatherIcon: true, BaseSpinner: true } },
  })
}

describe('transaction/EditPane category protection', () => {
  beforeEach(() => {
    updateTransaction.mockReset().mockResolvedValue(transaction(2))
  })

  it('hides category and omits it from multi-split updates even if present in the input DTO', async () => {
    const wrapper = mountPane(2)

    expect(wrapper.find('[data-test-id="edit-category"]').exists()).toBe(false)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(updateTransaction).toHaveBeenCalledOnce()
    const [key, workspace, dto] = updateTransaction.mock.calls[0]!
    expect(key).toBe('transaction')
    expect(workspace).toBe('workspace')
    expect(dto.category).toBeUndefined()
    expect(JSON.parse(JSON.stringify(dto))).not.toHaveProperty('category')
    expect(wrapper.emitted('updated')).toHaveLength(1)
  })

  it.each([0, 1])('allows category edits with %i visible splits', async (splitCount) => {
    const wrapper = mountPane(splitCount)
    await wrapper.get('[data-test-id="edit-category"]').setValue('Updated category')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(updateTransaction).toHaveBeenCalledOnce()
    expect(updateTransaction.mock.calls[0]![2].category).toBe('Updated category')
  })

  it('updates category availability when the transaction split count changes', async () => {
    const wrapper = mountPane(0)
    await wrapper.get('[data-test-id="edit-category"]').setValue('Draft category')
    await wrapper.setProps({ transaction: transaction(2) })
    expect(wrapper.find('[data-test-id="edit-category"]').exists()).toBe(false)

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(updateTransaction.mock.calls[0]![2].category).toBeUndefined()

    await wrapper.setProps({ transaction: transaction(0) })
    expect(wrapper.get<HTMLInputElement>('[data-test-id="edit-category"]').element.value).toBe(
      'Groceries',
    )
  })
})
