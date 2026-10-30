<script setup lang="ts">
/**
 * Edits a workspace, including validation and API error handling.
 */
import {
  TenantClient,
  TenantEditDto,
  type TenantRoleResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  workspace: TenantRoleResultDto
}>()

const emit = defineEmits<{
  cancel: []
  updated: []
}>()

const tenantClient = useApiClient(TenantClient)

const updating = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const name = ref('')
const description = ref('')

watch(
  () => props.workspace,
  (workspace) => {
    name.value = workspace.name || ''
    description.value = workspace.description || ''
    error.value = undefined
    showError.value = false
  },
  { immediate: true },
)

async function updateWorkspace() {
  if (updating.value) return

  if (!name.value.trim()) {
    error.value = {
      title: 'Validation Error',
      detail: 'Workspace name is required',
    }
    showError.value = true
    return
  }

  const workspaceKey = props.workspace.key
  if (!workspaceKey) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to update',
    }
    showError.value = true
    return
  }

  updating.value = true
  error.value = undefined
  showError.value = false

  try {
    const updateDto = new TenantEditDto({
      name: name.value.trim(),
      description: description.value.trim() || undefined,
    })

    await tenantClient.updateTenant(workspaceKey, updateDto)
    emit('updated')
  } catch (err) {
    error.value = handleApiError(err, 'Update Failed', 'Failed to update workspace')
    showError.value = true
  } finally {
    updating.value = false
  }
}
</script>

<template>
  <div data-test-id="edit-form">
    <ErrorDisplay
      v-model:show="showError"
      :problem="error"
      class="mb-3"
    />

    <div class="mb-3">
      <label class="form-label">Name <span class="text-danger">*</span></label>
      <input
        v-model="name"
        type="text"
        class="form-control form-control-sm"
        :disabled="updating"
        data-test-id="edit-workspace-name"
      />
    </div>

    <div class="mb-3">
      <label class="form-label">Description</label>
      <textarea
        v-model="description"
        class="form-control form-control-sm"
        rows="2"
        :disabled="updating"
        data-test-id="edit-workspace-description"
      />
    </div>

    <div class="d-flex gap-2">
      <button
        class="btn btn-sm btn-primary"
        :disabled="updating || !name.trim()"
        data-test-id="edit-workspace-submit"
        @click="updateWorkspace"
      >
        <BaseSpinner
          v-if="updating"
          size="sm"
          class="me-1"
        />
        {{ updating ? 'Saving...' : 'Save' }}
      </button>
      <button
        class="btn btn-sm btn-secondary"
        :disabled="updating"
        data-test-id="edit-workspace-cancel"
        @click="emit('cancel')"
      >
        Cancel
      </button>
    </div>
  </div>
</template>
