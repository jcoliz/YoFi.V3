/**
 * Transaction Search Store
 *
 * Holds the transactions page search term, filter criteria, and current page so they are retained
 * when navigating away from the transactions list (e.g. to a transaction's details
 * page) and back. Add new filter dimensions here as they are introduced.
 * State is in-memory only; it does not persist across full page reloads.
 */

import { defineStore } from 'pinia'
import {
  emptyTransactionAdvancedFilters,
  type TransactionAdvancedFilters,
} from '~/types/transactionFilters'

export const useTransactionSearchStore = defineStore('transactionSearch', () => {
  // State
  const searchText = ref('')
  const filters = ref<TransactionAdvancedFilters>(emptyTransactionAdvancedFilters())
  const showFilters = ref(false)

  /**
   * Current page number (1-based).
   */
  const currentPage = ref(1)

  // Getters

  /**
   * Whether any filter (excluding the search term) is currently applied.
   */
  const hasActiveFilters = computed(() => {
    const activeFilters = filters.value
    return (
      !!activeFilters.fromDate ||
      !!activeFilters.toDate ||
      !!activeFilters.payee.trim() ||
      !!activeFilters.category.trim() ||
      !!activeFilters.memo.trim() ||
      !!activeFilters.amount.trim() ||
      activeFilters.uncategorizedOnly
    )
  })

  // Actions
  function clearSearch() {
    searchText.value = ''
  }

  /**
   * Clears all filters, leaving the search term intact.
   */
  function clearFilters() {
    filters.value = emptyTransactionAdvancedFilters()
  }

  function setFilters(nextFilters: TransactionAdvancedFilters) {
    filters.value = { ...nextFilters }
    currentPage.value = 1
  }

  /**
   * Clears the entire query while preserving the panel's expanded state.
   */
  function clearAll() {
    clearSearch()
    clearFilters()
    currentPage.value = 1
  }

  /**
   * Clears the search term and all filters, and returns to page 1.
   */
  function reset() {
    clearSearch()
    clearFilters()
    currentPage.value = 1
    showFilters.value = false
  }

  return {
    // State
    searchText,
    filters,
    showFilters,
    currentPage,
    // Getters
    hasActiveFilters,
    // Actions
    clearSearch,
    clearFilters,
    setFilters,
    clearAll,
    reset,
  }
})
