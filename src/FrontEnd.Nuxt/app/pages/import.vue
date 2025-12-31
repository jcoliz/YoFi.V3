<script setup lang="ts">
/**
 * Import Page (Temporary)
 *
 * Temporary page to test and demonstrate FileUploadSection, UploadStatusPane, and ImportReviewTable components.
 * This will be expanded with the full import workflow in future iterations.
 */

import type { ImportReviewTransactionDto } from '~/utils/apiclient'
import { DuplicateStatus } from '~/utils/apiclient'

// Page metadata
definePageMeta({
  layout: 'default',
  auth: false, // Temporary: No auth required for testing
})

// State
const selectedFiles = ref<File[]>([])
const uploadInProgress = ref(false)
const statusMessages = ref<string[]>([])
const showStatusPane = ref(false)
const statusVariant = ref<'info' | 'warning' | 'success' | 'danger'>('info')

// Transaction review state
const transactions = ref<ImportReviewTransactionDto[]>([])
const selectedKeys = ref<Set<string>>(new Set())
const loading = ref(false)

/**
 * Generates fake transaction data for testing
 */
const generateFakeTransactions = (count: number) => {
  const payees = [
    'Amazon.com',
    'Whole Foods',
    'Shell Gas Station',
    'Starbucks',
    'Netflix',
    'AT&T Wireless',
    'PG&E',
    'Safeway',
    'Target',
    'Costco',
    'Chevron',
    'Apple Store',
    'Restaurant ABC',
    'Gym Membership',
    'Insurance Co',
  ]

  const categories = [
    'Groceries',
    'Gas',
    'Utilities',
    'Entertainment',
    'Shopping',
    'Dining',
    'Healthcare',
  ]

  const fakeTransactions: any[] = []

  for (let i = 0; i < count; i++) {
    const daysAgo = Math.floor(Math.random() * 90)
    const date = new Date()
    date.setDate(date.getDate() - daysAgo)

    // Randomly assign duplicate status (80% new, 10% exact duplicate, 10% potential duplicate)
    const rand = Math.random()
    let duplicateStatus: DuplicateStatus
    if (rand < 0.8) {
      duplicateStatus = DuplicateStatus.New
    } else if (rand < 0.9) {
      duplicateStatus = DuplicateStatus.ExactDuplicate
    } else {
      duplicateStatus = DuplicateStatus.PotentialDuplicate
    }

    fakeTransactions.push({
      key: `fake-${i}-${Date.now()}`,
      date: date,
      payee: payees[Math.floor(Math.random() * payees.length)],
      category: categories[Math.floor(Math.random() * categories.length)],
      amount: parseFloat((Math.random() * 200 - 50).toFixed(2)), // -50 to 150
      duplicateStatus: duplicateStatus,
      duplicateOfKey: duplicateStatus !== DuplicateStatus.New ? `duplicate-ref-${i}` : undefined,
    })
  }

  return fakeTransactions.sort((a, b) => b.date.getTime() - a.date.getTime())
}

/**
 * Handles file selection from FileUploadSection component
 */
const handleFilesSelected = (files: File[]) => {
  console.log('Files selected:', files)
  selectedFiles.value = files

  // Display selected file names
  const fileNames = files.map((f) => f.name).join(', ')
  console.log('File names:', fileNames)
}

/**
 * Simulates uploading the selected files with status updates
 */
const uploadFiles = async () => {
  if (selectedFiles.value.length === 0) {
    return
  }

  uploadInProgress.value = true
  statusMessages.value = []
  showStatusPane.value = true
  statusVariant.value = 'info'

  // Clear existing transactions
  transactions.value = []
  selectedKeys.value.clear()

  try {
    // Process each file sequentially
    for (const file of selectedFiles.value) {
      // Show uploading status
      statusMessages.value.push(`⏳ ${file.name}: Importing...`)

      // Simulate upload delay
      await new Promise((resolve) => setTimeout(resolve, 1000))

      // Remove the "Importing..." message
      statusMessages.value = statusMessages.value.filter((msg) => !msg.includes('Importing...'))

      // Simulate different outcomes based on file name
      if (file.name.toLowerCase().includes('error')) {
        // Simulate complete failure
        statusMessages.value.push(`❌ ${file.name}: Upload failed`)
        statusVariant.value = 'danger'
      } else if (file.name.toLowerCase().includes('warning')) {
        // Simulate partial success
        const transactionCount = Math.floor(Math.random() * 100) + 50
        const errorCount = Math.floor(Math.random() * 10) + 1
        statusMessages.value.push(
          `⚠ ${file.name}: ${transactionCount} transactions added, ${errorCount} errors detected <a href="#" class="alert-link">[View Errors]</a>`,
        )
        statusVariant.value = 'warning'

        // Generate fake transactions
        const newTransactions = generateFakeTransactions(transactionCount)
        transactions.value.push(...newTransactions)
      } else {
        // Simulate complete success
        const transactionCount = Math.floor(Math.random() * 200) + 50
        statusMessages.value.push(`✓ ${file.name}: ${transactionCount} transactions added`)
        if (statusVariant.value !== 'danger' && statusVariant.value !== 'warning') {
          statusVariant.value = 'success'
        }

        // Generate fake transactions
        const newTransactions = generateFakeTransactions(transactionCount)
        transactions.value.push(...newTransactions)
      }
    }

    // Set default selections (select New transactions, deselect duplicates)
    transactions.value.forEach((transaction) => {
      if (transaction.duplicateStatus === DuplicateStatus.New) {
        selectedKeys.value.add(transaction.key!)
      }
    })

    console.log('Upload complete!', transactions.value.length, 'transactions')

    // Clear selection after upload
    selectedFiles.value = []
  } finally {
    uploadInProgress.value = false
  }
}

/**
 * Handles closing the status pane
 */
const handleCloseStatusPane = () => {
  showStatusPane.value = false
  statusMessages.value = []
}

/**
 * Handles individual checkbox toggle
 */
const handleToggleSelection = (key: string) => {
  if (selectedKeys.value.has(key)) {
    selectedKeys.value.delete(key)
  } else {
    selectedKeys.value.add(key)
  }
}

/**
 * Handles "select all" checkbox toggle
 */
const handleToggleAll = () => {
  if (transactions.value.every((t) => selectedKeys.value.has(t.key!))) {
    // All selected, so deselect all
    selectedKeys.value.clear()
  } else {
    // Some or none selected, so select all
    transactions.value.forEach((t) => selectedKeys.value.add(t.key!))
  }
}

/**
 * Handles Import button click (temporary: clears transaction list)
 */
const handleImport = () => {
  console.log('Import clicked:', selectedKeys.value.size, 'transactions selected')
  // Temporary: clear transaction list to simulate successful import
  transactions.value = []
  selectedKeys.value.clear()
}

/**
 * Handles Delete All button click (temporary: clears transaction list)
 */
const handleDeleteAll = () => {
  console.log('Delete All clicked')
  // Temporary: clear transaction list
  transactions.value = []
  selectedKeys.value.clear()
}

/**
 * Computed: Check if there are any transactions
 */
const hasTransactions = computed(() => transactions.value.length > 0)

/**
 * Computed: Check if any transactions are selected
 */
const hasSelections = computed(() => selectedKeys.value.size > 0)

/**
 * Computed: Check if there are any potential duplicates in the transaction list
 */
const hasPotentialDuplicates = computed(() => {
  return transactions.value.some((t) => t.duplicateStatus === DuplicateStatus.PotentialDuplicate)
})

/**
 * Generates test transactions for testing (without file upload)
 */
const generateTestData = () => {
  console.log('Generating test transactions...')
  const testTransactions = generateFakeTransactions(50)
  transactions.value = testTransactions

  // Select New transactions by default
  selectedKeys.value.clear()
  testTransactions.forEach((transaction) => {
    if (transaction.duplicateStatus === DuplicateStatus.New) {
      selectedKeys.value.add(transaction.key!)
    }
  })

  console.log('Generated', testTransactions.length, 'transactions')
  const potentialDupes = testTransactions.filter(
    (t) => t.duplicateStatus === DuplicateStatus.PotentialDuplicate,
  )
  console.log('Potential duplicates:', potentialDupes.length)
}
</script>

<template>
  <div class="container py-4">
    <div class="row">
      <div class="col-lg-8 mx-auto">
        <h1
          class="mb-4"
          data-test-id="page-heading"
        >
          Import Bank Transactions
        </h1>

        <!-- Upload Status Pane -->
        <UploadStatusPane
          :show="showStatusPane"
          :status-messages="statusMessages"
          :variant="statusVariant"
          @close="handleCloseStatusPane"
        />

        <div class="card">
          <div class="card-body">
            <h5 class="card-title">Upload Files</h5>
            <p class="text-muted mb-4">
              Select OFX or QFX files from your bank to import transactions.
            </p>

            <!-- File Upload Section Component -->
            <FileUploadSection
              :disabled="uploadInProgress"
              accept=".ofx,.qfx"
              :multiple="true"
              @files-selected="handleFilesSelected"
            />

            <!-- Selected Files Display -->
            <div
              v-if="selectedFiles.length > 0"
              class="mt-3"
            >
              <h6>Selected Files:</h6>
              <ul class="list-group">
                <li
                  v-for="(file, index) in selectedFiles"
                  :key="index"
                  class="list-group-item"
                >
                  <strong>{{ file.name }}</strong>
                  <span class="text-muted ms-2">({{ Math.round(file.size / 1024) }} KB)</span>
                </li>
              </ul>

              <!-- Upload Button -->
              <button
                type="button"
                class="btn btn-primary mt-3"
                data-test-id="upload-button"
                :disabled="uploadInProgress"
                @click="uploadFiles"
              >
                <BaseSpinner
                  v-if="uploadInProgress"
                  size="sm"
                  class="me-1"
                />
                {{ uploadInProgress ? 'Uploading...' : 'Upload Files' }}
              </button>
            </div>

            <!-- Empty State -->
            <div
              v-else
              class="alert alert-info mt-3"
            >
              <strong>No files selected.</strong> Click "Browse..." to select files.
            </div>
          </div>
        </div>

        <!-- Transaction Review Table -->
        <div
          v-if="transactions.length > 0"
          class="card mt-4"
        >
          <div class="card-body">
            <h5 class="card-title">Review Transactions</h5>
            <p class="text-muted mb-4">
              Review and select transactions to import. Transactions marked as duplicates are
              deselected by default.
            </p>

            <!-- Duplicates Alert -->
            <ImportDuplicatesAlert :show="hasPotentialDuplicates" />

            <ImportActionButtons
              :has-transactions="hasTransactions"
              :has-selections="hasSelections"
              :loading="loading"
              :uploading="uploadInProgress"
              class="mb-3 d-flex justify-content-end"
              @import="handleImport"
              @delete-all="handleDeleteAll"
            />

            <ImportReviewTable
              :transactions="transactions"
              :selected-keys="selectedKeys"
              :loading="loading"
              @toggle-selection="handleToggleSelection"
              @toggle-all="handleToggleAll"
            />
          </div>
        </div>

        <!-- Temporary Notice -->
        <div class="alert alert-warning mt-4">
          <strong>⚠️ Temporary Page</strong><br />
          This is a temporary implementation to demonstrate the FileUploadSection, UploadStatusPane,
          ImportReviewTable, ImportActionButtons, and ImportDuplicatesAlert components. The full
          import workflow (pagination, modals, etc.) will be added in future iterations.
          <hr />
          <small
            ><strong>Testing tips:</strong> File names containing "error" will simulate upload
            failures. File names containing "warning" will simulate partial success with errors.
            Other files will simulate complete success and generate fake transaction data.</small
          >
          <hr />
          <button
            type="button"
            class="btn btn-sm btn-outline-secondary"
            data-test-id="generate-test-data-button"
            @click="generateTestData"
          >
            🧪 Generate Test Data (50 transactions)
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
