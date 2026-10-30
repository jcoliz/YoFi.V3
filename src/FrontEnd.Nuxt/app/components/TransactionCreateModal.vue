<script setup lang="ts">
/**
 * Creates transactions in the selected workspace, including validation and API error handling.
 */
import { TransactionsClient, TransactionEditDto, type IProblemDetails } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  show: boolean
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
  created: []
}>()

const userPreferencesStore = useUserPreferencesStore()
const transactionsClient = useApiClient(TransactionsClient)

const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)

const formData = ref({
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
  () => props.show,
  (show) => {
    if (!show) return

    formData.value = {
      date: new Date().toISOString().split('T')[0] || '',
      amount: 0,
      payee: '',
      memo: '',
      category: '',
      source: '',
      externalId: '',
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

  if (!formData.value.date) {
    formErrors.value.date = 'Date is required'
    isValid = false
  }

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

  if (formData.value.source && formData.value.source.length > 200) {
    formErrors.value.source = 'Source cannot exceed 200 characters'
    isValid = false
  }

  if (formData.value.externalId && formData.value.externalId.length > 100) {
    formErrors.value.externalId = 'External ID cannot exceed 100 characters'
    isValid = false
  }

  return isValid
}

async function createTransaction() {
  if (loading.value || !validateForm()) return

  const tenantKey = userPreferencesStore.getCurrentTenantKey
  if (!tenantKey) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to create a transaction',
    }
    showError.value = true
    return
  }

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    const dto = new TransactionEditDto({
      date: new Date(formData.value.date),
      amount: formData.value.amount,
      payee: formData.value.payee.trim(),
      memo: formData.value.memo.trim() || undefined,
      category: formData.value.category.trim() || undefined,
      source: formData.value.source.trim() || undefined,
      externalId: formData.value.externalId.trim() || undefined,
    })

    await transactionsClient.createTransaction(tenantKey, dto)
    emit('update:show', false)
    emit('created')
  } catch (err) {
    error.value = handleApiError(err, 'Create Failed', 'Failed to create transaction')
    showError.value = true
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <ModalDialog
    :show="show"
    title="Create Transaction"
    :loading="loading"
    :primary-button-text="loading ? 'Creating...' : 'Create'"
    primary-button-test-id="create-submit-button"
    secondary-button-test-id="create-cancel-button"
    test-id="create-transaction-modal"
    @primary="createTransaction"
    @update:show="emit('update:show', $event)"
  >
    <ErrorDisplay
      v-model:show="showError"
      :problem="error"
      class="mb-3"
    />
    <div class="mb-3">
      <label
        for="createDate"
        class="form-label"
        >Date</label
      >
      <input
        id="createDate"
        v-model="formData.date"
        type="date"
        class="form-control"
        :class="{ 'is-invalid': formErrors.date }"
        data-test-id="create-transaction-date"
      />
      <div
        v-if="formErrors.date"
        class="invalid-feedback"
      >
        {{ formErrors.date }}
      </div>
    </div>
    <div class="mb-3">
      <label
        for="createPayee"
        class="form-label"
        >Payee</label
      >
      <input
        id="createPayee"
        v-model="formData.payee"
        type="text"
        class="form-control"
        :class="{ 'is-invalid': formErrors.payee }"
        placeholder="Enter payee name"
        data-test-id="create-transaction-payee"
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
        for="createAmount"
        class="form-label"
        >Amount</label
      >
      <input
        id="createAmount"
        v-model.number="formData.amount"
        type="number"
        step="0.01"
        class="form-control"
        :class="{ 'is-invalid': formErrors.amount }"
        placeholder="0.00"
        data-test-id="create-transaction-amount"
      />
      <div
        v-if="formErrors.amount"
        class="invalid-feedback"
      >
        {{ formErrors.amount }}
      </div>
    </div>
    <div class="mb-3">
      <label
        for="createMemo"
        class="form-label"
        >Memo</label
      >
      <textarea
        id="createMemo"
        v-model="formData.memo"
        class="form-control"
        :class="{ 'is-invalid': formErrors.memo }"
        placeholder="Add notes about this transaction..."
        rows="3"
        maxlength="1000"
        data-test-id="create-transaction-memo"
      ></textarea>
      <small class="form-text text-muted">{{ formData.memo.length }} / 1000 characters</small>
      <div
        v-if="formErrors.memo"
        class="invalid-feedback"
      >
        {{ formErrors.memo }}
      </div>
    </div>
    <div class="mb-3">
      <label
        for="createCategory"
        class="form-label"
        >Category</label
      >
      <input
        id="createCategory"
        v-model="formData.category"
        type="text"
        class="form-control"
        :class="{ 'is-invalid': formErrors.category }"
        placeholder="Category (optional)"
        maxlength="100"
        data-test-id="create-transaction-category"
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
        for="createSource"
        class="form-label"
        >Source</label
      >
      <input
        id="createSource"
        v-model="formData.source"
        type="text"
        class="form-control"
        :class="{ 'is-invalid': formErrors.source }"
        placeholder="e.g., Chase Checking 1234"
        maxlength="200"
        data-test-id="create-transaction-source"
      />
      <small class="form-text text-muted">Bank account this transaction came from (optional)</small>
      <div
        v-if="formErrors.source"
        class="invalid-feedback"
      >
        {{ formErrors.source }}
      </div>
    </div>
    <div class="mb-3">
      <label
        for="createExternalId"
        class="form-label"
        >External ID</label
      >
      <input
        id="createExternalId"
        v-model="formData.externalId"
        type="text"
        class="form-control"
        :class="{ 'is-invalid': formErrors.externalId }"
        placeholder="Bank transaction ID"
        maxlength="100"
        data-test-id="create-transaction-external-id"
      />
      <small class="form-text text-muted">Bank's unique identifier (optional)</small>
      <div
        v-if="formErrors.externalId"
        class="invalid-feedback"
      >
        {{ formErrors.externalId }}
      </div>
    </div>
  </ModalDialog>
</template>
