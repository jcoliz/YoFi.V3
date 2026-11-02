<script setup lang="ts">
/**
 * Confirms and deletes all pending imports in the selected workspace.
 */
import { ImportClient, type IProblemDetails } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

defineProps<{
  show: boolean
  transactionCount: number
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
  'update:loading': [value: boolean]
  deleting: []
  deleted: []
  error: [problem: IProblemDetails]
}>()

const userPreferencesStore = useUserPreferencesStore()
const importClient = useApiClient(ImportClient)
const loading = ref(false)

async function deleteAll() {
  if (loading.value) return

  const tenantKey = userPreferencesStore.getCurrentTenantKey
  if (!tenantKey) {
    emit('error', {
      title: 'No workspace selected',
      detail: 'Please select a workspace to delete pending imports',
    })
    return
  }

  loading.value = true
  emit('update:loading', true)
  emit('deleting')

  try {
    await importClient.deleteAllPendingReview(tenantKey)
    emit('update:show', false)
    emit('deleted')
  } catch (err) {
    emit('error', handleApiError(err, 'Delete Failed', 'Failed to delete pending imports'))
  } finally {
    loading.value = false
    emit('update:loading', false)
  }
}
</script>

<template>
  <ModalDialog
    :show="show"
    title="Delete All Pending Imports"
    :loading="loading"
    primary-button-variant="danger"
    :primary-button-text="loading ? 'Deleting...' : 'Delete All'"
    primary-button-test-id="delete-all-submit-button"
    secondary-button-test-id="delete-all-cancel-button"
    test-id="delete-all-modal"
    @primary="deleteAll"
    @update:show="emit('update:show', $event)"
  >
    <p>Are you sure you want to delete all pending import transactions?</p>
    <div
      v-if="transactionCount > 0"
      class="alert alert-warning"
      data-test-id="delete-all-warning"
    >
      <strong>Warning:</strong> This will delete
      <strong>{{ transactionCount }}</strong>
      transaction{{ transactionCount === 1 ? '' : 's' }}
      from the import queue. This action cannot be undone.
    </div>
  </ModalDialog>
</template>
