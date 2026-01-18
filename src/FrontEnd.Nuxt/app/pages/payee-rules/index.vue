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

definePageMeta({
  title: 'Payee Matching Rules',
  order: 4,
  auth: true,
  layout: 'chrome',
})

const userPreferencesStore = useUserPreferencesStore()

// API Client
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const payeeRulesClient = new PayeeMatchingRulesClient(baseUrl, authFetch)

// Page ready state (for SSR/hydration)
const ready = ref(false)

// State
const rules = ref<PayeeMatchingRuleResultDto[]>([])
const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const showCreateDialog = ref(false)
const showEditDialog = ref(false)
const showDeleteDialog = ref(false)
const selectedRule = ref<PayeeMatchingRuleResultDto | null>(null)

// Search and sort state
const searchText = ref('')
const sortBy = ref<PayeeRuleSortBy>(PayeeRuleSortBy.PayeePattern)

// Pagination state
const paginationMetadata = ref<any>(null)
const pageSize = ref(50)
const requestedPage = ref(1)
const totalCount = computed(() => paginationMetadata.value?.totalCount || 0)

// Computed
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)
const canEditRules = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})

// Watch for workspace changes
watch(currentTenantKey, async (newKey) => {
  if (newKey) {
    requestedPage.value = 1
    await loadRules()
  } else {
    rules.value = []
  }
})

// Load rules on mount
onMounted(async () => {
  ready.value = true
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value) {
    await loadRules()
  }
})

// Methods
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

function openCreateDialog() {
  showCreateDialog.value = true
}

function openEditDialog(rule: PayeeMatchingRuleResultDto) {
  selectedRule.value = rule
  showEditDialog.value = true
}

function openDeleteDialog(rule: PayeeMatchingRuleResultDto) {
  selectedRule.value = rule
  showDeleteDialog.value = true
}

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

function handleSearch() {
  requestedPage.value = 1
  loadRules()
}

function clearSearch() {
  searchText.value = ''
  requestedPage.value = 1
  loadRules()
}

function handleSortChange() {
  requestedPage.value = 1
  loadRules()
}

function handlePageChange(page: number) {
  requestedPage.value = page
  loadRules()
}

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
          <p
            v-if="totalCount > 0"
            class="text-muted"
            data-test-id="total-rules-count"
          >
            {{ totalCount }} {{ totalCount === 1 ? 'rule' : 'rules' }}
          </p>
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
          <div class="row g-3">
            <div class="col-md-6">
              <label
                for="searchText"
                class="form-label"
                >Search</label
              >
              <div class="input-group">
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
            </div>
            <div class="col-md-6">
              <label
                for="sortBy"
                class="form-label"
                >Sort By</label
              >
              <select
                id="sortBy"
                v-model="sortBy"
                class="form-select"
                data-test-id="sort-dropdown"
                :disabled="loading || !ready"
                @change="handleSortChange"
              >
                <option :value="PayeeRuleSortBy.PayeePattern">Payee Pattern (A-Z)</option>
                <option :value="PayeeRuleSortBy.Category">Category (A-Z)</option>
                <option :value="PayeeRuleSortBy.LastUsedAt">Last Used</option>
              </select>
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
            v-if="paginationMetadata && paginationMetadata.totalPages > 1"
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
