---
status: Draft
layer: Frontend
parent: DESIGN-BANK-IMPORT.md
created: 2025-12-28
related_docs:
  - DESIGN-BANK-IMPORT.md
  - DESIGN-BANK-IMPORT-API.md
  - DESIGN-BANK-IMPORT-APPLICATION.md
  - VISUAL-DESIGN-BANK-IMPORT.md
  - MOCKUP-BANK-IMPORT.md
  - PRD-BANK-IMPORT.md
---

# Frontend Layer Design: Bank Import Feature

## Overview

This document provides the complete frontend layer design for the Bank Import feature. The frontend layer implements a Vue/Nuxt page for uploading OFX/QFX files, reviewing imported transactions with duplicate detection, and selectively accepting transactions into the main transaction list.

**Key components:**
- **[`import.vue`](src/FrontEnd.Nuxt/app/pages/import.vue)** - Main import page component
- **File upload** - Multi-file selection with `.ofx`/`.qfx` validation
- **Status pane** - Dismissible upload results display
- **Transaction table** - Paginated review with checkbox selection
- **Session storage** - Selection persistence across navigation
- **API integration** - Auto-generated TypeScript client with authentication

**Layer responsibilities:**
- Present file picker UI for OFX/QFX file selection
- Handle file upload and display processing status
- Display transaction review table with pagination
- Manage checkbox selections with session storage persistence
- Call API endpoints via authenticated fetch wrapper
- Navigate to transactions page after successful import

## Complete import.vue Page Component

Location: `src/FrontEnd.Nuxt/app/pages/import.vue`

```vue
<script setup lang="ts">
/**
 * Bank Import Page
 *
 * Allows users to upload OFX/QFX bank files, review imported transactions,
 * and selectively accept transactions into their workspace.
 */

import {
  ImportClient,
  TenantRole,
  type ImportReviewTransactionDto,
  type PaginatedResultDto,
  type ImportResultDto,
  type IProblemDetails,
  DuplicateStatus,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Import',
  order: 4,
  auth: true,
  layout: 'default',
})

const userPreferencesStore = useUserPreferencesStore()

// API Client
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const importClient = new ImportClient(baseUrl, authFetch)

// Page ready state (for SSR/hydration)
const ready = ref(false)

// File upload state
const fileInput = ref<HTMLInputElement | null>(null)
const selectedFiles = ref<File[]>([])
const uploading = ref(false)
const uploadStatus = ref<string[]>([])
const showUploadStatus = ref(false)

// Transaction review state
const transactions = ref<ImportReviewTransactionDto[]>([])
const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)

// Selection state (tracked by transaction key)
const selectedKeys = ref<Set<string>>(new Set())

// Pagination state
const currentPage = ref(1)
const pageSize = ref(50)
const totalCount = ref(0)
const totalPages = ref(0)
const hasPreviousPage = ref(false)
const hasNextPage = ref(false)

// Modal state
const showDeleteAllModal = ref(false)

// Computed
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)
const canImport = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})

const hasPotentialDuplicates = computed(() => {
  return transactions.value.some(
    (t) => t.duplicateStatus === DuplicateStatus.PotentialDuplicate
  )
})

const sessionStorageKey = computed(() => {
  return currentTenantKey.value ? `import-review-selections-${currentTenantKey.value}` : null
})

// Watch for workspace changes
watch(currentTenantKey, async (newKey) => {
  if (newKey) {
    await loadPendingReview()
    restoreSelections()
  } else {
    transactions.value = []
    selectedKeys.value.clear()
  }
})

// Load pending review on mount
onMounted(async () => {
  ready.value = true
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value) {
    await loadPendingReview()
    restoreSelections()
  }
})

// Methods

/**
 * Opens file picker dialog when "Browse..." button is clicked
 */
function openFilePicker() {
  fileInput.value?.click()
}

/**
 * Handles file selection from input element
 */
function handleFileSelected(event: Event) {
  const target = event.target as HTMLInputElement
  if (target.files) {
    selectedFiles.value = Array.from(target.files)
    handleUpload()
  }
}

/**
 * Uploads selected files and processes them
 */
async function handleUpload() {
  if (selectedFiles.value.length === 0) return
  if (!currentTenantKey.value) return

  uploading.value = true
  uploadStatus.value = []
  showUploadStatus.value = true
  error.value = undefined
  showError.value = false

  try {
    // Process files sequentially
    for (const file of selectedFiles.value) {
      // Validate file extension
      const extension = file.name.toLowerCase().split('.').pop()
      if (extension !== 'ofx' && extension !== 'qfx') {
        uploadStatus.value.push(`❌ ${file.name}: Invalid file type (must be .ofx or .qfx)`)
        continue
      }

      uploadStatus.value.push(`⏳ ${file.name}: Importing...`)

      try {
        const result: ImportResultDto = await importClient.uploadFile(file)

        // Replace "Importing..." message with success message
        const lastIndex = uploadStatus.value.length - 1
        uploadStatus.value[lastIndex] = `✓ ${file.name} imported: ${result.importedCount} transactions added`
      } catch (err) {
        const lastIndex = uploadStatus.value.length - 1
        uploadStatus.value[lastIndex] = `❌ ${file.name}: Upload failed`

        error.value = handleApiError(err, 'Upload Failed', `Failed to upload ${file.name}`)
        showError.value = true
      }
    }

    // Reload pending review to show newly imported transactions
    await loadPendingReview()

    // Clear file input
    selectedFiles.value = []
    if (fileInput.value) {
      fileInput.value.value = ''
    }
  } finally {
    uploading.value = false
  }
}

/**
 * Loads pending import review transactions with pagination
 */
async function loadPendingReview() {
  if (!currentTenantKey.value) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to view pending imports',
    }
    showError.value = true
    return
  }

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    const result: PaginatedResultDto<ImportReviewTransactionDto> =
      await importClient.getPendingReview(currentPage.value, pageSize.value)

    transactions.value = result.items || []
    totalCount.value = result.totalCount || 0
    totalPages.value = result.totalPages || 0
    hasPreviousPage.value = result.hasPreviousPage || false
    hasNextPage.value = result.hasNextPage || false

    // Set default selections (new transactions selected by default)
    setDefaultSelections()
  } catch (err) {
    error.value = handleApiError(err, 'Load Failed', 'Failed to load pending import review')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Sets default checkbox selections based on duplicate status
 * - New: selected by default
 * - ExactDuplicate: deselected by default
 * - PotentialDuplicate: deselected by default
 */
function setDefaultSelections() {
  transactions.value.forEach((transaction) => {
    // Only set default if not already in selectedKeys (from session storage or previous load)
    if (!selectedKeys.value.has(transaction.key!)) {
      if (transaction.duplicateStatus === DuplicateStatus.New) {
        selectedKeys.value.add(transaction.key!)
      }
    }
  })
  saveSelections()
}

/**
 * Toggles selection for a single transaction
 */
function toggleSelection(key: string) {
  if (selectedKeys.value.has(key)) {
    selectedKeys.value.delete(key)
  } else {
    selectedKeys.value.add(key)
  }
  saveSelections()
}

/**
 * Toggles all visible transactions on current page
 */
function toggleAll() {
  const allSelected = transactions.value.every((t) => selectedKeys.value.has(t.key!))

  transactions.value.forEach((transaction) => {
    if (allSelected) {
      selectedKeys.value.delete(transaction.key!)
    } else {
      selectedKeys.value.add(transaction.key!)
    }
  })
  saveSelections()
}

/**
 * Navigates to a specific page
 */
async function goToPage(page: number) {
  if (page < 1 || page > totalPages.value) return
  currentPage.value = page
  await loadPendingReview()
}

/**
 * Accepts selected transactions (copies to Transactions table, removes from review)
 */
async function acceptTransactions() {
  if (selectedKeys.value.size === 0) {
    error.value = {
      title: 'No transactions selected',
      detail: 'Please select at least one transaction to import',
    }
    showError.value = true
    return
  }

  if (!currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    const keysArray = Array.from(selectedKeys.value)
    await importClient.acceptTransactions(keysArray)

    // Clear selections
    clearSelections()

    // Navigate to transactions page to see newly imported transactions
    navigateTo('/transactions')
  } catch (err) {
    error.value = handleApiError(err, 'Import Failed', 'Failed to import selected transactions')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Opens delete all confirmation modal
 */
function openDeleteAllModal() {
  showDeleteAllModal.value = true
}

/**
 * Deletes all pending import review transactions
 */
async function deleteAllTransactions() {
  if (!currentTenantKey.value) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await importClient.deleteAllPendingReview()

    // Clear selections and reload
    clearSelections()
    await loadPendingReview()

    showDeleteAllModal.value = false
  } catch (err) {
    error.value = handleApiError(err, 'Delete Failed', 'Failed to delete all pending imports')
    showError.value = true
  } finally {
    loading.value = false
  }
}

/**
 * Saves current selections to session storage
 */
function saveSelections() {
  if (!sessionStorageKey.value) return

  try {
    const keysArray = Array.from(selectedKeys.value)
    sessionStorage.setItem(sessionStorageKey.value, JSON.stringify(keysArray))
  } catch (err) {
    console.warn('Failed to save selections to session storage:', err)
  }
}

/**
 * Restores selections from session storage
 */
function restoreSelections() {
  if (!sessionStorageKey.value) return

  try {
    const stored = sessionStorage.getItem(sessionStorageKey.value)
    if (stored) {
      const keysArray = JSON.parse(stored) as string[]
      selectedKeys.value = new Set(keysArray)
    }
  } catch (err) {
    console.warn('Failed to restore selections from session storage:', err)
  }
}

/**
 * Clears selections from state and session storage
 */
function clearSelections() {
  selectedKeys.value.clear()
  if (sessionStorageKey.value) {
    try {
      sessionStorage.removeItem(sessionStorageKey.value)
    } catch (err) {
      console.warn('Failed to clear selections from session storage:', err)
    }
  }
}

/**
 * Formats date for display
 */
function formatDate(date: Date | undefined): string {
  if (!date) return ''
  return new Date(date).toLocaleDateString()
}

/**
 * Formats currency for display
 */
function formatCurrency(amount: number | undefined): string {
  if (amount === undefined) return '$0.00'
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(amount)
}

/**
 * Returns CSS class for row based on duplicate status
 */
function getRowClass(transaction: ImportReviewTransactionDto): string {
  if (transaction.duplicateStatus === DuplicateStatus.PotentialDuplicate) {
    return 'table-warning'
  }
  return ''
}
</script>

<template>
  <div>
    <!-- Main Content -->
    <div class="container py-4">
      <h1 data-test-id="page-heading">Import</h1>

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
        Please select a workspace to import transactions
      </div>

      <!-- Permission Warning -->
      <div
        v-else-if="!canImport"
        class="alert alert-warning"
        data-test-id="permission-warning"
      >
        <FeatherIcon
          icon="alert-circle"
          size="16"
          class="me-2"
        />
        You do not have permission to import transactions in this workspace
      </div>

      <!-- Error Display -->
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-4"
      />

      <!-- File Upload Section -->
      <div
        v-if="canImport"
        class="mb-4"
      >
        <h5>Choose bank files to upload</h5>
        <div class="d-flex align-items-center">
          <div class="file-input-wrapper">
            <input
              ref="fileInput"
              type="file"
              accept=".ofx,.qfx"
              multiple
              class="d-none"
              data-test-id="file-input"
              @change="handleFileSelected"
            />
            <button
              class="btn btn-secondary"
              data-test-id="browse-button"
              :disabled="uploading || loading || !ready"
              @click="openFilePicker"
            >
              <FeatherIcon
                icon="folder"
                size="16"
                class="me-1"
              />
              Choose Files (.ofx, .qfx)
            </button>
          </div>
          <button
            class="btn btn-outline-secondary ms-2"
            data-test-id="browse-files-button"
            :disabled="uploading || loading || !ready"
            @click="openFilePicker"
          >
            Browse...
          </button>
        </div>
      </div>

      <!-- Upload Status Pane -->
      <div
        v-if="showUploadStatus && uploadStatus.length > 0"
        class="alert alert-info alert-dismissible fade show"
        data-test-id="upload-status-pane"
      >
        <button
          type="button"
          class="btn-close"
          data-test-id="close-status-button"
          @click="showUploadStatus = false"
        ></button>
        <div
          v-for="(status, index) in uploadStatus"
          :key="index"
          data-test-id="upload-status-line"
        >
          {{ status }}
        </div>
      </div>

      <!-- Potential Duplicates Alert -->
      <div
        v-if="hasPotentialDuplicates"
        class="alert alert-warning"
        data-test-id="potential-duplicates-alert"
      >
        <FeatherIcon
          icon="alert-triangle"
          size="16"
          class="me-2"
        />
        Note: Potential duplicates detected and highlighted. Transactions have the same identifier
        as another transaction, but differ in payee or amount.
      </div>

      <!-- Action Buttons (shown when transactions exist) -->
      <div
        v-if="transactions.length > 0"
        class="d-flex justify-content-end mb-3"
        data-test-id="action-buttons"
      >
        <button
          class="btn btn-danger me-2"
          data-test-id="delete-all-button"
          :disabled="loading || uploading || !ready"
          @click="openDeleteAllModal"
        >
          <FeatherIcon
            icon="trash-2"
            size="16"
            class="me-1"
          />
          Delete All
        </button>
        <button
          class="btn btn-primary"
          data-test-id="import-button"
          :disabled="loading || uploading || !ready || selectedKeys.size === 0"
          @click="acceptTransactions"
        >
          <FeatherIcon
            icon="check"
            size="16"
            class="me-1"
          />
          Import
        </button>
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
            data-test-id="loading-text"
            >Loading transactions...</small
          >
        </div>
      </div>

      <!-- Transactions Table -->
      <div
        v-else-if="hasWorkspace && canImport"
        class="card"
        data-test-id="transactions-card"
      >
        <div class="card-body">
          <!-- Empty State -->
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
            <p>No pending imports</p>
            <p class="small">Upload bank files to get started</p>
          </div>

          <!-- Table -->
          <div
            v-else
            class="table-responsive"
          >
            <table
              class="table table-hover"
              data-test-id="import-review-table"
            >
              <thead>
                <tr>
                  <th class="checkbox-column">
                    <input
                      type="checkbox"
                      class="form-check-input"
                      data-test-id="select-all-checkbox"
                      :checked="transactions.every((t) => selectedKeys.has(t.key!))"
                      :disabled="loading || uploading"
                      @change="toggleAll"
                    />
                  </th>
                  <th data-test-id="date-header">Date</th>
                  <th data-test-id="payee-header">Payee</th>
                  <th data-test-id="category-header">Category</th>
                  <th
                    data-test-id="amount-header"
                    class="text-end"
                  >
                    Amount
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="transaction in transactions"
                  :key="transaction.key"
                  :class="getRowClass(transaction)"
                  :data-test-id="`transaction-row-${transaction.key}`"
                >
                  <td class="checkbox-column">
                    <input
                      type="checkbox"
                      class="form-check-input"
                      :checked="selectedKeys.has(transaction.key!)"
                      :disabled="loading || uploading"
                      :data-test-id="`checkbox-${transaction.key}`"
                      @change="toggleSelection(transaction.key!)"
                    />
                  </td>
                  <td>
                    <span v-if="transaction.duplicateStatus === DuplicateStatus.PotentialDuplicate">
                      <FeatherIcon
                        icon="alert-triangle"
                        size="14"
                        class="me-1 text-warning"
                        data-test-id="duplicate-warning-icon"
                      />
                    </span>
                    {{ formatDate(transaction.date) }}
                  </td>
                  <td>{{ transaction.payee }}</td>
                  <td>{{ transaction.category || '' }}</td>
                  <td class="text-end">{{ formatCurrency(transaction.amount) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Pagination -->
          <div
            v-if="totalPages > 1"
            class="d-flex justify-content-between align-items-center mt-3"
            data-test-id="pagination"
          >
            <div class="text-muted small">
              Showing {{ (currentPage - 1) * pageSize + 1 }}-{{
                Math.min(currentPage * pageSize, totalCount)
              }}
              of {{ totalCount }}
            </div>
            <nav>
              <ul class="pagination mb-0">
                <li
                  class="page-item"
                  :class="{ disabled: !hasPreviousPage }"
                >
                  <button
                    class="page-link"
                    data-test-id="previous-page-button"
                    :disabled="!hasPreviousPage || loading"
                    @click="goToPage(currentPage - 1)"
                  >
                    ◀
                  </button>
                </li>
                <li
                  v-for="page in totalPages"
                  :key="page"
                  class="page-item"
                  :class="{ active: page === currentPage }"
                >
                  <button
                    class="page-link"
                    :data-test-id="`page-${page}-button`"
                    :disabled="loading"
                    @click="goToPage(page)"
                  >
                    {{ page }}
                  </button>
                </li>
                <li
                  class="page-item"
                  :class="{ disabled: !hasNextPage }"
                >
                  <button
                    class="page-link"
                    data-test-id="next-page-button"
                    :disabled="!hasNextPage || loading"
                    @click="goToPage(currentPage + 1)"
                  >
                    ▶
                  </button>
                </li>
              </ul>
            </nav>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete All Confirmation Modal -->
    <ModalDialog
      v-model:show="showDeleteAllModal"
      title="Delete All Pending Imports"
      :loading="loading"
      primary-button-variant="danger"
      :primary-button-text="loading ? 'Deleting...' : 'Delete'"
      primary-button-test-id="delete-all-submit-button"
      secondary-button-test-id="delete-all-cancel-button"
      test-id="delete-all-modal"
      @primary="deleteAllTransactions"
    >
      <p>Are you sure you want to delete all pending import transactions?</p>
      <p>This cannot be undone.</p>
      <div
        class="alert alert-warning"
        data-test-id="delete-all-count"
      >
        <strong>{{ totalCount }} transactions will be deleted.</strong>
      </div>
    </ModalDialog>
  </div>
</template>

<style scoped>
.checkbox-column {
  width: 40px;
  text-align: center;
}

.file-input-wrapper {
  display: inline-block;
}

.table th {
  font-weight: 600;
}

.table-warning {
  background-color: #fff3cd;
}
</style>
```

## Key Features

### File Upload

**Multi-file selection:**
- Users can select multiple `.ofx` or `.qfx` files at once
- File input has `multiple` attribute enabled
- Files are processed sequentially (simpler implementation)

**Validation:**
- Client-side extension validation before upload (`.ofx`, `.qfx` only)
- Server-side validation via API endpoint
- Clear error messages for invalid files

**Upload flow:**
1. User clicks "Browse..." button
2. File picker dialog opens (triggered by hidden input element)
3. User selects one or more files
4. Files are validated and uploaded sequentially
5. Status pane displays progress and results

### Upload Status Pane

**Visibility:**
- Shown when files are being processed
- Dismissible via close button (Bootstrap alert-dismissible)
- Remains visible after completion until user dismisses

**Content:**
- During upload: "⏳ filename.ofx: Importing..."
- After success: "✓ filename.ofx imported: 150 transactions added"
- After failure: "❌ filename.ofx: Upload failed"
- Multiple files show multiple status lines

**Styling:**
- Uses Bootstrap `.alert .alert-info` styling
- Dismissible via `.alert-dismissible` class

### Potential Duplicates Alert

**Visibility:**
- Shown when at least one transaction has `DuplicateStatus.PotentialDuplicate`
- Computed property `hasPotentialDuplicates` checks all transactions

**Content:**
- Warning icon (⚠) on the left
- Explanation text: "Note: Potential duplicates detected and highlighted. Transactions have the same identifier as another transaction, but differ in payee or amount."

**Styling:**
- Uses Bootstrap `.alert .alert-warning` styling
- Yellow/warning background color

### Transaction Table

**Columns:**
1. Checkbox (selection)
2. Date (with warning icon for potential duplicates)
3. Payee
4. Category (placeholder for future Payee Matching rules feature)
5. Amount (right-aligned)

**Row highlighting:**
- **Normal rows** (New, ExactDuplicate): Default white background
- **Potential duplicate rows**: Yellow background (`table-warning` class)

**Selection:**
- Checkbox in header row toggles all visible transactions on current page
- Individual checkboxes toggle single transaction selection
- Selection state tracked in `selectedKeys` Set (by transaction key)

**Default selections:**
- **New transactions**: Selected by default
- **Exact duplicates**: Deselected by default
- **Potential duplicates**: Deselected by default

### Pagination

**Standard pagination control:**
- Previous/Next buttons (◀ ▶)
- Page number buttons
- "Showing 1-50 of 150" text

**Behavior:**
- Hidden when `totalPages <= 1`
- Disabled when loading
- Current page highlighted via `.active` class

**Page size:**
- Default: 50 transactions per page
- Matches pattern from transactions page

### Delete Confirmation Modal

**Trigger:**
- "Delete All" button above transaction table

**Content:**
- Title: "Delete All Pending Imports"
- Warning message: "Are you sure you want to delete all pending import transactions? This cannot be undone."
- Count display: "378 transactions will be deleted."

**Actions:**
- Cancel button (closes modal)
- Delete button (red/danger variant, calls API)

### Empty State

**Shown when:**
- No pending import review transactions exist
- User has workspace selected and Editor/Owner role

**Content:**
- Inbox icon (📥)
- "No pending imports" message
- "Upload bank files to get started" helper text

## Session Storage Pattern

### Key Format

**Template:** `import-review-selections-{tenantKey}`

**Examples:**
- `import-review-selections-550e8400-e29b-41d4-a716-446655440000`
- `import-review-selections-660e8400-e29b-41d4-a716-446655440001`

**Rationale:**
- Tenant-scoped keys prevent cross-workspace selection leakage
- Workspace change automatically switches to correct selection state

### Value Format

**JSON array of selected transaction keys:**
```json
["guid1", "guid2", "guid3"]
```

### When to Save/Restore/Clear

**Save:**
- After every checkbox toggle (`toggleSelection`, `toggleAll`)
- After setting default selections (`setDefaultSelections`)

**Restore:**
- On page mount (after loading pending review)
- After workspace change (watch on `currentTenantKey`)

**Clear:**
- After successful import (`acceptTransactions`)
- After delete all (`deleteAllTransactions`)
- When workspace changes to null

### Why Session Storage Over Database

**Rationale:**
- Selection state is **UI state**, not domain data
- No need for server-side tracking or persistence
- Simpler implementation (no API calls on every checkbox toggle)
- Automatically cleared when user closes browser/tab
- Works offline (no network dependency for checkbox changes)
- No database migrations or additional tables required

**Tradeoffs:**
- Selections lost on browser close (acceptable for temporary review state)
- Not synchronized across browser tabs (acceptable for single-session workflow)
- Limited storage capacity (acceptable - only storing GUIDs)

## Navigation Integration

### Adding to layouts/default.vue

The Import page uses the `default` layout, which includes the [`SiteHeader`](src/FrontEnd.Nuxt/app/components/SiteHeader.vue) component. To add Import to the primary navigation:

**Location:** [`src/FrontEnd.Nuxt/app/components/SiteHeader.vue`](src/FrontEnd.Nuxt/app/components/SiteHeader.vue)

**Add navigation item:**
```vue
<NuxtLink
  to="/import"
  class="nav-link"
  :class="{ active: $route.path === '/import' }"
  data-test-id="import-nav-link"
>
  Import
</NuxtLink>
```

**Placement:**
- After "Transactions" link
- Before "Workspaces" link (if present)

**No badge indicator:**
- Deferred to future enhancement (too complex for initial release)
- Would require polling API or WebSocket for real-time count updates

## Component Reuse

### From Existing Pages

The import page reuses components and patterns from [`transactions/index.vue`](src/FrontEnd.Nuxt/app/pages/transactions/index.vue):

**Components:**
- [`ModalDialog`](src/FrontEnd.Nuxt/app/components/ModalDialog.vue) - For delete confirmation modal
- [`ErrorDisplay`](src/FrontEnd.Nuxt/app/components/ErrorDisplay.vue) - For API error messages
- [`BaseSpinner`](src/FrontEnd.Nuxt/app/components/BaseSpinner.vue) - For loading states
- [`WorkspaceSelector`](src/FrontEnd.Nuxt/app/components/WorkspaceSelector.vue) - Standard workspace selector (if using chrome layout)
- [`FeatherIcon`](src/FrontEnd.Nuxt/app/components/FeatherIcon.vue) - For icons (folder, check, trash, alert)

**Bootstrap styling:**
- `.table .table-hover` - Transaction table
- `.alert .alert-warning` - Warning messages
- `.alert .alert-info` - Upload status pane
- `.btn .btn-primary` - Primary action button
- `.btn .btn-danger` - Delete button
- `.btn .btn-secondary` - Secondary action button
- `.card .card-body` - Content container

**Patterns:**
- File upload handling (hidden input + button trigger)
- API error handling via `handleApiError` utility
- Loading states with spinner and disabled buttons
- Empty state messaging
- Pagination controls

## State Management

### Reactive State (Vue refs)

**File upload:**
```typescript
const fileInput = ref<HTMLInputElement | null>(null)
const selectedFiles = ref<File[]>([])
const uploading = ref(false)
const uploadStatus = ref<string[]>([])
const showUploadStatus = ref(false)
```

**Transaction review:**
```typescript
const transactions = ref<ImportReviewTransactionDto[]>([])
const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
```

**Selection:**
```typescript
const selectedKeys = ref<Set<string>>(new Set())
```

**Pagination:**
```typescript
const currentPage = ref(1)
const pageSize = ref(50)
const totalCount = ref(0)
const totalPages = ref(0)
const hasPreviousPage = ref(false)
const hasNextPage = ref(false)
```

**Modals:**
```typescript
const showDeleteAllModal = ref(false)
```

### Computed Properties

**Workspace context:**
```typescript
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)
```

**Permissions:**
```typescript
const canImport = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})
```

**Duplicate detection:**
```typescript
const hasPotentialDuplicates = computed(() => {
  return transactions.value.some(
    (t) => t.duplicateStatus === DuplicateStatus.PotentialDuplicate
  )
})
```

**Session storage key:**
```typescript
const sessionStorageKey = computed(() => {
  return currentTenantKey.value ? `import-review-selections-${currentTenantKey.value}` : null
})
```

### Watchers

**Workspace changes:**
```typescript
watch(currentTenantKey, async (newKey) => {
  if (newKey) {
    await loadPendingReview()
    restoreSelections()
  } else {
    transactions.value = []
    selectedKeys.value.clear()
  }
})
```

## Type Safety

### Importing Types from apiclient.ts

All API client types are imported from the auto-generated [`apiclient.ts`](src/FrontEnd.Nuxt/app/utils/apiclient.ts):

```typescript
import {
  ImportClient,
  TenantRole,
  type ImportReviewTransactionDto,
  type PaginatedResultDto,
  type ImportResultDto,
  type IProblemDetails,
  DuplicateStatus,
} from '~/utils/apiclient'
```

**Key types:**
- `ImportClient` - API client class (instantiated with baseUrl and authFetch)
- `ImportReviewTransactionDto` - Transaction in review state
- `ImportResultDto` - Upload result statistics
- `PaginatedResultDto<T>` - Generic paginated response
- `DuplicateStatus` - Enum (New, ExactDuplicate, PotentialDuplicate)
- `TenantRole` - Enum (Owner, Editor, Viewer)
- `IProblemDetails` - Error response contract

### TypeScript Strict Mode

**Null safety:**
- Optional chaining for transaction keys: `transaction.key!`
- Null checks before API calls: `if (!currentTenantKey.value) return`
- Type guards for computed properties

**Type annotations:**
- Function parameters: `(key: string)`
- Return types: `: Promise<void>`
- Event handlers: `(event: Event)`

## API Integration

### Authentication

**useAuthFetch composable:**
```typescript
const authFetch = useAuthFetch()
const importClient = new ImportClient(baseUrl, authFetch)
```

**Behavior:**
- Automatically adds `Authorization: Bearer <token>` header to all requests
- Handles token refresh if expired
- Throws errors for authentication failures (401)

### Error Handling

**handleApiError utility:**
```typescript
error.value = handleApiError(err, 'Upload Failed', `Failed to upload ${file.name}`)
showError.value = true
```

**Benefits:**
- Extracts ProblemDetails from API responses
- Provides default fallback messages
- Returns standardized error object for ErrorDisplay component

### API Client Methods Used

**Upload file:**
```typescript
const result: ImportResultDto = await importClient.uploadFile(file)
```

**Get pending review (paginated):**
```typescript
const result: PaginatedResultDto<ImportReviewTransactionDto> =
  await importClient.getPendingReview(currentPage.value, pageSize.value)
```

**Accept transactions:**
```typescript
const keysArray = Array.from(selectedKeys.value)
await importClient.acceptTransactions(keysArray)
```

**Delete all:**
```typescript
await importClient.deleteAllPendingReview()
```

## Testing Considerations

**Functional tests (Playwright):**
- File upload with valid OFX file (verify status pane displays results)
- Review pending imports (verify table displays transactions)
- Selection toggle (verify checkboxes work, session storage persists)
- Pagination navigation (verify page changes load correct data)
- Accept transactions (verify navigation to transactions page)
- Delete all (verify confirmation modal, transactions removed)
- Permission checks (verify Viewer role cannot access page)

**Component tests (Vue Test Utils):**
- File selection handler
- Checkbox toggle logic
- Session storage save/restore
- Default selection logic
- Pagination calculations

**Test patterns:**
- Use `data-test-id` attributes for element selection
- Mock API client responses
- Test loading/error states
- Verify computed properties

## Implementation Checklist

- [ ] Create [`import.vue`](src/FrontEnd.Nuxt/app/pages/import.vue) page component
- [ ] Implement file upload section with multi-file support
- [ ] Add upload status pane with dismissible alert
- [ ] Implement transaction review table with pagination
- [ ] Add checkbox selection with session storage persistence
- [ ] Implement potential duplicates alert
- [ ] Add action buttons (Import, Delete All)
- [ ] Create delete all confirmation modal
- [ ] Add navigation item to SiteHeader component
- [ ] Test file upload with valid/invalid files
- [ ] Test pagination navigation
- [ ] Test session storage persistence across page reloads
- [ ] Test workspace switching clears/restores selections
- [ ] Write functional tests for import workflow
- [ ] Verify permission enforcement (Editor/Owner only)

## References

**Design Documents:**
- [`DESIGN-BANK-IMPORT.md`](DESIGN-BANK-IMPORT.md) - Overall feature architecture
- [`DESIGN-BANK-IMPORT-API.md`](DESIGN-BANK-IMPORT-API.md) - API layer design (ImportController)
- [`DESIGN-BANK-IMPORT-APPLICATION.md`](DESIGN-BANK-IMPORT-APPLICATION.md) - Application layer design (ImportReviewFeature)
- [`DESIGN-BANK-IMPORT-DATABASE.md`](DESIGN-BANK-IMPORT-DATABASE.md) - Database schema and entities
- [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - Product requirements

**Visual Design:**
- [`VISUAL-DESIGN-BANK-IMPORT.md`](VISUAL-DESIGN-BANK-IMPORT.md) - UI design and interaction patterns
- [`MOCKUP-BANK-IMPORT.md`](MOCKUP-BANK-IMPORT.md) - Visual mockups of all page states

**Project Standards:**
- [`.roorules`](../../.roorules) - Project coding standards and patterns
- [`src/FrontEnd.Nuxt/.roorules`](src/FrontEnd.Nuxt/.roorules) - Frontend-specific patterns

**Related Code:**
- [`transactions/index.vue`](src/FrontEnd.Nuxt/app/pages/transactions/index.vue) - Reference page implementation
- [`apiclient.ts`](src/FrontEnd.Nuxt/app/utils/apiclient.ts) - Auto-generated API client
- [`useAuthFetch.ts`](src/FrontEnd.Nuxt/app/composables/useAuthFetch.ts) - Authentication wrapper
- [`errorHandler.ts`](src/FrontEnd.Nuxt/app/utils/errorHandler.ts) - API error handling utility
