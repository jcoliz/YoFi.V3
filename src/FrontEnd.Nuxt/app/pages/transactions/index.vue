<script setup lang="ts">
/**
 * Transactions Page
 *
 * Displays and manages transactions for the selected workspace.
 * Supports viewing, creating, quick editing, filtering by date range, and creating payee rules.
 * Delete functionality is restricted to the transaction details page only.
 */

import {
  TransactionsClient,
  PayeeMatchingRulesClient,
  TransactionEditDto,
  TransactionQuickEditDto,
  PayeeMatchingRuleEditDto,
  TenantRole,
  type PaginatedResultDtoOfTransactionResultDto,
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

/**
 * User preferences store for managing current workspace selection.
 */
const userPreferencesStore = useUserPreferencesStore()

/**
 * API client for transactions endpoint.
 */
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const transactionsClient = new TransactionsClient(baseUrl, authFetch)

/**
 * API client for payee matching rules endpoint.
 */
const payeeRulesClient = new PayeeMatchingRulesClient(baseUrl, authFetch)

/**
 * Whether the page is ready for interaction (signals client-side hydration is complete).
 */
const ready = ref(false)

/**
 * Paginated result containing transactions and metadata.
 */
const paginatedResult = ref<PaginatedResultDtoOfTransactionResultDto | undefined>(undefined)

/**
 * List of transactions from the current page.
 */
const transactions = computed(() => paginatedResult.value?.items || [])

/**
 * Whether a loading operation is in progress.
 */
const loading = ref(false)

/**
 * Current error from API operations.
 */
const error = ref<IProblemDetails | undefined>(undefined)

/**
 * Whether to display the error message.
 */
const showError = ref(false)

/**
 * Whether the create transaction modal is visible.
 */
const showCreateModal = ref(false)

/**
 * Whether the quick edit modal is visible.
 */
const showEditModal = ref(false)

/**
 * Whether the create rule dialog is visible.
 */
const showCreateRuleModal = ref(false)

/**
 * Currently selected transaction for edit or rule creation.
 */
const selectedTransaction = ref<TransactionResultDto | null>(null)

/**
 * Form data for create and edit operations.
 */
const formData = ref({
  date: '',
  amount: 0,
  payee: '',
  memo: '',
  category: '',
  source: '',
  externalId: '',
})

/**
 * Validation errors for form fields.
 */
const formErrors = ref({
  date: '',
  amount: '',
  payee: '',
  memo: '',
  category: '',
  source: '',
  externalId: '',
})

/**
 * Start date for filtering transactions (inclusive).
 */
const fromDate = ref<string>('')

/**
 * End date for filtering transactions (inclusive).
 */
const toDate = ref<string>('')

/**
 * Current page number for pagination.
 */
const currentPage = ref(1)

/**
 * The unique key of the currently selected workspace.
 */
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)

/**
 * Whether a workspace is currently selected.
 */
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)

/**
 * Whether the current user has permission to edit transactions.
 * Requires Editor or Owner role in the current workspace.
 */
const canEditTransactions = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})

/**
 * Watch for workspace changes and reload transactions.
 * Clears transaction list when no workspace is selected.
 */
watch(currentTenantKey, async (newKey) => {
  if (newKey) {
    await loadTransactions()
  } else {
    paginatedResult.value = undefined
  }
})

/**
 * Initialize page on mount by setting ready state and loading transactions.
 */
onMounted(async () => {
  ready.value = true
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value) {
    await loadTransactions()
  }
})

/**
 * Loads transactions for the current workspace with optional date range filters.
 * Displays validation error if no workspace is selected.
 *
 * @param pageNumber - The page number to load (defaults to 1)
 */
async function loadTransactions(pageNumber: number = 1) {
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

    paginatedResult.value = await transactionsClient.getTransactions(
      pageNumber,
      from,
      to,
      currentTenantKey.value,
    )
    currentPage.value = pageNumber
  } catch (err) {
    error.value = handleApiError(err, 'Load Failed', 'Failed to load transactions')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Handles page change events from the pagination component.
 *
 * @param pageNumber - The new page number to load
 */
function handlePageChange(pageNumber: number) {
  loadTransactions(pageNumber)
}

/**
 * Opens the create transaction modal with default values.
 * Initializes form data with current date and clears any validation errors.
 */
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

/**
 * Opens the quick edit modal for a transaction.
 * Populates form with transaction's payee, memo, and category (quick editable fields).
 *
 * @param transaction - The transaction to edit
 */
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

/**
 * Opens the payee rule creation dialog pre-populated with transaction's payee.
 *
 * @param transaction - The transaction to create a rule from
 */
function openCreateRuleModal(transaction: TransactionResultDto) {
  selectedTransaction.value = transaction
  showCreateRuleModal.value = true
}

/**
 * Validates the create transaction form.
 * Checks required fields and validates field length constraints.
 *
 * @returns True if form is valid, false otherwise
 */
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

/**
 * Creates a new transaction in the current workspace.
 * Validates form data, calls API, and reloads transactions on success.
 */
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

/**
 * Updates a transaction using the quick edit endpoint.
 * Only updates payee, memo, and category fields.
 * Validates form data, calls API, and reloads transactions on success.
 */
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

/**
 * Navigates to the transaction details page.
 *
 * @param key - The unique key of the transaction
 */
function navigateToDetails(key: string | undefined) {
  if (!key) return
  navigateTo(`/transactions/${key}`)
}

/**
 * Clears date range filters and reloads transactions from page 1.
 */
function clearFilters() {
  fromDate.value = ''
  toDate.value = ''
  loadTransactions(1)
}

/**
 * Formats a date for display using the user's locale.
 *
 * @param date - The date to format
 * @returns Formatted date string or empty string if undefined
 */
function formatDate(date: Date | undefined): string {
  if (!date) return ''
  return new Date(date).toLocaleDateString()
}

/**
 * Formats a number as USD currency.
 *
 * @param amount - The amount to format
 * @returns Formatted currency string (e.g., "$123.45")
 */
function formatCurrency(amount: number | undefined): string {
  if (amount === undefined) return '$0.00'
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(amount)
}

/**
 * Creates a payee matching rule from a transaction.
 * If the rule has a category, also updates the source transaction with that category.
 * Reloads transactions on success and closes the dialog.
 *
 * @param rule - The payee matching rule data to create
 */
async function createRuleFromTransaction(rule: PayeeMatchingRuleEditDto) {
  if (!currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await payeeRulesClient.createRule(currentTenantKey.value, rule)

    // If category was provided/modified, also update the source transaction
    if (rule.category && rule.category.trim()) {
      if (selectedTransaction.value?.key) {
        const updateDto = new TransactionQuickEditDto({
          payee: selectedTransaction.value.payee || '',
          memo: selectedTransaction.value.memo,
          category: rule.category.trim(),
        })

        await transactionsClient.quickEditTransaction(
          selectedTransaction.value.key,
          currentTenantKey.value,
          updateDto,
        )
      }
    }

    await loadTransactions()
    showCreateRuleModal.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Create Rule Failed', 'Failed to create payee matching rule')
    showError.value = true
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div>
    <!-- Workspace Selector -->
    <div class="workspace-selector-container">
      <WorkspaceSelector @change="() => loadTransactions(1)" />
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
                @change="() => loadTransactions(1)"
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
                @change="() => loadTransactions(1)"
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
                  <th data-test-id="date">Date</th>
                  <th data-test-id="payee">Payee</th>
                  <th
                    data-test-id="amount"
                    class="text-end"
                  >
                    Amount
                  </th>
                  <th data-test-id="category">Category</th>
                  <th data-test-id="memo">Memo</th>
                  <th
                    v-if="canEditTransactions"
                    class="text-end"
                    data-test-id="actions"
                  >
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="transaction in transactions"
                  :key="transaction.key"
                  :data-test-id="`row-${transaction.key}`"
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
                      class="btn btn-sm btn-outline-success ms-1"
                      title="Create Rule"
                      data-test-id="create-rule-button"
                      @click.stop="openCreateRuleModal(transaction)"
                    >
                      <FeatherIcon
                        icon="zap"
                        size="14"
                      />
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>

            <!-- Pagination -->
            <PaginationBar
              v-if="paginatedResult?.metadata"
              :page-info="paginatedResult.metadata"
              class="mt-3"
              @page-updated="handlePageChange"
            />
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

    <!-- Edit Modal (Quick Edit - Payee, Category, and Memo) -->
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
          @click="navigateToDetails(selectedTransaction?.key)"
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
          @click="showEditModal = false"
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

    <!-- Create Rule from Transaction Dialog -->
    <PayeeRuleDialog
      v-model:show="showCreateRuleModal"
      mode="create"
      :loading="loading"
      :initial-payee-pattern="selectedTransaction?.payee || ''"
      :initial-category="selectedTransaction?.category || ''"
      :initial-is-regex="false"
      @save="createRuleFromTransaction"
      @cancel="showCreateRuleModal = false"
    />
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
