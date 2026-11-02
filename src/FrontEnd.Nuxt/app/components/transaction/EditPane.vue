<script setup lang="ts">
/**
 * Edits a transaction, including validation and API error handling.
 */
import {
  TransactionsClient,
  TransactionEditDto,
  type TransactionDetailDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  transaction: TransactionDetailDto
}>()

const emit = defineEmits<{
  updated: [transaction: TransactionDetailDto]
  saving: [value: boolean]
}>()

const userPreferencesStore = useUserPreferencesStore()
const transactionsClient = useApiClient(TransactionsClient)

const saving = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const editData = ref({
  date: '',
  amount: 0,
  payee: '',
  memo: '',
  category: '',
  source: '',
  externalId: '',
})

const formErrors = ref({
  date: '',
  amount: '',
  payee: '',
  memo: '',
  category: '',
  source: '',
  externalId: '',
})

watch(
  () => props.transaction,
  (transaction) => {
    editData.value = {
      date: transaction.date ? new Date(transaction.date).toISOString().split('T')[0] || '' : '',
      amount: transaction.amount || 0,
      payee: transaction.payee || '',
      memo: transaction.memo || '',
      category: transaction.category || '',
      source: transaction.source || '',
      externalId: transaction.externalId || '',
    }
    formErrors.value = {
      date: '',
      amount: '',
      payee: '',
      memo: '',
      category: '',
      source: '',
      externalId: '',
    }
    error.value = undefined
    showError.value = false
  },
  { immediate: true },
)

function validateForm(): boolean {
  formErrors.value = {
    date: '',
    amount: '',
    payee: '',
    memo: '',
    category: '',
    source: '',
    externalId: '',
  }
  let isValid = true

  if (!editData.value.date) {
    formErrors.value.date = 'Date is required'
    isValid = false
  }

  if (!editData.value.payee || !editData.value.payee.trim()) {
    formErrors.value.payee = 'Payee is required'
    isValid = false
  }

  if (editData.value.memo && editData.value.memo.length > 1000) {
    formErrors.value.memo = 'Memo cannot exceed 1000 characters'
    isValid = false
  }

  if (editData.value.category && editData.value.category.length > 100) {
    formErrors.value.category = 'Category cannot exceed 100 characters'
    isValid = false
  }

  if (editData.value.source && editData.value.source.length > 200) {
    formErrors.value.source = 'Source cannot exceed 200 characters'
    isValid = false
  }

  if (editData.value.externalId && editData.value.externalId.length > 100) {
    formErrors.value.externalId = 'External ID cannot exceed 100 characters'
    isValid = false
  }

  return isValid
}

async function saveTransaction() {
  if (saving.value || !validateForm()) return

  const tenantKey = userPreferencesStore.getCurrentTenantKey
  if (!tenantKey) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to update this transaction',
    }
    showError.value = true
    return
  }

  if (!props.transaction.key) {
    error.value = {
      title: 'No transaction selected',
      detail: 'Please select a transaction to update',
    }
    showError.value = true
    return
  }

  saving.value = true
  emit('saving', true)
  error.value = undefined
  showError.value = false

  let updatedTransaction: TransactionDetailDto | undefined
  try {
    const dto = new TransactionEditDto({
      date: new Date(editData.value.date),
      amount: editData.value.amount,
      payee: editData.value.payee.trim(),
      memo: editData.value.memo.trim() || undefined,
      category: editData.value.category.trim() || undefined,
      source: editData.value.source.trim() || undefined,
      externalId: editData.value.externalId.trim() || undefined,
    })

    updatedTransaction = await transactionsClient.updateTransaction(
      props.transaction.key,
      tenantKey,
      dto,
    )
  } catch (err) {
    error.value = handleApiError(err, 'Save Failed', 'Failed to save transaction')
    showError.value = true
  } finally {
    saving.value = false
    emit('saving', false)
  }

  if (updatedTransaction) emit('updated', updatedTransaction)
}
</script>

<template>
  <div
    class="card"
    data-test-id="transaction-edit-card"
  >
    <div class="card-body">
      <h2 class="card-title mb-4">Edit Transaction</h2>

      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-3"
      />

      <form
        id="transaction-edit-form"
        @submit.prevent="saveTransaction"
      >
        <div class="row">
          <div class="col-md-6 mb-3">
            <label
              for="editDate"
              class="form-label"
              >Date</label
            >
            <input
              id="editDate"
              v-model="editData.date"
              type="date"
              class="form-control"
              :class="{ 'is-invalid': formErrors.date }"
              data-test-id="edit-date"
            />
            <div
              v-if="formErrors.date"
              class="invalid-feedback"
            >
              {{ formErrors.date }}
            </div>
          </div>

          <div class="col-md-6 mb-3">
            <label
              for="editAmount"
              class="form-label"
              >Amount</label
            >
            <input
              id="editAmount"
              v-model.number="editData.amount"
              type="number"
              step="0.01"
              class="form-control"
              :class="{ 'is-invalid': formErrors.amount }"
              data-test-id="edit-amount"
            />
            <div
              v-if="formErrors.amount"
              class="invalid-feedback"
            >
              {{ formErrors.amount }}
            </div>
          </div>

          <div class="col-12 mb-3">
            <label
              for="editPayee"
              class="form-label"
              >Payee</label
            >
            <input
              id="editPayee"
              v-model="editData.payee"
              type="text"
              class="form-control"
              :class="{ 'is-invalid': formErrors.payee }"
              data-test-id="edit-payee"
            />
            <div
              v-if="formErrors.payee"
              class="invalid-feedback"
            >
              {{ formErrors.payee }}
            </div>
          </div>

          <div class="col-12 mb-3">
            <label
              for="editMemo"
              class="form-label"
              >Memo</label
            >
            <textarea
              id="editMemo"
              v-model="editData.memo"
              class="form-control"
              :class="{ 'is-invalid': formErrors.memo }"
              rows="3"
              maxlength="1000"
              data-test-id="edit-memo"
            ></textarea>
            <small class="form-text text-muted">{{ editData.memo.length }} / 1000 characters</small>
            <div
              v-if="formErrors.memo"
              class="invalid-feedback"
            >
              {{ formErrors.memo }}
            </div>
          </div>

          <div class="col-12 mb-3">
            <label
              for="editCategory"
              class="form-label"
              >Category</label
            >
            <input
              id="editCategory"
              v-model="editData.category"
              type="text"
              class="form-control"
              :class="{ 'is-invalid': formErrors.category }"
              placeholder="Category (optional)"
              maxlength="100"
              data-test-id="edit-category"
            />
            <small class="form-text text-muted">Optional category for this transaction</small>
            <div
              v-if="formErrors.category"
              class="invalid-feedback"
            >
              {{ formErrors.category }}
            </div>
          </div>

          <div class="col-md-6 mb-3">
            <label
              for="editSource"
              class="form-label"
              >Source</label
            >
            <input
              id="editSource"
              v-model="editData.source"
              type="text"
              class="form-control"
              :class="{ 'is-invalid': formErrors.source }"
              maxlength="200"
              data-test-id="edit-source"
            />
            <small class="form-text text-muted">Bank account (optional)</small>
            <div
              v-if="formErrors.source"
              class="invalid-feedback"
            >
              {{ formErrors.source }}
            </div>
          </div>

          <div class="col-md-6 mb-3">
            <label
              for="editExternalId"
              class="form-label"
              >External ID</label
            >
            <input
              id="editExternalId"
              v-model="editData.externalId"
              type="text"
              class="form-control"
              :class="{ 'is-invalid': formErrors.externalId }"
              maxlength="100"
              data-test-id="edit-external-id"
            />
            <small class="form-text text-muted">Bank's unique identifier (optional)</small>
            <div
              v-if="formErrors.externalId"
              class="invalid-feedback"
            >
              {{ formErrors.externalId }}
            </div>
          </div>
        </div>
      </form>
    </div>
  </div>
</template>
