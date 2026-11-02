<script setup lang="ts">
/**
 * ImportActionButtons.vue
 *
 * Import-specific action buttons for the import review workflow.
 * Provides "Import" and "Delete All" buttons with proper enable/disable logic.
 *
 * Props:
 * - hasTransactions: boolean - Whether there are any transactions to display
 * - hasSelections: boolean - Whether any transactions are selected
 * - loading: boolean - Loading state for API operations
 * - uploading: boolean - Uploading state for file uploads
 *
 * Events:
 * - @import - Emitted when "Import" button clicked
 * - @deleteAll - Emitted when "Delete All" button clicked
 */

interface Props {
  hasTransactions?: boolean
  hasSelections?: boolean
  loading?: boolean
  uploading?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  hasTransactions: false,
  hasSelections: false,
  loading: false,
  uploading: false,
})

const emit = defineEmits<{
  import: []
  deleteAll: []
}>()

/**
 * Checks if Import button should be disabled
 */
const importDisabled = computed(() => {
  return !props.hasSelections || props.loading || props.uploading
})

/**
 * Checks if Delete All button should be disabled
 */
const deleteAllDisabled = computed(() => {
  return !props.hasTransactions || props.loading || props.uploading
})

/**
 * Handles Import button click
 */
const handleImport = () => {
  if (!importDisabled.value) {
    emit('import')
  }
}

/**
 * Handles Delete All button click
 */
const handleDeleteAll = () => {
  if (!deleteAllDisabled.value) {
    emit('deleteAll')
  }
}
</script>

<template>
  <div class="import-action-buttons d-flex gap-2">
    <button
      type="button"
      class="btn btn-outline-danger"
      data-test-id="delete-all-button"
      :disabled="deleteAllDisabled"
      @click="handleDeleteAll"
    >
      <BaseSpinner
        v-if="loading"
        size="sm"
        class="me-1"
      />
      {{ loading ? 'Deleting...' : 'Delete All' }}
    </button>

    <button
      type="button"
      class="btn btn-primary"
      data-test-id="import-button"
      :disabled="importDisabled"
      @click="handleImport"
    >
      <BaseSpinner
        v-if="loading"
        size="sm"
        class="me-1"
      />
      {{ loading ? 'Importing...' : 'Import' }}
    </button>
  </div>
</template>

<style scoped>
.import-action-buttons {
  margin-top: 1rem;
}
</style>
