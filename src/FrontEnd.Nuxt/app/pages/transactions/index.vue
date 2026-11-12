<script setup lang="ts">
/**
 * Transactions Page
 *
 * Displays and manages transactions for the selected workspace.
 * Supports viewing, creating, quick editing, searching, advanced filtering, and creating payee rules.
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
import { useTransactionSearchStore } from '~/stores/transactionSearch'
import { handleApiError } from '~/utils/errorHandler'
import { formatDateOnly, fromDateInputValue } from '~/utils/dateOnly'
import type { TransactionAdvancedFilters } from '~/types/transactionFilters'

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
const transactionsClient = useApiClient(TransactionsClient)

/**
 * API client for payee matching rules endpoint.
 */
const payeeRulesClient = useApiClient(PayeeMatchingRulesClient)

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
 * Search term, filter criteria, and current page for transactions.
 * Kept in a store so they survive navigating to transaction details and back.
 */
const transactionSearchStore = useTransactionSearchStore()
const { searchText, filters, showFilters, currentPage, hasActiveFilters } =
  storeToRefs(transactionSearchStore)
const searchDraft = ref(searchText.value)
const filterPanel = ref<{
  validateAndCommit: (options?: {
    emit?: boolean
    focusInvalid?: boolean
  }) => TransactionAdvancedFilters | null
  resetDraft: () => void
} | null>(null)
let requestVersion = 0
const retryPageNumber = ref(1)

watch(searchText, (value) => {
  searchDraft.value = value
})

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
 * Whether the current user has permission to manage the workspace.
 * Requires Owner role in the current workspace.
 */
const canManageWorkspace = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Owner
})

/**
 * Watch for workspace changes and reload transactions.
 * Clears transaction list when no workspace is selected.
 */
watch(currentTenantKey, async (newKey) => {
  requestVersion++
  paginatedResult.value = undefined
  error.value = undefined
  showError.value = false
  loading.value = false
  filterPanel.value?.resetDraft()
  currentPage.value = 1
  if (newKey) {
    await loadTransactions(1)
  }
})

onBeforeUnmount(() => {
  requestVersion++
})

/**
 * This page requires an authenticated user. If not authenticated, redirect to login page.
 * Only after we've confirmed authentication, do we load transactions for this page.
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
 * Loads transactions using a snapshot of the applied query and workspace.
 *
 * @param pageNumber - The page number to load (defaults to the retained current page)
 */
async function loadTransactions(pageNumber: number = currentPage.value) {
  const tenantKey = currentTenantKey.value
  if (!tenantKey) return

  const requestId = ++requestVersion
  retryPageNumber.value = pageNumber
  const appliedFilters = { ...filters.value }
  const appliedSearch = searchText.value || undefined
  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    const result = await transactionsClient.getTransactions(
      pageNumber,
      appliedFilters.fromDate ? fromDateInputValue(appliedFilters.fromDate) : undefined,
      appliedFilters.toDate ? fromDateInputValue(appliedFilters.toDate) : undefined,
      appliedSearch,
      appliedFilters.payee || undefined,
      appliedFilters.category || undefined,
      appliedFilters.memo || undefined,
      appliedFilters.amount || undefined,
      appliedFilters.uncategorizedOnly,
      tenantKey,
    )
    if (requestId !== requestVersion) return
    paginatedResult.value = result
    currentPage.value = pageNumber
  } catch (err) {
    if (requestId !== requestVersion) return
    error.value = handleApiError(err, 'Load Failed', 'Failed to load transactions')
    showError.value = true
  } finally {
    if (requestId === requestVersion) loading.value = false
  }
}

function sameFilters(left: TransactionAdvancedFilters, right: TransactionAdvancedFilters): boolean {
  return (
    left.fromDate === right.fromDate &&
    left.toDate === right.toDate &&
    left.payee === right.payee &&
    left.category === right.category &&
    left.memo === right.memo &&
    left.amount === right.amount &&
    left.uncategorizedOnly === right.uncategorizedOnly
  )
}

function commitPanelDraft(): boolean | null {
  const nextFilters = filterPanel.value?.validateAndCommit({
    emit: false,
    focusInvalid: true,
  })
  if (!nextFilters) return filterPanel.value ? null : false
  if (sameFilters(nextFilters, filters.value)) return false
  transactionSearchStore.setFilters(nextFilters)
  return true
}

function handleFilterCommit(nextFilters: TransactionAdvancedFilters) {
  if (loading.value || sameFilters(nextFilters, filters.value)) return
  transactionSearchStore.setFilters(nextFilters)
  void loadTransactions(1)
}

function handleSearch(value: string) {
  const changedFilters = commitPanelDraft()
  if (changedFilters === null) return
  searchText.value = value
  currentPage.value = 1
  void loadTransactions(1)
}

function handleClearSearch() {
  const changedFilters = commitPanelDraft()
  if (changedFilters === null) return
  transactionSearchStore.clearSearch()
  searchDraft.value = ''
  currentPage.value = 1
  void loadTransactions(1)
}

function handleFiltersToggle() {
  if (!showFilters.value) {
    showFilters.value = true
    return
  }

  const changedFilters = commitPanelDraft()
  if (changedFilters === null) return
  showFilters.value = false
  if (changedFilters) void loadTransactions(1)
}

function handleClearAll() {
  transactionSearchStore.clearAll()
  showFilters.value = true
  searchDraft.value = ''
  filterPanel.value?.resetDraft()
  void loadTransactions(1)
}

/**
 * Handles page change events from the pagination component.
 *
 * @param pageNumber - The new page number to load
 */
function handlePageChange(pageNumber: number) {
  const changedFilters = commitPanelDraft()
  if (changedFilters === null) return
  void loadTransactions(changedFilters ? 1 : pageNumber)
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
 * If the rule has a category, also updates the source transaction unless it has multiple splits.
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
    if (!selectedTransaction.value?.hasMultipleSplits && rule.category && rule.category.trim()) {
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

function retryLoadTransactions() {
  void loadTransactions(retryPageNumber.value)
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

      <!-- Search Bar -->
      <SearchFilterBar
        v-if="hasWorkspace"
        v-model="searchDraft"
        placeholder="Search payee, memo, category, or amount..."
        :disabled="loading || !ready"
        @search="handleSearch"
        @clear="handleClearSearch"
      >
        <!--
          IMPORTANT: Keep :disabled="loading || !ready" on this button.
          This is the ready signal for functional tests to detect client hydration.
          The filters toggle is always present regardless of user permissions,
          making it a reliable indicator that the page is interactive.
          See: tests/Functional/NUXT-SSR-TESTING-PATTERN.md
        -->
        <button
          class="btn"
          :class="hasActiveFilters ? 'btn-secondary' : 'btn-outline-secondary'"
          type="button"
          title="Filters"
          aria-label="Filters"
          data-test-id="filters-toggle-button"
          aria-controls="date-range-filters"
          :aria-expanded="showFilters"
          :disabled="loading || !ready"
          @click="handleFiltersToggle"
        >
          <FeatherIcon
            icon="filter"
            size="16"
          />
        </button>
      </SearchFilterBar>

      <TransactionFilterPanel
        v-if="hasWorkspace"
        ref="filterPanel"
        :expanded="showFilters"
        :disabled="loading || !ready"
        :filters="filters"
        @commit="handleFilterCommit"
        @clear-all="handleClearAll"
      />

      <div
        v-if="!loading && !showError && paginatedResult"
        class="small text-muted mb-2"
        role="status"
        aria-live="polite"
        data-test-id="transaction-result-count"
      >
        {{ paginatedResult.metadata?.totalCount ?? 0 }} transactions found.
      </div>

      <div
        v-if="showError"
        class="mb-3"
      >
        <button
          type="button"
          class="btn btn-outline-primary btn-sm"
          data-test-id="retry-transactions-button"
          :disabled="loading || !ready"
          @click="retryLoadTransactions"
        >
          Retry
        </button>
      </div>

      <!-- Loading State -->
      <div
        v-if="loading && !paginatedResult"
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
        class="card position-relative"
        data-test-id="transactions-card"
        :aria-busy="loading"
      >
        <!-- Keep the table's height while loading so pagination does not collapse the scroll position. -->
        <div
          v-if="loading"
          class="position-absolute top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center bg-body bg-opacity-75 z-1"
          data-test-id="loading-state"
        >
          <div class="text-center">
            <BaseSpinner />
            <div class="mt-2">
              <small
                class="text-muted"
                data-test-id="loading-transactions-text"
                >Loading transactions...</small
              >
            </div>
          </div>
        </div>
        <div
          class="card-body"
          :inert="loading"
        >
          <div
            v-if="transactions.length === 0 && !searchText && !hasActiveFilters"
            class="text-center py-5 text-muted"
            data-test-id="empty-state"
          >
            <FeatherIcon
              icon="inbox"
              size="48"
              class="mb-3"
            />
            <p>No transactions found</p>
            <div v-if="canManageWorkspace">
              <p>You can add sample data to get started</p>
              <button
                class="btn btn-primary btn-sm mb-3"
                data-test-id="empty-state-manage-workspace"
                @click="navigateTo(`/workspaces/${currentTenantKey}`)"
              >
                Manage your workspace
              </button>
              <p>Or you can create a new transaction</p>
            </div>
            <button
              class="btn btn-primary btn-sm"
              data-test-id="empty-state-create-button"
              @click="showCreateModal = true"
            >
              Create your first transaction
            </button>
          </div>

          <!-- No Filter Results -->
          <div
            v-else-if="transactions.length === 0"
            class="text-center py-5 text-muted"
            data-test-id="no-results"
          >
            <FeatherIcon
              icon="search"
              size="48"
              class="mb-3"
            />
            <p>No transactions match your filters.</p>
            <p class="small">Try adjusting the filters or use Clear All.</p>
            <button
              class="btn btn-secondary btn-sm"
              data-test-id="no-results-clear-all-button"
              @click="handleClearAll"
            >
              Clear All
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
                  <td>{{ formatDateOnly(transaction.date) }}</td>
                  <td>{{ transaction.payee }}</td>
                  <td class="text-end">{{ formatCurrency(transaction.amount) }}</td>
                  <td>
                    <span
                      v-if="transaction.hasMultipleSplits"
                      class="badge text-bg-secondary"
                      title="Allocated across multiple splits."
                      data-test-id="transaction-split-badge"
                    >
                      Split
                    </span>
                    <template v-else>{{ transaction.category || '' }}</template>
                  </td>
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
    <PayeeRulesDialog
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
