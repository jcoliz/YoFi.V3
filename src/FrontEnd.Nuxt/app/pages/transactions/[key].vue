<script setup lang="ts">
/**
 * Transaction Details Page
 *
 * Displays full details for a single transaction with inline editing capability
 */

import {
  TransactionsClient,
  TenantRole,
  type TransactionDetailDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Transaction Details',
  layout: 'chrome',
})

const route = useRoute()
const router = useRouter()
const userPreferencesStore = useUserPreferencesStore()

// API Client
const transactionsClient = useApiClient(TransactionsClient)

// Page ready state (for SSR/hydration)
const ready = ref(false)

// State
const transaction = ref<TransactionDetailDto | null>(null)
const loading = ref(false)
const saving = ref(false)
const editSaving = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const showDeleteModal = ref(false)

// Editing state
const isEditing = ref(false)

// Computed
const transactionKey = computed(() => route.params.key as string)
const currentTenantKey = computed(() => userPreferencesStore.getCurrentTenantKey)
const hasWorkspace = computed(() => userPreferencesStore.hasTenant)
const canEditTransactions = computed(() => {
  const currentTenant = userPreferencesStore.getCurrentTenant
  if (!currentTenant?.role) return false
  return currentTenant.role === TenantRole.Editor || currentTenant.role === TenantRole.Owner
})

/**
 * This page requires an authenticated user. If not authenticated, redirect to login page.
 * Only after we've confirmed authentication, do we call getData() to load the data for this page.
 */
useAuthenticationGuard().requireAuthenticatedUser(afterMounted)

// Load transaction after authentication is confirmed
async function afterMounted() {
  ready.value = true
  userPreferencesStore.loadFromStorage()
  if (currentTenantKey.value && transactionKey.value) {
    await loadTransaction()
  }
}

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
  isEditing.value = true
}

function cancelEditing() {
  isEditing.value = false
}

function handleTransactionUpdated(updatedTransaction: TransactionDetailDto) {
  transaction.value = updatedTransaction
  isEditing.value = false
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
            :disabled="loading || !ready"
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
            type="submit"
            form="transaction-edit-form"
            :disabled="editSaving"
          >
            <FeatherIcon
              icon="save"
              size="16"
              class="me-1"
            />
            {{ editSaving ? 'Saving...' : 'Save' }}
          </button>
          <button
            class="btn btn-outline-secondary"
            data-test-id="cancel-edit-button"
            :disabled="editSaving"
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
              <label class="text-muted small">Category</label>
              <div data-test-id="transaction-category">
                {{ transaction.category || '(none)' }}
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
      <TransactionEditPane
        v-else-if="transaction && isEditing"
        :transaction="transaction"
        @updated="handleTransactionUpdated"
        @saving="editSaving = $event"
      />
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
