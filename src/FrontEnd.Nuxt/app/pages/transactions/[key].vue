<script setup lang="ts">
/**
 * Transaction Details Page
 *
 * Displays full details for a single transaction with inline editing capability
 */

import {
  TransactionsClient,
  TransactionEditDto,
  TenantRole,
  type TransactionDetailDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Transaction Details',
  auth: true,
  layout: 'chrome',
})

const route = useRoute()
const router = useRouter()
const userPreferencesStore = useUserPreferencesStore()

// API Client
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const transactionsClient = new TransactionsClient(baseUrl, authFetch)

// State
const transaction = ref<TransactionDetailDto | null>(null)
const loading = ref(false)
const saving = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const showDeleteModal = ref(false)

// Editing state
const isEditing = ref(false)
const editData = ref({
  date: '',
  amount: 0,
  payee: '',
  memo: '',
  source: '',
  externalId: '',
})

const formErrors = ref({
  date: '',
  amount: '',
  payee: '',
  memo: '',
  source: '',
  externalId: '',
})

// Computed
const transactionKey = computed(() => route.params.key as string)
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)
const canEditTransactions = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})

// Load transaction on mount
onMounted(async () => {
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value && transactionKey.value) {
    await loadTransaction()
  }
})

// Methods
async function loadTransaction() {
  if (!currentTenantKey.value || !transactionKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    transaction.value = await transactionsClient.getTransactionById(
      transactionKey.value,
      currentTenantKey.value,
    )
  } catch (err) {
    error.value = handleApiError(err, 'Load Failed', 'Failed to load transaction details')
    showError.value = true
  } finally {
    loading.value = false
  }
}

function startEditing() {
  if (!transaction.value) return

  const dateStr = transaction.value.date
    ? new Date(transaction.value.date).toISOString().split('T')[0]
    : ''

  editData.value = {
    date: dateStr || '',
    amount: transaction.value.amount || 0,
    payee: transaction.value.payee || '',
    memo: transaction.value.memo || '',
    source: transaction.value.source || '',
    externalId: transaction.value.externalId || '',
  }
  formErrors.value = { date: '', amount: '', payee: '', memo: '', source: '', externalId: '' }
  isEditing.value = true
}

function cancelEditing() {
  isEditing.value = false
  formErrors.value = { date: '', amount: '', payee: '', memo: '', source: '', externalId: '' }
}

function validateForm(): boolean {
  formErrors.value = { date: '', amount: '', payee: '', memo: '', source: '', externalId: '' }
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
  if (!validateForm()) return
  if (!transaction.value?.key || !currentTenantKey.value) return

  saving.value = true
  error.value = undefined
  showError.value = false

  try {
    const dto = new TransactionEditDto({
      date: new Date(editData.value.date),
      amount: editData.value.amount,
      payee: editData.value.payee.trim(),
      memo: editData.value.memo.trim() || undefined,
      source: editData.value.source.trim() || undefined,
      externalId: editData.value.externalId.trim() || undefined,
    })

    transaction.value = await transactionsClient.updateTransaction(
      transaction.value.key,
      currentTenantKey.value,
      dto,
    )
    isEditing.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Save Failed', 'Failed to save transaction')
    showError.value = true
  } finally {
    saving.value = false
  }
}

function openDeleteModal() {
  showDeleteModal.value = true
}

async function deleteTransaction() {
  if (!transaction.value?.key || !currentTenantKey.value) return

  saving.value = true
  error.value = undefined
  showError.value = false

  try {
    await transactionsClient.deleteTransaction(transaction.value.key, currentTenantKey.value)
    // Navigate back to transactions list
    router.push('/transactions')
  } catch (err) {
    error.value = handleApiError(err, 'Delete Failed', 'Failed to delete transaction')
    showError.value = true
    showDeleteModal.value = false
  } finally {
    saving.value = false
  }
}

function goBack() {
  router.push('/transactions')
}

function formatDate(date: Date | undefined): string {
  if (!date) return ''
  return new Date(date).toLocaleDateString()
}

function formatCurrency(amount: number | undefined): string {
  if (amount === undefined) return '$0.00'
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(amount)
}
</script>

<template>
  <div>
    <!-- Workspace Selector -->
    <div class="workspace-selector-container">
      <WorkspaceSelector />
    </div>

    <!-- Main Content -->
    <div class="container py-4">
      <!-- Back Button and Actions -->
      <div class="d-flex justify-content-between align-items-center mb-4">
        <button
          class="btn btn-outline-secondary"
          data-test-id="back-button"
          @click="goBack"
        >
          <FeatherIcon
            icon="arrow-left"
            size="16"
            class="me-1"
          />
          Back to Transactions
        </button>

        <div v-if="!isEditing && canEditTransactions && transaction">
          <button
            class="btn btn-primary me-2"
            data-test-id="edit-button"
            :disabled="loading"
            @click="startEditing"
          >
            <FeatherIcon
              icon="edit"
              size="16"
              class="me-1"
            />
            Edit
          </button>
          <button
            class="btn btn-outline-danger"
            data-test-id="delete-button"
            :disabled="loading"
            @click="openDeleteModal"
          >
            <FeatherIcon
              icon="trash-2"
              size="16"
              class="me-1"
            />
            Delete
          </button>
        </div>

        <div v-if="isEditing">
          <button
            class="btn btn-primary me-2"
            data-test-id="save-button"
            :disabled="saving"
            @click="saveTransaction"
          >
            <FeatherIcon
              icon="save"
              size="16"
              class="me-1"
            />
            {{ saving ? 'Saving...' : 'Save' }}
          </button>
          <button
            class="btn btn-outline-secondary"
            data-test-id="cancel-edit-button"
            :disabled="saving"
            @click="cancelEditing"
          >
            Cancel
          </button>
        </div>
      </div>

      <!-- No Workspace Warning -->
      <div
        v-if="!hasWorkspace"
        class="alert alert-warning"
        data-test-id="no-workspace-warning"
      >
        <FeatherIcon
          icon="alert-circle"
          size="16"
          class="me-2"
        />
        Please select a workspace to view transaction details
      </div>

      <!-- Error Display -->
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-4"
      />

      <!-- Loading State -->
      <div
        v-if="loading"
        class="text-center py-5"
        data-test-id="loading-state"
      >
        <BaseSpinner />
        <div class="mt-2">
          <small
            class="text-muted"
            data-test-id="loading-transaction-text"
            >Loading transaction...</small
          >
        </div>
      </div>

      <!-- Transaction Details -->
      <div
        v-else-if="transaction && !isEditing"
        class="card"
        data-test-id="transaction-details-card"
      >
        <div class="card-body">
          <h2
            class="card-title mb-4"
            data-test-id="transaction-payee"
          >
            {{ transaction.payee }}
          </h2>

          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="text-muted small">Date</label>
              <div
                class="fw-bold"
                data-test-id="transaction-date"
              >
                {{ formatDate(transaction.date) }}
              </div>
            </div>

            <div class="col-md-6 mb-3">
              <label class="text-muted small">Amount</label>
              <div
                class="fw-bold"
                :class="transaction.amount && transaction.amount < 0 ? 'text-success' : ''"
                data-test-id="transaction-amount"
              >
                {{ formatCurrency(transaction.amount) }}
              </div>
            </div>

            <div class="col-12 mb-3">
              <label class="text-muted small">Memo</label>
              <div
                data-test-id="transaction-memo"
                style="white-space: pre-wrap"
              >
                {{ transaction.memo || '(none)' }}
              </div>
            </div>

            <div class="col-md-6 mb-3">
              <label class="text-muted small">Source</label>
              <div data-test-id="transaction-source">
                {{ transaction.source || '(none)' }}
              </div>
            </div>

            <div class="col-md-6 mb-3">
              <label class="text-muted small">External ID</label>
              <div data-test-id="transaction-external-id">
                {{ transaction.externalId || '(none)' }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Edit Form -->
      <div
        v-else-if="transaction && isEditing"
        class="card"
        data-test-id="transaction-edit-card"
      >
        <div class="card-body">
          <h2 class="card-title mb-4">Edit Transaction</h2>

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
              <small class="form-text text-muted"
                >{{ editData.memo.length }} / 1000 characters</small
              >
              <div
                v-if="formErrors.memo"
                class="invalid-feedback"
              >
                {{ formErrors.memo }}
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
        </div>
      </div>
    </div>

    <!-- Delete Modal -->
    <ModalDialog
      v-model:show="showDeleteModal"
      title="Delete Transaction"
      :loading="saving"
      primary-button-variant="danger"
      :primary-button-text="saving ? 'Deleting...' : 'Delete'"
      primary-button-test-id="delete-confirm-button"
      secondary-button-test-id="delete-cancel-button"
      test-id="delete-transaction-modal"
      @primary="deleteTransaction"
    >
      <p>Are you sure you want to delete this transaction?</p>
      <div
        v-if="transaction"
        class="alert alert-warning"
        data-test-id="delete-transaction-details"
      >
        <strong>{{ transaction.payee }}</strong
        ><br />
        {{ formatDate(transaction.date) }} - {{ formatCurrency(transaction.amount) }}
      </div>
    </ModalDialog>
  </div>
</template>

<style scoped>
.workspace-selector-container {
  background-color: #f8f9fa;
  border-bottom: 1px solid #dee2e6;
  padding: 0.75rem 1rem;
}

.form-label {
  font-weight: 600;
}
</style>
