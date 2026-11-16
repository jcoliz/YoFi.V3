<script setup lang="ts">
/**
 * Standalone editor for one allocation of a multi-split transaction.
 *
 * Owns its draft, validation, and API request. Rebalancing is performed by the
 * server; the parent receives the complete committed transaction on success.
 * Failed saves preserve the draft, and dismissal is blocked while saving.
 */
import {
  SplitEditDto,
  TransactionsClient,
  type IProblemDetails,
  type TransactionDetailDto,
  type TransactionSplitDto,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'

/** Identifies the split to edit without coupling the dialog to a page or workspace store. */
const props = defineProps<{
  /** Controls dialog visibility through v-model:show. */
  show: boolean
  /** Workspace key used to scope the update request. */
  tenantKey: string
  /** Key of the transaction that owns the selected split. */
  transactionKey: string
  /** Selected allocation; copied into a local draft and never mutated directly. */
  split: TransactionSplitDto | null
}>()

/** Reports dismissal and committed data; failed saves emit neither event. */
const emit = defineEmits<{
  /** Requests visibility changes after cancellation or a successful save. */
  'update:show': [value: boolean]
  /** Supplies the server result, including the edited and rebalanced allocations. */
  updated: [transaction: TransactionDetailDto]
}>()

const transactionsClient = useApiClient(TransactionsClient)
const loading = ref(false)
const error = ref<IProblemDetails | undefined>()
const showError = ref(false)
const categoryInput = ref<HTMLInputElement | null>(null)
const formData = ref({ amount: '', category: '', memo: '' })
const validationErrors = ref<Partial<Record<'Amount' | 'Category' | 'Memo', string[]>>>({})

watch(
  () => [props.show, props.split] as const,
  async ([show, split]) => {
    if (!show) return

    formData.value = {
      amount: split?.amount?.toString() ?? '',
      category: split?.category ?? '',
      memo: split?.memo ?? '',
    }
    validationErrors.value = {}
    error.value = undefined
    showError.value = false
    await nextTick()
    categoryInput.value?.focus()
  },
  { immediate: true },
)

function setShow(show: boolean) {
  if (!loading.value) emit('update:show', show)
}

function validateForm(): boolean {
  validationErrors.value = {}
  const amount = formData.value.amount.trim()
  if (!amount) {
    validationErrors.value.Amount = ['Amount is required']
  } else if (
    !/^[+-]?(?:\d+(?:\.\d{0,2}0*)?|\.\d{1,2}0*)$/.test(amount) ||
    !Number.isFinite(Number(amount)) ||
    Math.abs(Number(amount)) > 999999999
  ) {
    validationErrors.value.Amount = [
      'Amount must be between -999999999 and 999999999 with no more than two decimal places',
    ]
  }
  if (formData.value.category.length > 200) {
    validationErrors.value.Category = ['Category cannot exceed 200 characters']
  }
  if (formData.value.memo.length > 500) {
    validationErrors.value.Memo = ['Memo cannot exceed 500 characters']
  }
  if (Object.keys(validationErrors.value).length === 0) return true

  error.value = { title: 'Please fix the following errors:', errors: validationErrors.value }
  showError.value = true
  return false
}

async function save() {
  if (loading.value || !props.show) return
  error.value = undefined
  showError.value = false

  if (!props.tenantKey || !props.transactionKey || !props.split?.key) {
    error.value = {
      title: 'No split selected',
      detail: 'Select a workspace, transaction, and split before saving.',
    }
    showError.value = true
    return
  }
  if (!validateForm()) return

  loading.value = true
  try {
    const updated = await transactionsClient.updateSplit(
      props.transactionKey,
      props.split.key,
      props.tenantKey,
      new SplitEditDto({
        amount: Number(formData.value.amount),
        category: formData.value.category,
        memo: formData.value.memo || undefined,
      }),
    )
    emit('updated', updated)
    emit('update:show', false)
  } catch (err) {
    error.value = handleApiError(err, 'Edit Split Failed', 'Failed to update split')
    showError.value = true
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

onMounted(() => document.addEventListener('keydown', handleKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', handleKeydown))
</script>

<template>
  <ModalDialog
    :show="show"
    title="Edit Split"
    :loading="loading"
    test-id="edit-split-dialog"
    @update:show="setShow"
  >
    <form
      id="edit-split-form"
      novalidate
      @submit.prevent="save"
    >
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-3"
      />
      <fieldset :disabled="loading">
        <div class="mb-3">
          <label
            for="edit-split-category"
            class="form-label"
            >Category</label
          >
          <input
            id="edit-split-category"
            ref="categoryInput"
            v-model="formData.category"
            type="text"
            class="form-control"
            :class="{ 'is-invalid': validationErrors.Category }"
            :aria-invalid="!!validationErrors.Category"
            maxlength="200"
            placeholder="Category (optional)"
            data-test-id="edit-split-category"
          />
        </div>
        <div class="mb-3">
          <label
            for="edit-split-amount"
            class="form-label"
            >Amount</label
          >
          <input
            id="edit-split-amount"
            v-model="formData.amount"
            type="text"
            inputmode="decimal"
            class="form-control"
            :class="{ 'is-invalid': validationErrors.Amount }"
            :aria-invalid="!!validationErrors.Amount"
            required
            data-test-id="edit-split-amount"
          />
          <HelpText>
            Another split will be adjusted automatically to keep the transaction balanced. The
            transaction amount will not change.
          </HelpText>
        </div>
        <div class="mb-3">
          <label
            for="edit-split-memo"
            class="form-label"
            >Memo</label
          >
          <textarea
            id="edit-split-memo"
            v-model="formData.memo"
            class="form-control"
            :class="{ 'is-invalid': validationErrors.Memo }"
            :aria-invalid="!!validationErrors.Memo"
            maxlength="500"
            rows="3"
            data-test-id="edit-split-memo"
          />
        </div>
      </fieldset>
    </form>
    <template #footer>
      <button
        type="button"
        class="btn btn-secondary"
        :disabled="loading"
        data-test-id="edit-split-cancel-button"
        @click="setShow(false)"
      >
        Cancel
      </button>
      <button
        type="submit"
        form="edit-split-form"
        class="btn btn-primary"
        :disabled="loading"
        data-test-id="edit-split-save-button"
      >
        <BaseSpinner
          v-if="loading"
          size="sm"
          class="me-1"
        />
        {{ loading ? 'Saving...' : 'Save' }}
      </button>
    </template>
  </ModalDialog>
</template>
