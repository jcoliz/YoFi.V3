<script setup lang="ts">
import {
  TenantClient,
  type TenantDataDeletionResultDto,
  type TenantRoleResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Manage Workspace',
  auth: false,
  layout: 'chrome',
  breadcrumbs: [{ title: 'Workspaces', to: '/workspaces' }],
})

const route = useRoute()
const router = useRouter()
const tenantClient = useApiClient(TenantClient)
const userPreferencesStore = useUserPreferencesStore()
const { data, status } = useAuth()
const authenticated = ref(false)

const workspace = ref<TenantRoleResultDto>()
const loading = ref(true)
/** Pending-write flags reported by child panes; any pending write blocks navigation. */
const busyFlags = reactive({
  edit: false,
  deleteWorkspace: false,
  deleteData: false,
  sample: false,
})
const busy = computed(() => Object.values(busyFlags).some(Boolean))
const editing = ref(false)
const showDeleteModal = ref(false)
const showDeleteDataModal = ref(false)
const deletionResult = ref<TenantDataDeletionResultDto>()
/** Incremented when workspace data changes, so dependent views refresh. */
const dataVersion = ref(0)
const error = ref<IProblemDetails>()
const showError = ref(false)
let loadVersion = 0

/**
 * Whether the current user has Manage rights on the route-targeted workspace.
 *
 * Three states:
 * - `undefined` — permission check has not run yet; show neither the banner nor the controls.
 * - `true`      — user has Owner rights; show the controls normally.
 * - `false`     — user lacks Owner rights; show the access-denied banner and hide controls.
 */
const canManage = ref<boolean | undefined>(undefined)

/**
 * This page requires an authenticated user. If not authenticated, redirect to login page.
 * Only after authentication resolves do we check the JWT-derived tenant role claims.
 */
useAuthenticationGuard().requireAuthenticatedUser(afterMounted)

async function afterMounted() {
  userPreferencesStore.loadFromStorage()
  authenticated.value = true
}

watch([authenticated, status, () => route.params.key, () => data.value?.claims], loadWorkspace, {
  deep: true,
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
  showDeleteDataModal.value = false
  deletionResult.value = undefined
  error.value = undefined
  showError.value = false
  loading.value = true
  canManage.value = undefined

  if (!authenticated.value || status.value !== 'authenticated') return

  if (typeof key !== 'string' || !key) {
    error.value = {
      title: 'Workspace unavailable',
      detail: 'A workspace key is required.',
    }
    showError.value = true
    loading.value = false
    return
  }

  canManage.value =
    data.value?.claims?.some(
      (claim) => claim.type === 'tenant_role' && claim.value === `${key}:Owner`,
    ) ?? false

  if (!canManage.value) {
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
  busyFlags.deleteWorkspace = false
  await router.push('/workspaces')
}

function handleDataDeleted(result?: TenantDataDeletionResultDto) {
  deletionResult.value = result
  dataVersion.value++
}

function handleSampleLoaded() {
  deletionResult.value = undefined
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

    <!-- Access denied banner — shown only when permission is definitively false AND no problem details are set -->
    <BaseAlert
      v-else-if="canManage === false && !error"
      variant="warning"
      data-test-id="access-denied"
    >
      Access denied
      <template #detail>
        You do not have Manage rights on this workspace. Contact the workspace owner to request
        access.
      </template>
    </BaseAlert>

    <template v-else-if="canManage && workspace">
      <div class="row g-4">
        <div class="col-md-6">
          <section
            class="card h-100"
            aria-labelledby="workspace-settings-heading"
            data-test-id="workspace-settings-card"
          >
            <div class="card-body">
              <CardTitle
                id="workspace-settings-heading"
                data-test-id="workspace-settings-title"
                class="mb-3"
                title="Workspace Settings"
                subtitle="View and edit your workspace's properties."
              />
              <WorkspaceEditPane
                v-if="editing"
                :workspace="workspace"
                @cancel="editing = false"
                @updated="loadWorkspace"
                @update:busy="busyFlags.edit = $event"
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
            class="card h-100 border-danger"
            aria-labelledby="delete-workspace-heading"
            data-test-id="delete-workspace-card"
          >
            <div class="card-body">
              <CardTitle
                id="delete-workspace-heading"
                data-test-id="delete-workspace-title"
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
              <hr class="my-4" />
              <CardTitle
                id="delete-data-heading"
                data-test-id="delete-data-title"
                class="mb-3"
                title="Delete All Workspace Data"
                subtitle="Delete all transactions, payee matching rules, and pending bank imports. The workspace and its members remain. This action cannot be undone."
              />
              <button
                class="btn btn-outline-danger"
                :disabled="busy"
                data-test-id="delete-data-button"
                @click="showDeleteDataModal = true"
              >
                Delete All Workspace Data
              </button>
              <div
                v-if="deletionResult?.deleted"
                class="alert alert-success mt-3 mb-0"
                role="status"
                data-test-id="delete-data-result"
              >
                Deleted
                <strong data-test-id="deleted-transactions">{{
                  deletionResult.deleted.transactions
                }}</strong>
                transactions,
                <strong data-test-id="deleted-payee-rules">{{
                  deletionResult.deleted.payeeMatchingRules
                }}</strong>
                payee matching rules, and
                <strong data-test-id="deleted-import-reviews">{{
                  deletionResult.deleted.importReviewTransactions
                }}</strong>
                pending bank imports.
              </div>
            </div>
          </section>
        </div>
      </div>

      <h2
        class="h4 mt-5 mb-3"
        data-test-id="data-administration-heading"
      >
        Data Administration
      </h2>
      <div class="row g-4">
        <div class="col-lg-8">
          <section
            class="card h-100"
            aria-labelledby="sample-data-heading"
            data-test-id="sample-data-card"
          >
            <div class="card-body">
              <CardTitle
                id="sample-data-heading"
                data-test-id="sample-data-title"
                class="mb-3"
                title="Sample Data"
                subtitle="Add realistic sample transactions and payee matching rules to explore the app."
              />
              <WorkspaceSampleDataPane
                :key="workspace.key"
                :workspace="workspace"
                :data-version="dataVersion"
                @loaded="handleSampleLoaded"
                @update:busy="busyFlags.sample = $event"
              />
            </div>
          </section>
        </div>

        <div class="col-lg-4">
          <section
            class="card h-100"
            aria-labelledby="import-data-heading"
            data-test-id="import-data-card"
          >
            <div class="card-body">
              <CardTitle
                id="import-data-heading"
                data-test-id="import-data-title"
                class="mb-3"
                title="Import Data"
                subtitle="Import data into this workspace."
              />
              <p class="text-muted mb-0">Coming soon.</p>
            </div>
          </section>
        </div>
      </div>

      <WorkspaceDeleteModal
        v-model:show="showDeleteModal"
        :workspace="workspace"
        @deleted="handleWorkspaceDeleted"
        @update:busy="busyFlags.deleteWorkspace = $event"
      />
      <WorkspaceDeleteModal
        v-model:show="showDeleteDataModal"
        :workspace="workspace"
        scope="data"
        @deleted="handleDataDeleted"
        @update:busy="busyFlags.deleteData = $event"
      />
    </template>
  </div>
</template>
