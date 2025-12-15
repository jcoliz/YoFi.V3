<script setup lang="ts">
/**
 * Workspaces Management Page
 *
 * Provides complete CRUD (Create, Read, Update, Delete) support for managing
 * workspace (tenant) entities. Shows workspace selector for consistency while
 * providing expanded management functionality.
 */

import {
  TenantClient,
  TenantEditDto,
  TenantRole,
  type TenantRoleResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

// Store
const userPreferencesStore = useUserPreferencesStore()

// State
const tenants = ref<TenantRoleResultDto[]>([])
const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)

// Create state
const showCreateForm = ref(false)
const creating = ref(false)
const createError = ref<IProblemDetails | undefined>(undefined)
const showCreateError = ref(false)
const newWorkspaceName = ref('')
const newWorkspaceDescription = ref('')

// Edit state
const editingTenant = ref<TenantRoleResultDto | null>(null)
const updating = ref(false)
const updateError = ref<IProblemDetails | undefined>(undefined)
const showUpdateError = ref(false)
const editName = ref('')
const editDescription = ref('')

// Delete state
const deletingTenant = ref<TenantRoleResultDto | null>(null)
const showDeleteModal = ref(false)
const deleting = ref(false)
const deleteError = ref<IProblemDetails | undefined>(undefined)
const showDeleteError = ref(false)

// API Client
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const tenantClient = new TenantClient(baseUrl, authFetch)

// Helper function to get role name
function getRoleName(role: TenantRole | undefined): string {
  if (!role) return 'Unknown'
  switch (role) {
    case TenantRole.Owner:
      return 'Owner'
    case TenantRole.Editor:
      return 'Editor'
    case TenantRole.Viewer:
      return 'Viewer'
    default:
      return 'Unknown'
  }
}

// Helper function to get role badge class
function getRoleBadgeClass(role: TenantRole | undefined): string {
  if (!role) return 'bg-secondary'
  switch (role) {
    case TenantRole.Owner:
      return 'bg-primary'
    case TenantRole.Editor:
      return 'bg-success'
    case TenantRole.Viewer:
      return 'bg-info'
    default:
      return 'bg-secondary'
  }
}

// Check if user can edit/delete a tenant
function canManageTenant(tenant: TenantRoleResultDto): boolean {
  return tenant.role === TenantRole.Owner
}

// Load tenants on mount
onMounted(async () => {
  userPreferencesStore.loadFromStorage()
  await loadTenants()
})

// Methods
async function loadTenants() {
  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    tenants.value = await tenantClient.getTenants()

    // Update current tenant if it exists
    const storedKey = userPreferencesStore.getCurrentTenantKey
    if (storedKey) {
      const updatedTenant = tenants.value.find((t) => t.key === storedKey)
      if (updatedTenant) {
        userPreferencesStore.setCurrentTenant(updatedTenant)
      } else {
        userPreferencesStore.clearPreferences()
      }
    }

    // If no current tenant is set but we have tenants, select the first one
    if (!userPreferencesStore.hasTenant && tenants.value.length > 0 && tenants.value[0]) {
      userPreferencesStore.setCurrentTenant(tenants.value[0])
    }
  } catch (err) {
    error.value = handleApiError(err, 'Load Failed', 'Failed to load workspaces')
    showError.value = true
  } finally {
    loading.value = false
  }
}

function handleWorkspaceChange() {
  // Just reload the list to show updated current workspace
  loadTenants()
}

// Create operations
function openCreateForm() {
  showCreateForm.value = true
  createError.value = undefined
  showCreateError.value = false
  newWorkspaceName.value = ''
  newWorkspaceDescription.value = ''
}

function cancelCreate() {
  showCreateForm.value = false
  newWorkspaceName.value = ''
  newWorkspaceDescription.value = ''
  createError.value = undefined
  showCreateError.value = false
}

async function createWorkspace() {
  if (!newWorkspaceName.value.trim()) {
    createError.value = {
      title: 'Validation Error',
      detail: 'Workspace name is required',
    }
    showCreateError.value = true
    return
  }

  creating.value = true
  createError.value = undefined
  showCreateError.value = false

  try {
    const newTenant = new TenantEditDto({
      name: newWorkspaceName.value.trim(),
      description: newWorkspaceDescription.value.trim() || undefined,
    })

    await tenantClient.createTenant(newTenant)
    await loadTenants()

    cancelCreate()
  } catch (err) {
    createError.value = handleApiError(err, 'Create Failed', 'Failed to create workspace')
    showCreateError.value = true
  } finally {
    creating.value = false
  }
}

// Edit operations
function startEdit(tenant: TenantRoleResultDto) {
  editingTenant.value = tenant
  editName.value = tenant.name || ''
  editDescription.value = tenant.description || ''
  updateError.value = undefined
  showUpdateError.value = false
}

function cancelEdit() {
  editingTenant.value = null
  editName.value = ''
  editDescription.value = ''
  updateError.value = undefined
  showUpdateError.value = false
}

async function updateWorkspace() {
  if (!editingTenant.value || !editName.value.trim()) {
    updateError.value = {
      title: 'Validation Error',
      detail: 'Workspace name is required',
    }
    showUpdateError.value = true
    return
  }

  updating.value = true
  updateError.value = undefined
  showUpdateError.value = false

  try {
    const updateDto = new TenantEditDto({
      name: editName.value.trim(),
      description: editDescription.value.trim() || undefined,
    })

    await tenantClient.updateTenant(editingTenant.value.key!, updateDto)
    await loadTenants()

    cancelEdit()
  } catch (err) {
    updateError.value = handleApiError(err, 'Update Failed', 'Failed to update workspace')
    showUpdateError.value = true
  } finally {
    updating.value = false
  }
}

// Delete operations
function startDelete(tenant: TenantRoleResultDto) {
  deletingTenant.value = tenant
  deleteError.value = undefined
  showDeleteError.value = false
  showDeleteModal.value = true
}

async function deleteWorkspace() {
  if (!deletingTenant.value) return

  deleting.value = true
  deleteError.value = undefined
  showDeleteError.value = false

  try {
    await tenantClient.deleteTenant(deletingTenant.value.key!)

    // If we deleted the current workspace, clear it
    if (deletingTenant.value.key === userPreferencesStore.getCurrentTenantKey) {
      userPreferencesStore.clearPreferences()
    }

    await loadTenants()
    showDeleteModal.value = false
    deletingTenant.value = null
  } catch (err) {
    deleteError.value = handleApiError(err, 'Delete Failed', 'Failed to delete workspace')
    showDeleteError.value = true
  } finally {
    deleting.value = false
  }
}

definePageMeta({
  title: 'Workspaces',
  layout: 'chrome',
})
</script>

<template>
  <div>
    <!-- Workspace Selector - Full Width -->
    <div class="workspace-selector-container">
      <WorkspaceSelector @change="handleWorkspaceChange" />
    </div>

    <!-- Main Content -->
    <div class="container py-4">
      <div
        class="d-flex justify-content-between align-items-center mb-4"
        data-test-id="page-heading"
      >
        <h1>Workspace Management</h1>
        <button
          class="btn btn-primary"
          :disabled="showCreateForm"
          data-test-id="create-workspace-button"
          @click="openCreateForm"
        >
          <FeatherIcon
            icon="plus"
            size="16"
            class="me-1"
          />
          Create Workspace
        </button>
      </div>

      <!-- Error State -->
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-4"
      />

      <!-- Create Form -->
      <div
        v-if="showCreateForm"
        class="card mb-4"
        data-test-id="create-form-card"
      >
        <div class="card-header bg-primary text-white">
          <h5 class="mb-0">Create New Workspace</h5>
        </div>
        <div class="card-body">
          <ErrorDisplay
            v-model:show="showCreateError"
            :problem="createError"
          />

          <div class="mb-3">
            <label
              for="create-name"
              class="form-label"
              >Name <span class="text-danger">*</span></label
            >
            <input
              id="create-name"
              v-model="newWorkspaceName"
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
              v-model="newWorkspaceDescription"
              class="form-control"
              rows="3"
              placeholder="Enter workspace description (optional)"
              :disabled="creating"
            />
          </div>

          <div class="d-flex gap-2">
            <button
              class="btn btn-primary"
              :disabled="creating || !newWorkspaceName.trim()"
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
              @click="cancelCreate"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>

      <!-- Loading State -->
      <div
        v-if="loading"
        class="text-center py-5"
        data-test-id="loading-state"
      >
        <BaseSpinner size="lg" />
        <div
          class="mt-3 text-muted"
          data-test-id="loading-workspaces-text"
        >
          Loading workspaces...
        </div>
      </div>

      <!-- Workspaces List -->
      <div
        v-else-if="tenants.length > 0"
        class="row"
        data-test-id="workspaces-list"
      >
        <div
          v-for="tenant in tenants"
          :key="tenant.key"
          class="col-md-6 col-lg-4 mb-4"
        >
          <div
            class="card h-100"
            :class="{ 'border-primary': tenant.key === userPreferencesStore.getCurrentTenantKey }"
            :data-test-id="`workspace-card-${tenant.key}`"
          >
            <div class="card-body">
              <!-- Edit Form -->
              <div v-if="editingTenant?.key === tenant.key">
                <ErrorDisplay
                  v-model:show="showUpdateError"
                  :problem="updateError"
                  class="mb-3"
                />

                <div class="mb-3">
                  <label class="form-label">Name <span class="text-danger">*</span></label>
                  <input
                    v-model="editName"
                    type="text"
                    class="form-control form-control-sm"
                    :disabled="updating"
                    data-test-id="edit-workspace-name"
                  />
                </div>

                <div class="mb-3">
                  <label class="form-label">Description</label>
                  <textarea
                    v-model="editDescription"
                    class="form-control form-control-sm"
                    rows="2"
                    :disabled="updating"
                    data-test-id="edit-workspace-description"
                  />
                </div>

                <div class="d-flex gap-2">
                  <button
                    class="btn btn-sm btn-primary"
                    :disabled="updating || !editName.trim()"
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
                    @click="cancelEdit"
                  >
                    Cancel
                  </button>
                </div>
              </div>

              <!-- View Mode -->
              <div v-else>
                <div class="d-flex justify-content-between align-items-start mb-3">
                  <h5
                    class="card-title mb-0"
                    data-test-id="workspace-name"
                  >
                    {{ tenant.name }}
                    <span
                      v-if="tenant.key === userPreferencesStore.getCurrentTenantKey"
                      class="badge bg-success ms-2"
                      data-test-id="current-workspace-badge"
                    >
                      Current
                    </span>
                  </h5>
                  <span
                    class="badge"
                    :class="getRoleBadgeClass(tenant.role)"
                    data-test-id="workspace-role-badge"
                  >
                    {{ getRoleName(tenant.role) }}
                  </span>
                </div>

                <p
                  v-if="tenant.description"
                  class="card-text text-muted"
                >
                  {{ tenant.description }}
                </p>
                <p
                  v-else
                  class="card-text text-muted fst-italic"
                >
                  No description
                </p>

                <div class="mb-3">
                  <small class="text-muted">
                    <strong>Key:</strong> <code data-test-id="workspace-key">{{ tenant.key }}</code>
                  </small>
                </div>

                <div
                  v-if="tenant.createdAt"
                  class="mb-3"
                >
                  <small class="text-muted">
                    <strong>Created:</strong>
                    <span data-test-id="created-date">
                      {{ new Date(tenant.createdAt).toLocaleDateString() }}
                    </span>
                  </small>
                </div>

                <!-- Action Buttons -->
                <div class="d-flex gap-2">
                  <button
                    v-if="canManageTenant(tenant)"
                    class="btn btn-sm btn-outline-primary"
                    data-test-id="edit-workspace-button"
                    @click="startEdit(tenant)"
                  >
                    <FeatherIcon
                      icon="edit"
                      size="14"
                      class="me-1"
                    />
                    Edit
                  </button>
                  <button
                    v-if="canManageTenant(tenant)"
                    class="btn btn-sm btn-outline-danger"
                    data-test-id="delete-workspace-button"
                    @click="startDelete(tenant)"
                  >
                    <FeatherIcon
                      icon="trash-2"
                      size="14"
                      class="me-1"
                    />
                    Delete
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div
        v-else
        class="text-center py-5"
        data-test-id="empty-state"
      >
        <FeatherIcon
          icon="folder"
          size="64"
          class="text-muted mb-3"
        />
        <h3 class="text-muted">No Workspaces</h3>
        <p class="text-muted">Create your first workspace to get started.</p>
        <button
          v-if="!showCreateForm"
          class="btn btn-primary"
          data-test-id="create-workspace-button"
          @click="openCreateForm"
        >
          <FeatherIcon
            icon="plus"
            size="16"
            class="me-1"
          />
          Create Workspace
        </button>
      </div>
    </div>

    <!-- Delete Confirmation Modal -->
    <ModalDialog
      v-model:show="showDeleteModal"
      title="Delete Workspace"
      :loading="deleting"
      primary-button-variant="danger"
      :primary-button-text="deleting ? 'Deleting...' : 'Delete'"
      primary-button-test-id="delete-submit-button"
      secondary-button-test-id="delete-cancel-button"
      data-test-id="delete-modal"
      @primary="deleteWorkspace"
    >
      <ErrorDisplay
        v-model:show="showDeleteError"
        :problem="deleteError"
      />

      <p>
        Are you sure you want to delete the workspace
        <strong data-test-id="deleting-workspace-name">{{ deletingTenant?.name }}</strong
        >?
      </p>
      <p class="text-danger">
        <strong>Warning:</strong> This action cannot be undone. All data associated with this
        workspace will be permanently deleted.
      </p>
    </ModalDialog>
  </div>
</template>

<style scoped>
.workspace-selector-container {
  background-color: #f8f9fa;
  border-bottom: 1px solid #dee2e6;
  padding: 0.75rem 1rem;
}

.alert-sm {
  font-size: 0.875rem;
  padding: 0.5rem 0.75rem;
}

.modal.show {
  display: block;
}
</style>
