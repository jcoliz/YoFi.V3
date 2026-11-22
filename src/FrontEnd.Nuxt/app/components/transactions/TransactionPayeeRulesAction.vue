<script setup lang="ts">
/**
 * Applies the current workspace's payee rules to one single-split transaction
 * and publishes the resulting notification.
 */
import {
  PayeeRuleApplicationOutcome,
  TransactionsClient,
  type IProblemDetails,
  type PayeeRuleApplicationResultDto,
  type TransactionResultDto,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'
import { useToastStore } from '~/stores/toast'

const props = withDefaults(
  defineProps<{
    /** The transaction receiving the manual payee-rule application. */
    transaction: TransactionResultDto
    /** The current workspace identifier. */
    tenantKey: string
    /** Prevents the action while the transaction list is loading. */
    disabled?: boolean
  }>(),
  {
    disabled: false,
  },
)

const emit = defineEmits<{
  /** Updates or refreshes the Transactions page after a server result. */
  completed: [tenantKey: string, result: PayeeRuleApplicationResultDto]
  /** Displays an unexpected API failure through the page error state. */
  failed: [tenantKey: string, problem: IProblemDetails]
}>()

const client = useApiClient(TransactionsClient)
const toastStore = useToastStore()
const loading = ref(false)
let active = true

onBeforeUnmount(() => {
  active = false
})

async function applyPayeeRules() {
  if (loading.value || props.disabled || !props.transaction.key || !props.tenantKey) return

  const tenantKey = props.tenantKey
  loading.value = true

  try {
    const result = await client.applyPayeeRules(props.transaction.key, tenantKey)
    if (!active || props.tenantKey !== tenantKey) return

    emit('completed', tenantKey, result)

    if (result.outcome === PayeeRuleApplicationOutcome.NoMatch) {
      toastStore.add({
        title: 'No matching rule',
        message: 'No payee rule matched this transaction. Its category was not changed.',
        variant: 'info',
        testId: 'manual-rules-no-match',
      })
    } else if (result.outcome === PayeeRuleApplicationOutcome.Applied) {
      if (!result.previousCategory) {
        toastStore.success(
          `Category set to "${result.category}".`,
          'Success',
          'manual-rules-applied',
        )
      } else {
        toastStore.success(
          `Replaced category "${result.previousCategory}" with "${result.category}".`,
          'Success',
          'manual-rules-overwritten',
        )
      }
    } else if (result.outcome === PayeeRuleApplicationOutcome.TransactionHasMultipleSplits) {
      toastStore.add({
        title: 'Transaction changed',
        message: 'This transaction now has multiple splits, so payee rules were not applied.',
        variant: 'warning',
        testId: 'manual-rules-outdated',
      })
    }
  } catch (err) {
    if (!active || props.tenantKey !== tenantKey) return

    const problem = handleApiError(
      err,
      'Apply Rules Failed',
      'Payee rules could not be applied. Reload the page and try again.',
    )
    emit('failed', tenantKey, problem)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <button
    type="button"
    class="dropdown-item"
    :disabled="loading || disabled"
    data-test-id="apply-payee-rules-button"
    @click="applyPayeeRules"
  >
    <FeatherIcon
      icon="zap"
      size="14"
      class="me-2"
    />
    {{ loading ? 'Applying...' : 'Apply Matching Rules' }}
  </button>
</template>
