<script setup lang="ts">
/**
 * ImportReviewTable.vue
 *
 * Import-specific transaction review table with checkbox selection.
 * Displays transactions with duplicate status highlighting and supports
 * individual selection for import approval.
 * Uses server-side selection state from transaction.isSelected property.
 *
 * Props:
 * - transactions: ImportReviewTransactionDto[] - Transactions to display (includes isSelected)
 * - loading: boolean - Loading state for the table
 *
 * Events:
 * - @toggleSelection(key: string) - Emitted when individual checkbox toggled
 */

import type { ImportReviewTransactionDto } from '~/utils/apiclient'
import { DuplicateStatus } from '~/utils/apiclient'

interface Props {
  transactions?: ImportReviewTransactionDto[]
  loading?: boolean
}

// eslint-disable-next-line no-unused-vars
const props = withDefaults(defineProps<Props>(), {
  transactions: () => [],
  loading: false,
})

const emit = defineEmits<{
  toggleSelection: [key: string]
}>()

/**
 * Checks if transaction has potential duplicate status
 */
const isPotentialDuplicate = (transaction: ImportReviewTransactionDto): boolean => {
  return transaction.duplicateStatus === DuplicateStatus.PotentialDuplicate
}

/**
 * Handles individual checkbox toggle
 */
const handleToggle = (key: string) => {
  emit('toggleSelection', key)
}

/**
 * Formats amount for display with proper sign and currency
 */
const formatAmount = (amount: number): string => {
  const formatted = Math.abs(amount).toFixed(2)
  return amount < 0 ? `-$${formatted}` : `$${formatted}`
}

/**
 * Formats date for display
 */
const formatDate = (date: Date): string => {
  return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="import-review-table">
    <!-- Loading State -->
    <div
      v-if="loading"
      class="text-center py-5"
      data-test-id="loading-state"
    >
      <BaseSpinner size="lg" />
      <p class="text-muted mt-3">Loading transactions...</p>
    </div>

    <!-- Empty State -->
    <div
      v-else-if="transactions.length === 0"
      class="text-center py-5"
      data-test-id="empty-state"
    >
      <i class="bi bi-inbox fs-1 text-muted"></i>
      <p class="text-muted mt-3 mb-0">No pending imports</p>
      <p class="text-muted">Upload bank files to get started</p>
    </div>

    <!-- Transaction Table -->
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
            <th
              data-test-id="selected"
              style="width: 50px"
            >
              <span
                class="text-muted"
                title="Selection managed via action buttons"
              >
                <i class="bi bi-check-square"></i>
              </span>
            </th>
            <th
              data-test-id="date"
              style="width: 120px"
            >
              Date
            </th>
            <th data-test-id="payee">Payee</th>
            <th
              data-test-id="category"
              style="width: 150px"
            >
              Category
            </th>
            <th
              data-test-id="amount"
              style="width: 120px"
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
            :class="{ 'table-warning': isPotentialDuplicate(transaction) }"
            :data-test-id="`row-${transaction.key}`"
          >
            <td>
              <input
                type="checkbox"
                class="form-check-input"
                :data-test-id="`select-checkbox-${transaction.key}`"
                :checked="transaction.isSelected"
                @change="handleToggle(transaction.key!)"
              />
            </td>
            <td>
              <span v-if="isPotentialDuplicate(transaction)">
                <i
                  class="bi bi-exclamation-triangle-fill text-warning me-1"
                  title="Potential duplicate"
                ></i>
              </span>
              {{ formatDate(transaction.date!) }}
            </td>
            <td>{{ transaction.payee }}</td>
            <td class="text-muted">
              <small>{{ transaction.category || '—' }}</small>
            </td>
            <td class="text-end">
              {{ formatAmount(transaction.amount!) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
/* Ensure checkbox alignment */
.form-check-input {
  cursor: pointer;
}

/* Highlight potential duplicates */
.table-warning {
  background-color: #fff3cd;
}

.table-warning:hover {
  background-color: #ffe69c;
}

/* Amount coloring */
.text-danger {
  font-weight: 500;
}

.text-success {
  font-weight: 500;
}
</style>
