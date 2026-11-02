<script setup lang="ts">
/**
 * Creates workspaces, including validation and API error handling.
 */
import { TenantClient, TenantEditDto, type IProblemDetails } from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  show: boolean
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
  created: []
}>()

const tenantClient = useApiClient(TenantClient)

const creating = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const name = ref('')
const description = ref('')

watch(
  () => props.show,
  (show) => {
    if (!show) return

    name.value = ''
    description.value = ''
    error.value = undefined
    showError.value = false
  },
  { immediate: true },
)

async function createWorkspace() {
  if (creating.value) return

  if (!name.value.trim()) {
    error.value = {
      title: 'Validation Error',
      detail: 'Workspace name is required',
    }
    showError.value = true
    return
  }

  creating.value = true
  error.value = undefined
  showError.value = false

  try {
    const newTenant = new TenantEditDto({
      name: name.value.trim(),
      description: description.value.trim() || undefined,
    })

    await tenantClient.createTenant(newTenant)
    emit('update:show', false)
    emit('created')
  } catch (err) {
    error.value = handleApiError(err, 'Create Failed', 'Failed to create workspace')
    showError.value = true
  } finally {
    creating.value = false
  }
}
</script>

<template>
  <div
    v-if="show"
    class="card mb-4"
    data-test-id="create-form-card"
  >
    <div class="card-header bg-primary text-white">
      <h5 class="mb-0">Create New Workspace</h5>
    </div>
    <div class="card-body">
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
      />

      <div class="mb-3">
        <label
          for="create-name"
          class="form-label"
          >Name <span class="text-danger">*</span></label
        >
        <input
          id="create-name"
          v-model="name"
          type="text"
          class="form-control"
          placeholder="Enter workspace name"
          :disabled="creating"
          @keyup.enter="createWorkspace"
        />
      </div>

      <div class="mb-3">
        <label
          for="create-description"
          class="form-label"
          >Description</label
        >
        <textarea
          id="create-description"
          v-model="description"
          class="form-control"
          rows="3"
          placeholder="Enter workspace description (optional)"
          :disabled="creating"
        />
      </div>

      <div class="d-flex gap-2">
        <button
          class="btn btn-primary"
          :disabled="creating || !name.trim()"
          data-test-id="create-submit-button"
          @click="createWorkspace"
        >
          <BaseSpinner
            v-if="creating"
            size="sm"
            class="me-1"
          />
          {{ creating ? 'Creating...' : 'Create' }}
        </button>
        <button
          class="btn btn-secondary"
          :disabled="creating"
          data-test-id="create-cancel-button"
          @click="emit('update:show', false)"
        >
          Cancel
        </button>
      </div>
    </div>
  </div>
</template>
