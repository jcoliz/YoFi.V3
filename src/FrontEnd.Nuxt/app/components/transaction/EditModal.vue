<script setup lang="ts">
/**
 * Quick edits a transaction's payee, category, and memo, including validation and API error handling.
 */
import {
  TransactionsClient,
  TransactionQuickEditDto,
  type TransactionResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  show: boolean
  transaction: TransactionResultDto | null
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
  updated: []
}>()

const userPreferencesStore = useUserPreferencesStore()
const transactionsClient = useApiClient(TransactionsClient)

const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const hasMultipleSplits = computed(() => props.transaction?.hasMultipleSplits === true)

const formData = ref({
  payee: '',
  memo: '',
  category: '',
})

const validationErrors = ref<Partial<Record<'Payee' | 'Memo' | 'Category', string[]>>>({})

const formErrors = computed(() => ({
  payee: validationErrors.value.Payee?.[0] ?? '',
  memo: validationErrors.value.Memo?.[0] ?? '',
  category: validationErrors.value.Category?.[0] ?? '',
}))

watch(
  () => [props.show, props.transaction] as const,
  ([show, transaction]) => {
    if (!show) return

    formData.value = {
      payee: transaction?.payee || '',
      memo: transaction?.memo || '',
      category: transaction?.hasMultipleSplits ? '' : transaction?.category || '',
    }
    validationErrors.value = {}
    error.value = undefined
    showError.value = false
  },
  { immediate: true },
)

function validateForm(): boolean {
  validationErrors.value = {}

  if (!formData.value.payee || !formData.value.payee.trim()) {
    validationErrors.value.Payee = ['Payee is required']
  }

  if (formData.value.memo && formData.value.memo.length > 1000) {
    validationErrors.value.Memo = ['Memo cannot exceed 1000 characters']
  }

  if (!hasMultipleSplits.value && formData.value.category && formData.value.category.length > 100) {
    validationErrors.value.Category = ['Category cannot exceed 100 characters']
  }

  if (Object.keys(validationErrors.value).length === 0) return true

  error.value = {
    title: 'Please fix the following errors:',
    errors: validationErrors.value,
  }
  showError.value = true
  return false
}

async function updateTransaction() {
  if (loading.value) return

  error.value = undefined
  showError.value = false
  if (!validateForm()) return

  const tenantKey = userPreferencesStore.getCurrentTenantKey
  if (!tenantKey) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to update a transaction',
    }
    showError.value = true
    return
  }

  const transactionKey = props.transaction?.key
  if (!transactionKey) {
    error.value = {
      title: 'No transaction selected',
      detail: 'Please select a transaction to update',
    }
    showError.value = true
    return
  }

  loading.value = true

  try {
    const dto = new TransactionQuickEditDto({
      payee: formData.value.payee.trim(),
      memo: formData.value.memo.trim() || undefined,
      category: hasMultipleSplits.value ? undefined : formData.value.category.trim() || undefined,
    })

    await transactionsClient.quickEditTransaction(transactionKey, tenantKey, dto)
    emit('update:show', false)
    emit('updated')
  } catch (err) {
    error.value = handleApiError(err, 'Update Failed', 'Failed to update transaction')
    showError.value = true
  } finally {
    loading.value = false
  }
}

function navigateToDetails() {
  if (!props.transaction?.key) return
  navigateTo(`/transactions/${props.transaction.key}`)
}

function handleKeydown(event: KeyboardEvent) {
  if (!props.show || event.defaultPrevented || event.isComposing) return

  if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    if (!loading.value && !event.repeat) emit('update:show', false)
  }
}

onMounted(() => document.addEventListener('keydown', handleKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', handleKeydown))
</script>

<template>
  <ModalDialog
    :show="show"
    title="Quick Edit Transaction"
    :loading="loading"
    :primary-button-text="loading ? 'Updating...' : 'Update'"
    primary-button-test-id="edit-submit-button"
    secondary-button-test-id="edit-cancel-button"
    test-id="edit-transaction-modal"
    @primary="updateTransaction"
    @update:show="emit('update:show', $event)"
  >
    <form
      id="edit-transaction-form"
      @submit.prevent="updateTransaction"
    >
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-3"
      />
      <div class="mb-3">
        <label
          for="editPayee"
          class="form-label"
          >Payee</label
        >
        <input
          id="editPayee"
          v-model="formData.payee"
          type="text"
          class="form-control"
          :class="{ 'is-invalid': formErrors.payee }"
          placeholder="Enter payee name"
          data-test-id="edit-transaction-payee"
        />
        <div
          v-if="formErrors.payee"
          class="invalid-feedback"
        >
          {{ formErrors.payee }}
        </div>
      </div>
      <div class="mb-3">
        <label
          for="editCategory"
          class="form-label"
          >Category</label
        >
        <input
          id="editCategory"
          v-model="formData.category"
          type="text"
          class="form-control"
          :class="{ 'is-invalid': formErrors.category }"
          placeholder="Category (optional)"
          maxlength="100"
          :disabled="hasMultipleSplits"
          aria-describedby="editCategoryHelp"
          data-test-id="edit-transaction-category"
        />
        <HelpText
          id="editCategoryHelp"
          data-test-id="edit-transaction-category-help"
        >
          {{
            hasMultipleSplits
              ? 'This transaction is split across multiple categories. Category cannot be changed in Quick Edit.'
              : 'Optional category for this transaction'
          }}
        </HelpText>
        <div
          v-if="formErrors.category"
          class="invalid-feedback"
        >
          {{ formErrors.category }}
        </div>
      </div>
      <div class="mb-3">
        <label
          for="editMemo"
          class="form-label"
          >Memo</label
        >
        <textarea
          id="editMemo"
          v-model="formData.memo"
          class="form-control"
          :class="{ 'is-invalid': formErrors.memo }"
          placeholder="Add notes about this transaction..."
          rows="3"
          maxlength="1000"
          data-test-id="edit-transaction-memo"
        ></textarea>
        <small class="form-text text-muted">{{ formData.memo.length }} / 1000 characters</small>
        <div
          v-if="formErrors.memo"
          class="invalid-feedback"
        >
          {{ formErrors.memo }}
        </div>
      </div>
    </form>

    <template #footer>
      <button
        type="button"
        class="btn btn-outline-secondary me-auto"
        data-test-id="edit-more-button"
        :disabled="loading"
        @click="navigateToDetails"
      >
        <FeatherIcon
          icon="arrow-right"
          size="14"
          class="me-1"
        />
        More
      </button>
      <button
        type="button"
        class="btn btn-secondary"
        data-test-id="edit-cancel-button"
        :disabled="loading"
        @click="emit('update:show', false)"
      >
        Cancel
      </button>
      <button
        type="submit"
        form="edit-transaction-form"
        class="btn btn-primary"
        data-test-id="edit-submit-button"
        :disabled="loading"
      >
        <BaseSpinner
          v-if="loading"
          size="sm"
          class="me-1"
        />
        {{ loading ? 'Updating...' : 'Update' }}
      </button>
    </template>
  </ModalDialog>
</template>
