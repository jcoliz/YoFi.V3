<script setup lang="ts">
/**
 * Confirms and completes an import using the server-side transaction selections.
 */
import { ImportClient, type ImportReviewSummaryDto, type IProblemDetails } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

defineProps<{
  show: boolean
  summary: ImportReviewSummaryDto | undefined
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
  importing: []
  imported: []
  error: [problem: IProblemDetails]
}>()

const userPreferencesStore = useUserPreferencesStore()
const importClient = useApiClient(ImportClient)
const importing = ref(false)
const currentTenantName = computed(() => userPreferencesStore.getCurrentTenant?.name || '')

async function confirmImport() {
  if (importing.value) return

  const tenantKey = userPreferencesStore.getCurrentTenantKey
  if (!tenantKey) {
    emit('update:show', false)
    emit('error', {
      title: 'No workspace selected',
      detail: 'Please select a workspace to import transactions',
    })
    return
  }

  importing.value = true
  emit('importing')

  try {
    await importClient.completeReview(tenantKey)
    emit('update:show', false)
    emit('imported')
  } catch (err) {
    emit('update:show', false)
    emit('error', handleApiError(err, 'Import Failed', 'Failed to import transactions'))
  } finally {
    importing.value = false
  }
}
</script>

<template>
  <ModalDialog
    :show="show"
    title="Confirm Import"
    :loading="importing"
    primary-button-variant="primary"
    :primary-button-text="importing ? 'Importing...' : 'Import'"
    secondary-button-text="Cancel"
    primary-button-test-id="import-confirm-button"
    secondary-button-test-id="import-cancel-button"
    test-id="import-confirm-modal"
    @primary="confirmImport"
    @update:show="emit('update:show', $event)"
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
          <strong data-test-id="import-selected-count">{{ summary.selectedCount || 0 }}</strong>
          transaction{{ summary.selectedCount === 1 ? '' : 's' }}
          will be imported
        </li>
        <li v-if="summary.totalCount && summary.totalCount > (summary.selectedCount || 0)">
          <strong data-test-id="import-discarded-count">{{
            summary.totalCount - (summary.selectedCount || 0)
          }}</strong>
          transaction{{ summary.totalCount - (summary.selectedCount || 0) === 1 ? '' : 's' }}
          will be discarded
        </li>
        <li v-if="summary.potentialDuplicateCount && summary.potentialDuplicateCount > 0">
          <strong data-test-id="import-potential-duplicate-count">{{
            summary.potentialDuplicateCount
          }}</strong>
          potential duplicate{{ summary.potentialDuplicateCount === 1 ? '' : 's' }}
          detected
        </li>
      </ul>
    </div>

    <p class="mb-0">
      <strong>Do you want to proceed with this import?</strong>
    </p>
  </ModalDialog>
</template>
