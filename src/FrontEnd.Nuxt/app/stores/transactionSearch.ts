/**
 * Transaction Search Store
 *
 * Holds the transactions page search term, filter criteria, and current page so they are retained
 * when navigating away from the transactions list (e.g. to a transaction's details
 * page) and back. Add new filter dimensions here as they are introduced.
 * State is in-memory only; it does not persist across full page reloads.
 */

import { defineStore } from 'pinia'

export const useTransactionSearchStore = defineStore('transactionSearch', () => {
  // State
  const searchText = ref('')

  /**
   * Start date filter (inclusive), in YYYY-MM-DD format, or empty for no lower bound.
   */
  const fromDate = ref('')

  /**
   * End date filter (inclusive), in YYYY-MM-DD format, or empty for no upper bound.
   */
  const toDate = ref('')

  /**
   * Current page number (1-based).
   */
  const currentPage = ref(1)

  // Getters

  /**
   * Whether any filter (excluding the search term) is currently applied.
   */
  const hasActiveFilters = computed(() => !!fromDate.value || !!toDate.value)

  // Actions
  function clearSearch() {
    searchText.value = ''
  }

  /**
   * Clears all filters, leaving the search term intact.
   */
  function clearFilters() {
    fromDate.value = ''
    toDate.value = ''
  }

  /**
   * Clears the search term and all filters, and returns to page 1.
   */
  function reset() {
    clearSearch()
    clearFilters()
    currentPage.value = 1
  }

  return {
    // State
    searchText,
    fromDate,
    toDate,
    currentPage,
    // Getters
    hasActiveFilters,
    // Actions
    clearSearch,
    clearFilters,
    reset,
  }
})
