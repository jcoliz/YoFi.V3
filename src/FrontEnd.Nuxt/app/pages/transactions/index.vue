<script setup lang="ts">
/**
 * Transactions Page
 *
 * Displays and manages transactions for the selected workspace with full CRUD functionality
 */

import {
  TransactionsClient,
  TransactionEditDto,
  TransactionQuickEditDto,
  TenantRole,
  type TransactionResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Transactions',
  order: 3,
  auth: true,
  layout: 'chrome',
})

const userPreferencesStore = useUserPreferencesStore()

// API Client
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const transactionsClient = new TransactionsClient(baseUrl, authFetch)

// Page ready state (for SSR/hydration)
const ready = ref(false)

// State
const transactions = ref<TransactionResultDto[]>([])
const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const showCreateModal = ref(false)
const showEditModal = ref(false)
const showDeleteModal = ref(false)
const selectedTransaction = ref<TransactionResultDto | null>(null)

// Form data
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

// Date range filters
const fromDate = ref<string>('')
const toDate = ref<string>('')

// Computed
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)
const canEditTransactions = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})

// Watch for workspace changes
watch(currentTenantKey, async (newKey) => {
  if (newKey) {
    await loadTransactions()
  } else {
    transactions.value = []
  }
})

// Load transactions on mount
onMounted(async () => {
  ready.value = true
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value) {
    await loadTransactions()
  }
})

// Methods
async function loadTransactions() {
  if (!currentTenantKey.value) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to view transactions',
    }
    showError.value = true
    return
  }

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    const from = fromDate.value ? new Date(fromDate.value) : undefined
    const to = toDate.value ? new Date(toDate.value) : undefined

    transactions.value = await transactionsClient.getTransactions(from, to, currentTenantKey.value)
  } catch (err) {
    error.value = handleApiError(err, 'Load Failed', 'Failed to load transactions')
    showError.value = true
  } finally {
    loading.value = false
  }
}

function openCreateModal() {
  const today = new Date().toISOString().split('T')[0]
  formData.value = {
    date: today || '',
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
  showCreateModal.value = true
}

function openEditModal(transaction: TransactionResultDto) {
  selectedTransaction.value = transaction
  const dateStr = transaction.date ? new Date(transaction.date).toISOString().split('T')[0] : ''
  formData.value = {
    date: dateStr || '',
    amount: transaction.amount || 0,
    payee: transaction.payee || '',
    memo: transaction.memo || '',
    category: transaction.category || '',
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
  showEditModal.value = true
}

function openDeleteModal(transaction: TransactionResultDto) {
  selectedTransaction.value = transaction
  showDeleteModal.value = true
}

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

  // Validate memo length (max 1000 characters)
  if (formData.value.memo && formData.value.memo.length > 1000) {
    formErrors.value.memo = 'Memo cannot exceed 1000 characters'
    isValid = false
  }

  // Validate category length (max 100 characters)
  if (formData.value.category && formData.value.category.length > 100) {
    formErrors.value.category = 'Category cannot exceed 100 characters'
    isValid = false
  }

  // Validate source length (max 200 characters)
  if (formData.value.source && formData.value.source.length > 200) {
    formErrors.value.source = 'Source cannot exceed 200 characters'
    isValid = false
  }

  // Validate externalId length (max 100 characters)
  if (formData.value.externalId && formData.value.externalId.length > 100) {
    formErrors.value.externalId = 'External ID cannot exceed 100 characters'
    isValid = false
  }

  return isValid
}

async function createTransaction() {
  if (!validateForm()) return
  if (!currentTenantKey.value) return

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

    await transactionsClient.createTransaction(currentTenantKey.value, dto)
    await loadTransactions()
    showCreateModal.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Create Failed', 'Failed to create transaction')
    showError.value = true
  } finally {
    loading.value = false
  }
}

async function updateTransaction() {
  // For quick edit, validate payee, memo, and category
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

  if (!isValid) return
  if (!selectedTransaction.value?.key || !currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    // Use quick edit endpoint: sends payee, memo, and category
    const dto = new TransactionQuickEditDto({
      payee: formData.value.payee.trim(),
      memo: formData.value.memo.trim() || undefined,
      category: formData.value.category.trim() || undefined,
    })

    await transactionsClient.quickEditTransaction(
      selectedTransaction.value.key,
      currentTenantKey.value,
      dto,
    )
    await loadTransactions()
    showEditModal.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Update Failed', 'Failed to update transaction')
    showError.value = true
  } finally {
    loading.value = false
  }
}

async function deleteTransaction() {
  if (!selectedTransaction.value?.key || !currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await transactionsClient.deleteTransaction(
      selectedTransaction.value.key,
      currentTenantKey.value,
    )
    await loadTransactions()
    showDeleteModal.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Delete Failed', 'Failed to delete transaction')
    showError.value = true
  } finally {
    loading.value = false
  }
}

function navigateToDetails(key: string | undefined) {
  if (!key) return
  navigateTo(`/transactions/${key}`)
}

function clearFilters() {
  fromDate.value = ''
  toDate.value = ''
  loadTransactions()
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
      <WorkspaceSelector @change="loadTransactions" />
    </div>

    <!-- Main Content -->
    <div class="container py-4">
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h1 data-test-id="page-heading">Transactions</h1>
        <button
          v-if="canEditTransactions"
          class="btn btn-primary"
          data-test-id="new-transaction-button"
          :disabled="loading || !ready"
          @click="openCreateModal"
        >
          <FeatherIcon
            icon="plus"
            size="16"
            class="me-1"
          />
          New Transaction
        </button>
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
        Please select a workspace to view transactions
      </div>

      <!-- Error Display -->
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-4"
      />

      <!-- Date Range Filters -->
      <div
        v-if="hasWorkspace"
        class="card mb-4"
        data-test-id="date-range-filters"
      >
        <div class="card-body">
          <div class="row g-3">
            <div class="col-md-4">
              <label
                for="fromDate"
                class="form-label"
                >From Date</label
              >
              <input
                id="fromDate"
                v-model="fromDate"
                type="date"
                class="form-control"
                @change="loadTransactions"
              />
            </div>
            <div class="col-md-4">
              <label
                for="toDate"
                class="form-label"
                >To Date</label
              >
              <input
                id="toDate"
                v-model="toDate"
                type="date"
                class="form-control"
                @change="loadTransactions"
              />
            </div>
            <div class="col-md-4 d-flex align-items-end">
              <button
                class="btn btn-secondary"
                data-test-id="clear-filters-button"
                :disabled="loading || !ready"
                @click="clearFilters"
              >
                <!--
                  IMPORTANT: Keep :disabled="loading || !ready" on this button.
                  This is the ready signal for functional tests to detect client hydration.
                  The Clear Filters button is always present regardless of user permissions,
                  making it a reliable indicator that the page is interactive.
                  See: tests/Functional/NUXT-SSR-TESTING-PATTERN.md
                -->
                Clear Filters
              </button>
            </div>
          </div>
        </div>
      </div>

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
            data-test-id="loading-transactions-text"
            >Loading transactions...</small
          >
        </div>
      </div>

      <!-- Transactions Table -->
      <div
        v-else-if="hasWorkspace"
        class="card"
        data-test-id="transactions-card"
      >
        <div class="card-body">
          <div
            v-if="transactions.length === 0"
            class="text-center py-5 text-muted"
            data-test-id="empty-state"
          >
            <FeatherIcon
              icon="inbox"
              size="48"
              class="mb-3"
            />
            <p>No transactions found</p>
            <button
              v-if="canEditTransactions"
              class="btn btn-primary btn-sm"
              data-test-id="empty-state-create-button"
              @click="openCreateModal"
            >
              Create your first transaction
            </button>
          </div>

          <div
            v-else
            class="table-responsive"
          >
            <table
              class="table table-hover"
              data-test-id="transactions-table"
            >
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Payee</th>
                  <th class="text-end">Amount</th>
                  <th>Category</th>
                  <th>Memo</th>
                  <th
                    v-if="canEditTransactions"
                    class="text-end"
                  >
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="transaction in transactions"
                  :key="transaction.key"
                  :data-test-id="`transaction-row-${transaction.key}`"
                  class="clickable-row"
                  @click="navigateToDetails(transaction.key)"
                >
                  <td>{{ formatDate(transaction.date) }}</td>
                  <td>{{ transaction.payee }}</td>
                  <td class="text-end">{{ formatCurrency(transaction.amount) }}</td>
                  <td>{{ transaction.category || '' }}</td>
                  <td
                    class="memo-cell"
                    :title="transaction.memo || ''"
                  >
                    {{ transaction.memo || '' }}
                  </td>
                  <td
                    v-if="canEditTransactions"
                    class="text-end"
                  >
                    <button
                      class="btn btn-sm btn-outline-primary me-1"
                      title="Quick Edit"
                      data-test-id="edit-transaction-button"
                      @click.stop="openEditModal(transaction)"
                    >
                      <FeatherIcon
                        icon="edit"
                        size="14"
                      />
                    </button>
                    <button
                      class="btn btn-sm btn-outline-danger"
                      title="Delete"
                      data-test-id="delete-transaction-button"
                      @click.stop="openDeleteModal(transaction)"
                    >
                      <FeatherIcon
                        icon="trash-2"
                        size="14"
                      />
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Create Modal -->
    <ModalDialog
      v-model:show="showCreateModal"
      title="Create Transaction"
      :loading="loading"
      :primary-button-text="loading ? 'Creating...' : 'Create'"
      primary-button-test-id="create-submit-button"
      secondary-button-test-id="create-cancel-button"
      test-id="create-transaction-modal"
      @primary="createTransaction"
    >
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
        <small class="form-text text-muted"
          >Bank account this transaction came from (optional)</small
        >
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

    <!-- Edit Modal (Light Edit - Payee and Memo only) -->
    <ModalDialog
      v-model:show="showEditModal"
      title="Quick Edit Transaction"
      :loading="loading"
      :primary-button-text="loading ? 'Updating...' : 'Update'"
      primary-button-test-id="edit-submit-button"
      secondary-button-test-id="edit-cancel-button"
      test-id="edit-transaction-modal"
      @primary="updateTransaction"
    >
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
      <div class="alert alert-info">
        <small
          ><FeatherIcon
            icon="info"
            size="14"
            class="me-1"
          />Click on a transaction row to view and edit all details</small
        >
      </div>
    </ModalDialog>

    <!-- Delete Modal -->
    <ModalDialog
      v-model:show="showDeleteModal"
      title="Delete Transaction"
      :loading="loading"
      primary-button-variant="danger"
      :primary-button-text="loading ? 'Deleting...' : 'Delete'"
      primary-button-test-id="delete-submit-button"
      secondary-button-test-id="delete-cancel-button"
      test-id="delete-transaction-modal"
      @primary="deleteTransaction"
    >
      <p>Are you sure you want to delete this transaction?</p>
      <div
        v-if="selectedTransaction"
        class="alert alert-warning"
        data-test-id="delete-transaction-details"
      >
        <strong>{{ selectedTransaction.payee }}</strong
        ><br />
        {{ formatDate(selectedTransaction.date) }} -
        {{ formatCurrency(selectedTransaction.amount) }}
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

.table th {
  font-weight: 600;
}

.btn-sm {
  padding: 0.25rem 0.5rem;
  font-size: 0.875rem;
}

.memo-cell {
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.clickable-row {
  cursor: pointer;
}

.clickable-row:hover {
  background-color: #f8f9fa;
}
</style>
