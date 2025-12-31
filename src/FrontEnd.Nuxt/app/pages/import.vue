<script setup lang="ts">
/**
 * Import Page
 *
 * Allows users to upload OFX/QFX bank files and review/import transactions
 * Requires Editor or Owner role in the current workspace
 */

import {
  ImportClient,
  TenantRole,
  DuplicateStatus,
  SetSelectionRequest,
  type PaginatedResultDtoOfImportReviewTransactionDto,
  type ImportReviewUploadDto,
  type ImportReviewSummaryDto,
  type IProblemDetails,
  type FileParameter,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Import',
  order: 4,
  auth: true,
  layout: 'chrome',
})

const userPreferencesStore = useUserPreferencesStore()

// API Client
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const importClient = new ImportClient(baseUrl, authFetch)

// Page ready state (for SSR/hydration)
const ready = ref(false)

// State - File Upload
const selectedFiles = ref<File[]>([])
const uploadInProgress = ref(false)
const statusMessages = ref<string[]>([])
const showStatusPane = ref(false)
const statusVariant = ref<'info' | 'warning' | 'success' | 'danger'>('info')

// State - Transaction Review
const paginatedResult = ref<PaginatedResultDtoOfImportReviewTransactionDto | undefined>(undefined)
const transactions = computed(() => paginatedResult.value?.items || [])
const summary = ref<ImportReviewSummaryDto | undefined>(undefined)
const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)

// State - Modals
const showConfirmModal = ref(false)
const showDeleteModal = ref(false)
const importing = ref(false)

// State - Pagination
const currentPage = ref(1)
const pageSize = ref(50)

// Computed - Workspace and Permissions
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)
const currentTenantName = computed(() => userPreferencesStore.getCurrentTenant?.name || '')
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)
const canImport = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})

// Computed - Transaction States
const hasTransactions = computed(() => transactions.value.length > 0)
const hasSelections = computed(() => (summary.value?.selectedCount ?? 0) > 0)
const hasPotentialDuplicates = computed(() => {
  return transactions.value.some((t) => t.duplicateStatus === DuplicateStatus.PotentialDuplicate)
})

// Watch for workspace changes
watch(currentTenantKey, async (newKey) => {
  if (newKey) {
    await loadPendingReview()
    await loadSummary()
  } else {
    paginatedResult.value = undefined
    summary.value = undefined
  }
})

// Load on mount
onMounted(async () => {
  ready.value = true
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value && canImport.value) {
    await loadPendingReview()
    await loadSummary()
  }
})

/**
 * Load pending review transactions from API (paginated)
 */
async function loadPendingReview(pageNumber: number = 1) {
  if (!currentTenantKey.value) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to import transactions',
    }
    showError.value = true
    return
  }

  if (!canImport.value) {
    return
  }

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    paginatedResult.value = await importClient.getPendingReview(
      pageNumber,
      pageSize.value,
      currentTenantKey.value,
    )
    currentPage.value = pageNumber
  } catch (err) {
    error.value = handleApiError(err, 'Load Failed', 'Failed to load pending imports')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Load summary statistics from API
 */
async function loadSummary() {
  if (!currentTenantKey.value) return

  try {
    summary.value = await importClient.getReviewSummary(currentTenantKey.value)
  } catch (err) {
    error.value = handleApiError(err, 'Load Summary Failed', 'Failed to load import summary')
    showError.value = true
  }
}

/**
 * Handle page change from PaginationBar
 */
function handlePageChange(pageNumber: number) {
  loadPendingReview(pageNumber)
}

/**
 * Handle files selected from file input
 */
function handleFileInputChange(event: Event) {
  const input = event.target as HTMLInputElement
  if (input.files) {
    selectedFiles.value = Array.from(input.files)
  }
}

/**
 * Upload selected files to API
 */
async function uploadFiles() {
  if (selectedFiles.value.length === 0) return
  if (!currentTenantKey.value) return

  uploadInProgress.value = true
  statusMessages.value = []
  showStatusPane.value = true
  statusVariant.value = 'info'
  error.value = undefined
  showError.value = false

  let uploadError: IProblemDetails | undefined = undefined
  let hasUploadError = false

  try {
    // Process each file sequentially
    for (const file of selectedFiles.value) {
      statusMessages.value.push(`${file.name}: Importing...`)

      try {
        const fileParameter: FileParameter = {
          data: file,
          fileName: file.name,
        }

        const result: ImportReviewUploadDto = await importClient.uploadFile(
          currentTenantKey.value,
          fileParameter,
        )

        // Remove "Importing..." message
        statusMessages.value = statusMessages.value.filter((msg) => !msg.includes('Importing...'))

        // Check for errors
        if (result.errors && result.errors.length > 0) {
          statusMessages.value.push(
            `${file.name}: ${result.importedCount} transactions added, ${result.errors.length} errors detected`,
          )
          if (statusVariant.value !== 'danger') {
            statusVariant.value = 'warning'
          }
        } else {
          statusMessages.value.push(`${file.name}: ${result.importedCount} transactions added`)
          if (statusVariant.value !== 'danger' && statusVariant.value !== 'warning') {
            statusVariant.value = 'success'
          }
        }
      } catch (err) {
        // Remove "Importing..." message
        statusMessages.value = statusMessages.value.filter((msg) => !msg.includes('Importing...'))

        // Extract error details from API response and save for display after reload
        uploadError = handleApiError(err, 'Upload Failed', 'Failed to upload file')
        hasUploadError = true
        const errorMessage = uploadError.detail || 'Upload failed'

        statusMessages.value.push(`${file.name}: ${errorMessage}`)
        statusVariant.value = 'danger'
      }
    }

    // Reload pending review to show newly imported transactions
    await loadPendingReview(1)
    await loadSummary()

    // Restore error state after loadPendingReview (which clears it)
    if (hasUploadError && uploadError) {
      error.value = uploadError
      showError.value = true
    }

    // Clear file selection
    selectedFiles.value = []
  } finally {
    uploadInProgress.value = false
  }
}

/**
 * Handle closing the status pane
 */
function handleCloseStatusPane() {
  showStatusPane.value = false
  statusMessages.value = []
}

/**
 * Handle individual checkbox toggle
 */
async function handleToggleSelection(key: string) {
  if (!currentTenantKey.value) return

  error.value = undefined
  showError.value = false

  try {
    // Get current selection state from transaction
    const transaction = transactions.value.find((t) => t.key === key)
    if (!transaction) return

    // Optimistically update UI immediately
    const newValue = !transaction.isSelected
    transaction.isSelected = newValue

    // Update summary count optimistically
    if (summary.value) {
      summary.value.selectedCount = (summary.value.selectedCount || 0) + (newValue ? 1 : -1)
    }

    // Toggle selection via API (in background)
    const request = new SetSelectionRequest({
      keys: [key],
      isSelected: newValue,
    })
    await importClient.setSelection(currentTenantKey.value, request)
  } catch (err) {
    // On error, reload to get correct state from server
    error.value = handleApiError(err, 'Selection Failed', 'Failed to update transaction selection')
    showError.value = true
    await loadPendingReview(currentPage.value)
    await loadSummary()
  }
}

/**
 * Handle Select All button click
 */
async function handleSelectAll() {
  if (!currentTenantKey.value) return

  error.value = undefined
  showError.value = false

  try {
    // Optimistically update UI immediately
    transactions.value.forEach((t) => {
      t.isSelected = true
    })
    if (summary.value) {
      summary.value.selectedCount = summary.value.totalCount || 0
    }

    // Call API in background
    await importClient.selectAll(currentTenantKey.value)

    // Refresh summary to get accurate count (in case of pagination)
    await loadSummary()
  } catch (err) {
    error.value = handleApiError(err, 'Select All Failed', 'Failed to select all transactions')
    showError.value = true
    // On error, reload to get correct state from server
    await loadPendingReview(currentPage.value)
    await loadSummary()
  }
}

/**
 * Handle Deselect All button click
 */
async function handleDeselectAll() {
  if (!currentTenantKey.value) return

  error.value = undefined
  showError.value = false

  try {
    // Optimistically update UI immediately
    transactions.value.forEach((t) => {
      t.isSelected = false
    })
    if (summary.value) {
      summary.value.selectedCount = 0
    }

    // Call API in background
    await importClient.deselectAll(currentTenantKey.value)
  } catch (err) {
    error.value = handleApiError(err, 'Deselect All Failed', 'Failed to deselect all transactions')
    showError.value = true
    // On error, reload to get correct state from server
    await loadPendingReview(currentPage.value)
    await loadSummary()
  }
}

/**
 * Handle Import button click - shows confirmation dialog
 */
function handleImport() {
  if (!currentTenantKey.value) return
  if (!hasSelections.value) return

  // Show confirmation modal
  showConfirmModal.value = true
}

/**
 * Execute the actual import after user confirms
 */
async function confirmImport() {
  if (!currentTenantKey.value) return

  importing.value = true
  error.value = undefined
  showError.value = false

  try {
    // Call completeReview with NO parameters - server reads IsSelected from database
    await importClient.completeReview(currentTenantKey.value)

    // Close confirmation modal
    showConfirmModal.value = false

    // Navigate to transactions page
    navigateTo('/transactions')
  } catch (err) {
    error.value = handleApiError(err, 'Import Failed', 'Failed to import transactions')
    showError.value = true
    // Close confirmation modal on error
    showConfirmModal.value = false
  } finally {
    importing.value = false
  }
}

/**
 * Handle Delete All button click - opens confirmation modal
 */
function handleDeleteAll() {
  showDeleteModal.value = true
}

/**
 * Confirm and execute delete all
 */
async function confirmDeleteAll() {
  if (!currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await importClient.deleteAllPendingReview(currentTenantKey.value)

    // Reload pending review (should be empty now) and summary
    await loadPendingReview(1)
    await loadSummary()

    // Close modal
    showDeleteModal.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Delete Failed', 'Failed to delete pending imports')
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
      <WorkspaceSelector @change="loadPendingReview(1)" />
    </div>

    <!-- Main Content -->
    <div class="container py-4">
      <h1
        class="mb-4"
        data-test-id="page-heading"
      >
        Import Bank Transactions
      </h1>

      <!-- No Workspace Warning -->
      <div
        v-if="!hasWorkspace"
        class="alert alert-warning"
        data-test-id="no-workspace-warning"
      >
        <strong>No workspace selected.</strong> Please select a workspace to import transactions.
      </div>

      <!-- Permission Denied Error -->
      <div
        v-else-if="!canImport"
        class="alert alert-danger"
        data-test-id="permission-denied-error"
      >
        <strong>Permission Denied</strong><br />
        You do not have permission to import into this workspace. Editor role is required.
      </div>

      <!-- Main Import UI (only shown if user has permission) -->
      <div v-else>
        <!-- Upload Status Pane -->
        <div style="min-height: 0">
          <UploadStatusPane
            :show="showStatusPane"
            :status-messages="statusMessages"
            :variant="statusVariant"
            @close="handleCloseStatusPane"
          />
        </div>

        <!-- Error Display -->
        <div style="min-height: 0">
          <ErrorDisplay
            v-model:show="showError"
            :problem="error"
            class="mb-4"
          />
        </div>

        <!-- File Upload Section -->
        <div class="card mb-4">
          <div class="card-body">
            <h5 class="card-title">Upload Files</h5>
            <p class="text-muted mb-3">
              Select OFX or QFX files from your bank to import transactions.
            </p>

            <div class="row g-2">
              <div class="col">
                <input
                  type="file"
                  class="form-control"
                  accept=".ofx,.qfx"
                  multiple
                  data-test-id="file-input"
                  :disabled="uploadInProgress || loading"
                  @change="handleFileInputChange"
                />
              </div>
              <div class="col-auto">
                <button
                  type="button"
                  class="btn btn-primary"
                  data-test-id="upload-button"
                  :disabled="uploadInProgress || loading || !ready || selectedFiles.length === 0"
                  @click="uploadFiles"
                >
                  <BaseSpinner
                    v-if="uploadInProgress"
                    size="sm"
                    class="me-1"
                  />
                  {{ uploadInProgress ? 'Uploading...' : 'Upload' }}
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Transaction Review Section -->
        <div
          v-if="hasTransactions"
          class="card"
        >
          <div class="card-body">
            <h5 class="card-title">Review Transactions</h5>
            <p class="text-muted mb-4">
              Review and select transactions to import. Transactions marked as duplicates are
              deselected by default.
            </p>

            <!-- Duplicates Alert -->
            <ImportDuplicatesAlert :show="hasPotentialDuplicates" />

            <!-- Selection Action Buttons -->
            <div class="mb-3 d-flex justify-content-between align-items-center">
              <div class="btn-group">
                <button
                  type="button"
                  class="btn btn-sm btn-outline-secondary"
                  data-test-id="select-all-button"
                  :disabled="loading || uploadInProgress"
                  @click="handleSelectAll"
                >
                  Select All
                </button>
                <button
                  type="button"
                  class="btn btn-sm btn-outline-secondary"
                  data-test-id="deselect-all-button"
                  :disabled="loading || uploadInProgress"
                  @click="handleDeselectAll"
                >
                  Deselect All
                </button>
              </div>

              <!-- Action Buttons -->
              <ImportActionButtons
                :has-transactions="hasTransactions"
                :has-selections="hasSelections"
                :loading="loading"
                :uploading="uploadInProgress"
                @import="handleImport"
                @delete-all="handleDeleteAll"
              />
            </div>

            <!-- Transaction Table with Loading Overlay -->
            <div style="position: relative; min-height: 200px">
              <ImportReviewTable
                :transactions="transactions"
                :loading="loading"
                @toggle-selection="handleToggleSelection"
              />
              <!-- Loading Overlay for Pagination -->
              <div
                v-if="loading"
                style="
                  position: absolute;
                  top: 0;
                  left: 0;
                  right: 0;
                  bottom: 0;
                  background: rgba(255, 255, 255, 0.8);
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  z-index: 10;
                "
              >
                <BaseSpinner />
              </div>
            </div>

            <!-- Pagination -->
            <PaginationBar
              v-if="paginatedResult?.metadata"
              :page-info="paginatedResult.metadata"
              class="mt-3"
              @page-updated="handlePageChange"
            />
          </div>
        </div>

        <!-- Empty State -->
        <div
          v-else-if="!loading"
          class="card"
        >
          <div
            class="card-body text-center py-5 text-muted"
            data-test-id="empty-state"
          >
            <FeatherIcon
              icon="inbox"
              size="48"
              class="mb-3"
            />
            <h5>No pending imports</h5>
            <p>Upload bank files to get started</p>
          </div>
        </div>

        <!-- Loading State -->
        <div
          v-if="loading && !hasTransactions"
          class="text-center py-5"
          data-test-id="loading-state"
        >
          <BaseSpinner />
          <div class="mt-2">
            <small class="text-muted">Loading pending imports...</small>
          </div>
        </div>
      </div>
    </div>

    <!-- Import Confirmation Modal -->
    <ModalDialog
      v-model:show="showConfirmModal"
      title="Confirm Import"
      :loading="importing"
      primary-button-variant="primary"
      :primary-button-text="importing ? 'Importing...' : 'Import'"
      secondary-button-text="Cancel"
      primary-button-test-id="import-confirm-button"
      secondary-button-test-id="import-cancel-button"
      test-id="import-confirm-modal"
      @primary="confirmImport"
    >
      <p>
        You are about to import the selected transactions into your workspace:
        <strong>{{ currentTenantName }}</strong>
      </p>

      <!-- Import Statistics -->
      <div
        v-if="summary"
        class="alert alert-info mb-3"
        data-test-id="import-statistics"
      >
        <h6 class="alert-heading mb-2">Import Summary:</h6>
        <ul class="mb-0">
          <li>
            <strong>{{ summary.selectedCount || 0 }}</strong> transaction{{
              summary.selectedCount === 1 ? '' : 's'
            }}
            will be imported
          </li>
          <li v-if="summary.totalCount && summary.totalCount > (summary.selectedCount || 0)">
            <strong>{{ summary.totalCount - (summary.selectedCount || 0) }}</strong> transaction{{
              summary.totalCount - (summary.selectedCount || 0) === 1 ? '' : 's'
            }}
            will be discarded
          </li>
          <li v-if="summary.potentialDuplicateCount && summary.potentialDuplicateCount > 0">
            <strong>{{ summary.potentialDuplicateCount }}</strong> potential duplicate{{
              summary.potentialDuplicateCount === 1 ? '' : 's'
            }}
            detected
          </li>
        </ul>
      </div>

      <p class="mb-0">
        <strong>Do you want to proceed with this import?</strong>
      </p>
    </ModalDialog>

    <!-- Delete All Confirmation Modal -->
    <ModalDialog
      v-model:show="showDeleteModal"
      title="Delete All Pending Imports"
      :loading="loading"
      primary-button-variant="danger"
      :primary-button-text="loading ? 'Deleting...' : 'Delete All'"
      primary-button-test-id="delete-all-submit-button"
      secondary-button-test-id="delete-all-cancel-button"
      test-id="delete-all-modal"
      @primary="confirmDeleteAll"
    >
      <p>Are you sure you want to delete all pending import transactions?</p>
      <div
        v-if="hasTransactions"
        class="alert alert-warning"
        data-test-id="delete-all-warning"
      >
        <strong>Warning:</strong> This will delete
        <strong>{{ paginatedResult?.metadata?.totalCount || transactions.length }}</strong>
        transaction{{
          (paginatedResult?.metadata?.totalCount || transactions.length) === 1 ? '' : 's'
        }}
        from the import queue. This action cannot be undone.
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
</style>
