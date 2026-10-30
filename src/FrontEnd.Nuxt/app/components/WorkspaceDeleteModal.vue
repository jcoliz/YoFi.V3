<script setup lang="ts">
/**
 * Deletes a workspace, including API error handling and clearing its selection.
 */
import { TenantClient, type TenantRoleResultDto, type IProblemDetails } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  show: boolean
  workspace: TenantRoleResultDto | undefined
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
  deleted: []
}>()

const userPreferencesStore = useUserPreferencesStore()
const tenantClient = useApiClient(TenantClient)

const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)

watch(
  () => [props.show, props.workspace] as const,
  ([show]) => {
    if (!show) return

    error.value = undefined
    showError.value = false
  },
  { immediate: true },
)

async function deleteWorkspace() {
  if (loading.value) return

  const workspaceKey = props.workspace?.key
  if (!workspaceKey) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to delete',
    }
    showError.value = true
    return
  }

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await tenantClient.deleteTenant(workspaceKey)

    if (workspaceKey === userPreferencesStore.getCurrentTenantKey) {
      userPreferencesStore.clearPreferences()
    }

    emit('update:show', false)
    emit('deleted')
  } catch (err) {
    error.value = handleApiError(err, 'Delete Failed', 'Failed to delete workspace')
    showError.value = true
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <ModalDialog
    :show="show"
    title="Delete Workspace"
    :loading="loading"
    primary-button-variant="danger"
    :primary-button-text="loading ? 'Deleting...' : 'Delete'"
    primary-button-test-id="delete-submit-button"
    secondary-button-test-id="delete-cancel-button"
    test-id="delete-modal"
    @primary="deleteWorkspace"
    @update:show="emit('update:show', $event)"
  >
    <ErrorDisplay
      v-model:show="showError"
      :problem="error"
    />

    <p>
      Are you sure you want to delete the workspace
      <strong data-test-id="deleting-workspace-name">{{ workspace?.name }}</strong
      >?
    </p>
    <p class="text-danger">
      <strong>Warning:</strong> This action cannot be undone. All data associated with this
      workspace will be permanently deleted.
    </p>
  </ModalDialog>
</template>
