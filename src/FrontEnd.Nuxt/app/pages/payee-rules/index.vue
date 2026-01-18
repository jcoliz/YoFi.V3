<script setup lang="ts">
/**
 * Payee Rules Management Page
 *
 * Allows users to view, search, sort, create, edit, and delete payee matching rules
 */

import {
  PayeeMatchingRulesClient,
  PayeeMatchingRuleEditDto,
  PayeeRuleSortBy,
  TenantRole,
  type PayeeMatchingRuleResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'
import PayeeRuleSortSelector from '~/components/payee-rules/PayeeRuleSortSelector.vue'

definePageMeta({
  title: 'Rules',
  order: 4,
  auth: true,
  layout: 'chrome',
})

/**
 * User preferences store for managing current tenant/workspace selection.
 */
const userPreferencesStore = useUserPreferencesStore()

/**
 * Base URL for API requests.
 */
const { baseUrl } = useApiBaseUrl()

/**
 * Authenticated fetch function for API calls.
 */
const authFetch = useAuthFetch()

/**
 * API client for payee matching rule operations.
 */
const payeeRulesClient = new PayeeMatchingRulesClient(baseUrl, authFetch)

/**
 * Page ready state for SSR/hydration.
 * Prevents hydration mismatches by deferring rendering until mounted.
 */
const ready = ref(false)

/**
 * List of payee matching rules loaded from the API.
 */
const rules = ref<PayeeMatchingRuleResultDto[]>([])

/**
 * Whether a loading operation is in progress.
 */
const loading = ref(false)

/**
 * Error details from failed API operations.
 */
const error = ref<IProblemDetails | undefined>(undefined)

/**
 * Whether to display the error message.
 */
const showError = ref(false)

/**
 * Whether the create rule dialog is visible.
 */
const showCreateDialog = ref(false)

/**
 * Whether the edit rule dialog is visible.
 */
const showEditDialog = ref(false)

/**
 * Whether the delete confirmation dialog is visible.
 */
const showDeleteDialog = ref(false)

/**
 * The rule currently selected for editing or deletion.
 */
const selectedRule = ref<PayeeMatchingRuleResultDto | null>(null)

/**
 * Search text filter for rules.
 */
const searchText = ref('')

/**
 * Sort order for rules list.
 */
const sortBy = ref<PayeeRuleSortBy>(PayeeRuleSortBy.PayeePattern)

/**
 * Pagination metadata from the last API response.
 */
const paginationMetadata = ref<any>(null)

/**
 * Number of rules to display per page.
 */
const pageSize = ref(50)

/**
 * Current page number (1-based).
 */
const requestedPage = ref(1)

/**
 * Total count of rules matching the current filters.
 *
 * @returns Total count from pagination metadata or 0
 */
const totalCount = computed(() => paginationMetadata.value?.totalCount || 0)

/**
 * Current tenant/workspace key from user preferences.
 *
 * @returns Tenant key or undefined if no workspace selected
 */
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)

/**
 * Whether a workspace is currently selected.
 *
 * @returns True if user has selected a workspace
 */
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)

/**
 * Whether the current user has permission to edit rules.
 * Requires Editor or Owner role in the current workspace.
 *
 * @returns True if user can create, edit, or delete rules
 */
const canEditRules = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})

/**
 * Watch for workspace changes and reload rules.
 * Clears rules list when no workspace is selected.
 */
watch(currentTenantKey, async (newKey) => {
  if (newKey) {
    requestedPage.value = 1
    await loadRules()
  } else {
    rules.value = []
  }
})

/**
 * Load user preferences and initial rules on component mount.
 */
onMounted(async () => {
  ready.value = true
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value) {
    await loadRules()
  }
})

/**
 * Loads payee matching rules from the API.
 * Applies current search, sort, and pagination settings.
 */
async function loadRules() {
  if (!currentTenantKey.value) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to view payee matching rules',
    }
    showError.value = true
    return
  }

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    const result = await payeeRulesClient.getRules(
      requestedPage.value,
      pageSize.value,
      sortBy.value,
      searchText.value || null,
      currentTenantKey.value,
    )

    rules.value = result.items || []
    paginationMetadata.value = result.metadata || null
  } catch (err) {
    error.value = handleApiError(err, 'Load Failed', 'Failed to load payee matching rules')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Opens the create rule dialog.
 */
function openCreateDialog() {
  showCreateDialog.value = true
}

/**
 * Opens the edit rule dialog with the specified rule's data.
 *
 * @param rule - The rule to edit
 */
function openEditDialog(rule: PayeeMatchingRuleResultDto) {
  selectedRule.value = rule
  showEditDialog.value = true
}

/**
 * Opens the delete confirmation dialog for the specified rule.
 *
 * @param rule - The rule to delete
 */
function openDeleteDialog(rule: PayeeMatchingRuleResultDto) {
  selectedRule.value = rule
  showDeleteDialog.value = true
}

/**
 * Creates a new payee matching rule.
 * Reloads the rules list on success and closes the dialog.
 *
 * @param rule - The rule data to create
 */
async function createRule(rule: PayeeMatchingRuleEditDto) {
  if (!currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await payeeRulesClient.createRule(currentTenantKey.value, rule)
    await loadRules()
    showCreateDialog.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Create Failed', 'Failed to create payee matching rule')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Updates an existing payee matching rule.
 * Reloads the rules list on success and closes the dialog.
 *
 * @param rule - The updated rule data
 */
async function updateRule(rule: PayeeMatchingRuleEditDto) {
  if (!selectedRule.value?.key || !currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await payeeRulesClient.updateRule(selectedRule.value.key, currentTenantKey.value, rule)
    await loadRules()
    showEditDialog.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Update Failed', 'Failed to update payee matching rule')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Deletes the currently selected payee matching rule.
 * Reloads the rules list on success and closes the dialog.
 */
async function deleteRule() {
  if (!selectedRule.value?.key || !currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await payeeRulesClient.deleteRule(selectedRule.value.key, currentTenantKey.value)
    await loadRules()
    showDeleteDialog.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Delete Failed', 'Failed to delete payee matching rule')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Handles the search button click.
 * Resets to page 1 and reloads rules with the current search text.
 */
function handleSearch() {
  requestedPage.value = 1
  loadRules()
}

/**
 * Clears the search text and reloads all rules.
 * Resets to page 1.
 */
function clearSearch() {
  searchText.value = ''
  requestedPage.value = 1
  loadRules()
}

/**
 * Handles sort order changes.
 * Resets to page 1 and reloads rules with the new sort order.
 */
function handleSortChange() {
  requestedPage.value = 1
  loadRules()
}

/**
 * Handles pagination page changes.
 *
 * @param page - The page number to load (1-based)
 */
function handlePageChange(page: number) {
  requestedPage.value = page
  loadRules()
}

/**
 * Formats a date as a relative time string (e.g., "2 days ago", "Never").
 *
 * @param date - The date to format
 * @returns Human-readable relative time string
 */
function formatDate(date: Date | undefined): string {
  if (!date) return 'Never'
  const d = new Date(date)
  const now = new Date()
  const diffMs = now.getTime() - d.getTime()
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24))

  if (diffDays === 0) return 'Today'
  if (diffDays === 1) return 'Yesterday'
  if (diffDays < 7) return `${diffDays} days ago`
  if (diffDays < 30) return `${Math.floor(diffDays / 7)} weeks ago`
  if (diffDays < 365) return `${Math.floor(diffDays / 30)} months ago`
  return `${Math.floor(diffDays / 365)} years ago`
}
</script>

<template>
  <div>
    <!-- Workspace Selector -->
    <div class="workspace-selector-container">
      <WorkspaceSelector @change="loadRules" />
    </div>

    <!-- Main Content -->
    <div class="container py-4">
      <div class="d-flex justify-content-between align-items-center mb-4">
        <div>
          <h1 data-test-id="page-heading">Payee Matching Rules</h1>
        </div>
        <button
          v-if="canEditRules"
          class="btn btn-primary"
          data-test-id="new-button"
          :disabled="loading || !ready"
          @click="openCreateDialog"
        >
          <FeatherIcon
            icon="plus"
            size="16"
            class="me-1"
          />
          New Rule
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
        Please select a workspace to view payee matching rules
      </div>

      <!-- Error Display -->
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-4"
      />

      <!-- Search and Filter Bar -->
      <div
        v-if="hasWorkspace"
        class="card mb-4"
      >
        <div class="card-body">
          <label
            for="searchText"
            class="form-label"
            >Search</label
          >
          <div class="d-flex gap-2">
            <div class="input-group flex-grow-1">
              <input
                id="searchText"
                v-model="searchText"
                type="text"
                class="form-control"
                placeholder="Search payee or category..."
                data-test-id="search-input"
                @keyup.enter="handleSearch"
              />
              <button
                class="btn btn-outline-secondary"
                data-test-id="search-button"
                :disabled="loading || !ready"
                @click="handleSearch"
              >
                <FeatherIcon
                  icon="search"
                  size="16"
                />
              </button>
              <button
                v-if="searchText"
                class="btn btn-outline-secondary"
                data-test-id="clear-search-button"
                :disabled="loading || !ready"
                @click="clearSearch"
              >
                <FeatherIcon
                  icon="x"
                  size="16"
                />
              </button>
            </div>
            <!-- Sort Selector Component -->
            <PayeeRuleSortSelector
              v-model="sortBy"
              :disabled="loading || !ready"
              @update:model-value="handleSortChange"
            />
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
            data-test-id="loading-rules-text"
            >Loading rules...</small
          >
        </div>
      </div>

      <!-- Rules Table -->
      <div
        v-else-if="hasWorkspace"
        class="card"
        data-test-id="rules-card"
      >
        <div class="card-body">
          <!-- Empty State -->
          <div
            v-if="rules.length === 0 && !searchText"
            class="text-center py-5 text-muted"
            data-test-id="empty-state"
          >
            <FeatherIcon
              icon="inbox"
              size="48"
              class="mb-3"
            />
            <p>You haven't created any payee matching rules yet</p>
            <button
              v-if="canEditRules"
              class="btn btn-primary btn-sm"
              data-test-id="empty-state-create-button"
              @click="openCreateDialog"
            >
              Create your first rule
            </button>
          </div>

          <!-- No Search Results -->
          <div
            v-else-if="rules.length === 0 && searchText"
            class="text-center py-5 text-muted"
            data-test-id="no-results"
          >
            <FeatherIcon
              icon="search"
              size="48"
              class="mb-3"
            />
            <p>No rules match your search</p>
            <p class="small">Search term: "{{ searchText }}"</p>
            <button
              class="btn btn-secondary btn-sm"
              @click="clearSearch"
            >
              Clear search
            </button>
          </div>

          <!-- Rules Table -->
          <div
            v-else
            class="table-responsive"
          >
            <table
              class="table table-hover"
              data-test-id="payee-rules"
            >
              <thead>
                <tr>
                  <th data-test-id="payee-pattern">Payee Pattern</th>
                  <th data-test-id="category">Category</th>
                  <th data-test-id="last-used">Last Used</th>
                  <th
                    data-test-id="match-count"
                    class="text-end"
                  >
                    Matches
                  </th>
                  <th
                    v-if="canEditRules"
                    class="text-end"
                    data-test-id="actions"
                  >
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="rule in rules"
                  :key="rule.key"
                  :data-test-id="`row-${rule.key}`"
                >
                  <td>
                    {{ rule.payeePattern }}
                    <span
                      v-if="rule.payeeIsRegex"
                      class="badge bg-secondary ms-2"
                      data-test-id="regex-badge"
                      >Regex</span
                    >
                  </td>
                  <td>{{ rule.category }}</td>
                  <td>{{ formatDate(rule.lastUsedAt || undefined) }}</td>
                  <td class="text-end">{{ rule.matchCount || 0 }}</td>
                  <td
                    v-if="canEditRules"
                    class="text-end"
                  >
                    <button
                      class="btn btn-sm btn-outline-primary me-1"
                      title="Edit"
                      data-test-id="edit-button"
                      @click="openEditDialog(rule)"
                    >
                      <FeatherIcon
                        icon="edit"
                        size="14"
                      />
                    </button>
                    <button
                      class="btn btn-sm btn-outline-danger"
                      title="Delete"
                      data-test-id="delete-button"
                      @click="openDeleteDialog(rule)"
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

          <!-- Pagination -->
          <PaginationBar
            v-if="paginationMetadata"
            :page-info="paginationMetadata"
            @page-updated="handlePageChange"
          />
        </div>
      </div>
    </div>

    <!-- Create Dialog -->
    <PayeeRuleDialog
      v-model:show="showCreateDialog"
      mode="create"
      :loading="loading"
      @save="createRule"
    />

    <!-- Edit Dialog -->
    <PayeeRuleDialog
      v-if="selectedRule"
      v-model:show="showEditDialog"
      mode="edit"
      :loading="loading"
      :initial-payee-pattern="selectedRule.payeePattern"
      :initial-category="selectedRule.category"
      :initial-is-regex="selectedRule.payeeIsRegex"
      :rule-key="selectedRule.key"
      @save="updateRule"
    />

    <!-- Delete Confirmation Dialog -->
    <ModalDialog
      v-model:show="showDeleteDialog"
      title="Delete Payee Matching Rule"
      :loading="loading"
      primary-button-variant="danger"
      :primary-button-text="loading ? 'Deleting...' : 'Delete'"
      primary-button-test-id="delete-submit-button"
      secondary-button-test-id="delete-cancel-button"
      test-id="delete-rule-modal"
      @primary="deleteRule"
    >
      <p>Are you sure you want to delete this payee matching rule? This action cannot be undone.</p>
      <div
        v-if="selectedRule"
        class="alert alert-warning"
        data-test-id="delete-rule-details"
      >
        <strong>{{ selectedRule.payeePattern }}</strong
        ><br />
        Category: {{ selectedRule.category }}
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

.badge {
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
}
</style>
