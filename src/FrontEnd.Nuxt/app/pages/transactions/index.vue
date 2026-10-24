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
  TransactionQuickEditDto,
  type PayeeMatchingRuleEditDto,
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
  layout: 'chrome',
  name: 'transactions',
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
 * This page requires an authenticated user. If not authenticated, redirect to login page.
 * Only after we've confirmed authentication, do we call getData() to load the data for this page.
 */
useAuthenticationGuard().requireAuthenticatedUser(afterMounted)

/**
 * Initialize page on mount by setting ready state and loading transactions.
 */
async function afterMounted() {
  ready.value = true
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value) {
    await loadTransactions()
  }
}

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
 * Opens the quick edit modal for a transaction.
 *
 * @param transaction - The transaction to edit
 */
function openEditModal(transaction: TransactionResultDto) {
  selectedTransaction.value = transaction
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
          @click="showCreateModal = true"
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
              @click="showCreateModal = true"
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
    <TransactionCreateModal
      v-model:show="showCreateModal"
      @created="loadTransactions()"
    />

    <!-- Edit Modal (Quick Edit - Payee, Category, and Memo) -->
    <TransactionEditModal
      v-model:show="showEditModal"
      :transaction="selectedTransaction"
      @updated="loadTransactions()"
    />

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
