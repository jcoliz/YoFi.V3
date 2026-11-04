<script setup lang="ts">
import { TenantClient, type TenantRoleResultDto, type IProblemDetails } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Manage Workspace',
  layout: 'chrome',
  breadcrumbs: [{ title: 'Workspaces', to: '/workspaces' }],
})

const route = useRoute()
const router = useRouter()
const tenantClient = useApiClient(TenantClient)
const userPreferencesStore = useUserPreferencesStore()

const workspace = ref<TenantRoleResultDto>()
const loading = ref(true)
const busy = ref(false)
const editing = ref(false)
const showDeleteModal = ref(false)
const error = ref<IProblemDetails>()
const showError = ref(false)
let loadVersion = 0

onMounted(() => {
  userPreferencesStore.loadFromStorage()
  watch(() => route.params.key, loadWorkspace, { immediate: true })
})

onBeforeUnmount(() => {
  loadVersion++
})

onBeforeRouteLeave(() => !busy.value)
onBeforeRouteUpdate(() => !busy.value)

async function loadWorkspace() {
  const version = ++loadVersion
  const key = route.params.key
  workspace.value = undefined
  editing.value = false
  showDeleteModal.value = false
  error.value = undefined
  showError.value = false
  loading.value = true

  if (typeof key !== 'string' || !key) {
    error.value = {
      title: 'Workspace unavailable',
      detail: 'A workspace key is required.',
    }
    showError.value = true
    loading.value = false
    return
  }

  try {
    const result = await tenantClient.getTenant(key)
    if (version !== loadVersion) return

    workspace.value = result
    userPreferencesStore.setCurrentTenant(result)
  } catch (err) {
    if (version !== loadVersion) return

    error.value = handleApiError(err, 'Load Failed', 'Failed to load workspace')
    showError.value = true
  } finally {
    if (version === loadVersion) loading.value = false
  }
}

async function handleWorkspaceDeleted() {
  busy.value = false
  await router.push('/workspaces')
}
</script>

<template>
  <div class="container py-4">
    <PageBreadcrumb />
    <h1
      v-if="workspace?.name !== undefined"
      data-test-id="page-heading"
      class="mb-3"
    >
      {{ workspace.name }}
    </h1>

    <ErrorDisplay
      v-model:show="showError"
      :problem="error"
      class="mb-4"
    />

    <div
      v-if="loading"
      class="text-center py-5"
      data-test-id="loading-state"
    >
      <BaseSpinner size="lg" />
      <p class="mt-3 text-muted">Loading workspace...</p>
    </div>

    <template v-else-if="workspace">
      <div class="row g-4">
        <div class="col-md-6">
          <section
            class="card h-100"
            aria-labelledby="workspace-settings-heading"
          >
            <div class="card-body">
              <CardTitle
                id="workspace-settings-heading"
                class="mb-3"
                title="Workspace Settings"
                subtitle="View and edit your workspace's properties."
              />
              <WorkspaceEditPane
                v-if="editing"
                :workspace="workspace"
                @cancel="editing = false"
                @updated="loadWorkspace"
                @update:busy="busy = $event"
              />
              <template v-else>
                <p>
                  <strong>Name:</strong>&nbsp;
                  <span data-test-id="workspace-settings-name">{{ workspace.name }}</span>
                </p>
                <p>
                  <strong>Description:</strong>&nbsp;
                  <span data-test-id="workspace-description">
                    {{ workspace.description || 'No description' }}
                  </span>
                </p>
                <p>
                  <strong>Key:</strong>&nbsp;
                  <code data-test-id="workspace-key">{{ workspace.key }}</code>
                  <WorkspaceCopyKeyButton
                    v-if="workspace.key"
                    :workspace-key="workspace.key"
                    compact
                    class="ms-2"
                  />
                </p>
                <button
                  class="btn btn-outline-primary"
                  :disabled="busy"
                  data-test-id="edit-workspace-button"
                  @click="editing = true"
                >
                  Edit Workspace
                </button>
              </template>
            </div>
          </section>
        </div>

        <div class="col-md-6">
          <section
            class="card h-100"
            aria-labelledby="delete-workspace-heading"
          >
            <div class="card-body">
              <CardTitle
                id="delete-workspace-heading"
                class="mb-3"
                title="Delete Workspace"
                subtitle="Delete the workspace itself. This action cannot be undone."
              />
              <button
                class="btn btn-outline-danger"
                :disabled="busy"
                data-test-id="delete-workspace-button"
                @click="showDeleteModal = true"
              >
                Delete Workspace
              </button>
            </div>
          </section>
        </div>
      </div>

      <WorkspaceDeleteModal
        v-model:show="showDeleteModal"
        :workspace="workspace"
        @deleted="handleWorkspaceDeleted"
        @update:busy="busy = $event"
      />
    </template>
  </div>
</template>
