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

const formData = ref({
  payee: '',
  memo: '',
  category: '',
})

const formErrors = ref({
  payee: '',
  memo: '',
  category: '',
})

watch(
  () => [props.show, props.transaction] as const,
  ([show, transaction]) => {
    if (!show) return

    formData.value = {
      payee: transaction?.payee || '',
      memo: transaction?.memo || '',
      category: transaction?.category || '',
    }
    formErrors.value = {
      payee: '',
      memo: '',
      category: '',
    }
    error.value = undefined
    showError.value = false
  },
  { immediate: true },
)

function validateForm(): boolean {
  formErrors.value = {
    payee: '',
    memo: '',
    category: '',
  }
  let isValid = true

  if (!formData.value.payee || !formData.value.payee.trim()) {
    formErrors.value.payee = 'Payee is required'
    isValid = false
  }

  if (formData.value.memo && formData.value.memo.length > 1000) {
    formErrors.value.memo = 'Memo cannot exceed 1000 characters'
    isValid = false
  }

  if (formData.value.category && formData.value.category.length > 100) {
    formErrors.value.category = 'Category cannot exceed 100 characters'
    isValid = false
  }

  return isValid
}

async function updateTransaction() {
  if (loading.value || !validateForm()) return

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
  error.value = undefined
  showError.value = false

  try {
    const dto = new TransactionQuickEditDto({
      payee: formData.value.payee.trim(),
      memo: formData.value.memo.trim() || undefined,
      category: formData.value.category.trim() || undefined,
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
        data-test-id="edit-transaction-category"
      />
      <small class="form-text text-muted">Optional category for this transaction</small>
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
        type="button"
        class="btn btn-primary"
        data-test-id="edit-submit-button"
        :disabled="loading"
        @click="updateTransaction"
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
