<script setup lang="ts">
/**
 * Independent split deletion confirmation. The server owns removal and rebalancing.
 * Failed requests retain the selection; dismissal is blocked while saving.
 * Results are ignored after unmount or a change of workspace, transaction, or split.
 */
import {
  TransactionsClient,
  type IProblemDetails,
  type TransactionDetailDto,
  type TransactionSplitDto,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  /** Dialog visibility, bound with v-model:show. */
  show: boolean
  /** Workspace owning the transaction. */
  tenantKey: string
  /** Transaction owning the selected allocation. */
  transactionKey: string
  /** Allocation to confirm; never mutated by this component. */
  split: TransactionSplitDto | null
}>()

const emit = defineEmits<{
  /** Requests dismissal after cancellation or committed deletion. */
  'update:show': [value: boolean]
  /** Returns the committed transaction, including any single-split transition. */
  updated: [transaction: TransactionDetailDto]
}>()

const client = useApiClient(TransactionsClient)
const loading = ref(false)
const error = ref<IProblemDetails>()
const showError = ref(false)
const cancelButton = ref<HTMLButtonElement | null>(null)
let active = true
let generation = 0
const returnFocus = typeof document === 'undefined' ? null : document.activeElement

watch(
  () => [props.show, props.tenantKey, props.transactionKey, props.split] as const,
  async () => {
    generation++
    error.value = undefined
    showError.value = false
    if (props.show) {
      await nextTick()
      cancelButton.value?.focus()
    }
  },
  { immediate: true },
)

function setShow(show: boolean) {
  if (!loading.value) emit('update:show', show)
}

async function deleteSplit() {
  if (loading.value || !props.show) return
  error.value = undefined
  showError.value = false
  if (!props.tenantKey || !props.transactionKey || !props.split?.key) {
    error.value = {
      title: 'No split selected',
      detail: 'Select a workspace, transaction, and split before deleting.',
    }
    showError.value = true
    return
  }
  const requestGeneration = generation
  loading.value = true
  try {
    const updated = await client.deleteSplit(props.transactionKey, props.split.key, props.tenantKey)
    if (!active || generation !== requestGeneration) return
    emit('updated', updated)
    emit('update:show', false)
  } catch (err) {
    const problem = handleApiError(err, 'Delete Split Failed', 'Failed to delete split')
    if (active && generation === requestGeneration) {
      error.value = problem
      showError.value = true
    }
  } finally {
    loading.value = false
  }
}

function handleKeydown(event: KeyboardEvent) {
  if (!props.show || event.defaultPrevented || event.isComposing || event.key !== 'Escape') return
  event.preventDefault()
  event.stopPropagation()
  if (!event.repeat) setShow(false)
}

function formatCurrency(amount: number | undefined) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount ?? 0)
}

onMounted(() => document.addEventListener('keydown', handleKeydown))
onBeforeUnmount(() => {
  active = false
  document.removeEventListener('keydown', handleKeydown)
  nextTick(() => {
    if (returnFocus instanceof HTMLElement && returnFocus.isConnected) returnFocus.focus()
  })
})
</script>

<template>
  <ModalDialog
    :show="show"
    title="Delete Split"
    :loading="loading"
    test-id="delete-split-dialog"
    @update:show="setShow"
  >
    <ErrorDisplay
      v-model:show="showError"
      :problem="error"
      class="mb-3"
    />
    <p>
      Delete this split? The first remaining split will be adjusted to keep the transaction
      balanced. The transaction amount will not change.
    </p>
    <p>This action cannot be undone.</p>
    <div
      v-if="split"
      data-test-id="delete-split-details"
    >
      <strong>{{ split.category || 'Uncategorized' }}</strong>
      <div>{{ formatCurrency(split.amount) }}</div>
      <div
        v-if="split.memo"
        class="split-memo"
      >
        {{ split.memo }}
      </div>
    </div>
    <template #footer>
      <button
        ref="cancelButton"
        type="button"
        class="btn btn-secondary"
        :disabled="loading"
        data-test-id="delete-split-cancel-button"
        @click="setShow(false)"
      >
        Cancel
      </button>
      <button
        type="button"
        class="btn btn-danger"
        :disabled="loading"
        data-test-id="delete-split-confirm-button"
        @click="deleteSplit"
      >
        <BaseSpinner
          v-if="loading"
          size="sm"
          class="me-1"
        />
        {{ loading ? 'Deleting...' : 'Delete' }}
      </button>
    </template>
  </ModalDialog>
</template>

<style scoped>
.split-memo {
  white-space: pre-wrap;
}
</style>
