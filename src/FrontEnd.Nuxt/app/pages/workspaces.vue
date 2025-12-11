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
  ApiException,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'

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
    if (ApiException.isApiException(err)) {
      console.error('Failed to load tenants:', err)
      error.value = err.result
      showError.value = true
    } else {
      console.error('Unexpected error loading tenants:', err)
      error.value = {
        title: 'Unexpected Error',
        detail: 'An unexpected error occurred while loading workspaces',
      }
      showError.value = true
    }
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
    if (ApiException.isApiException(err)) {
      console.error('Failed to create workspace:', err)
      createError.value = err.result
      showCreateError.value = true
    } else {
      console.error('Unexpected error creating workspace:', err)
      createError.value = {
        title: 'Unexpected Error',
        detail: 'An unexpected error occurred while creating the workspace',
      }
      showCreateError.value = true
    }
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
    if (ApiException.isApiException(err)) {
      console.error('Failed to update workspace:', err)
      updateError.value = err.result
      showUpdateError.value = true
    } else {
      console.error('Unexpected error updating workspace:', err)
      updateError.value = {
        title: 'Unexpected Error',
        detail: 'An unexpected error occurred while updating the workspace',
      }
      showUpdateError.value = true
    }
  } finally {
    updating.value = false
  }
}

// Delete operations
function startDelete(tenant: TenantRoleResultDto) {
  deletingTenant.value = tenant
  deleteError.value = undefined
  showDeleteError.value = false
}

function cancelDelete() {
  deletingTenant.value = null
  deleteError.value = undefined
  showDeleteError.value = false
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
    cancelDelete()
  } catch (err) {
    if (ApiException.isApiException(err)) {
      console.error('Failed to delete workspace:', err)
      deleteError.value = err.result
      showDeleteError.value = true
    } else {
      console.error('Unexpected error deleting workspace:', err)
      deleteError.value = {
        title: 'Unexpected Error',
        detail: 'An unexpected error occurred while deleting the workspace',
      }
      showDeleteError.value = true
    }
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
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h1>Workspace Management</h1>
        <button
          class="btn btn-primary"
          :disabled="showCreateForm"
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
      >
        <BaseSpinner size="lg" />
        <div class="mt-3 text-muted">Loading workspaces...</div>
      </div>

      <!-- Workspaces List -->
      <div
        v-else-if="tenants.length > 0"
        class="row"
      >
        <div
          v-for="tenant in tenants"
          :key="tenant.key"
          class="col-md-6 col-lg-4 mb-4"
        >
          <div
            class="card h-100"
            :class="{ 'border-primary': tenant.key === userPreferencesStore.getCurrentTenantKey }"
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
                  />
                </div>

                <div class="mb-3">
                  <label class="form-label">Description</label>
                  <textarea
                    v-model="editDescription"
                    class="form-control form-control-sm"
                    rows="2"
                    :disabled="updating"
                  />
                </div>

                <div class="d-flex gap-2">
                  <button
                    class="btn btn-sm btn-primary"
                    :disabled="updating || !editName.trim()"
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
                    @click="cancelEdit"
                  >
                    Cancel
                  </button>
                </div>
              </div>

              <!-- View Mode -->
              <div v-else>
                <div class="d-flex justify-content-between align-items-start mb-3">
                  <h5 class="card-title mb-0">
                    {{ tenant.name }}
                    <span
                      v-if="tenant.key === userPreferencesStore.getCurrentTenantKey"
                      class="badge bg-success ms-2"
                    >
                      Current
                    </span>
                  </h5>
                  <span
                    class="badge"
                    :class="getRoleBadgeClass(tenant.role)"
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
                    <strong>Key:</strong> <code>{{ tenant.key }}</code>
                  </small>
                </div>

                <div
                  v-if="tenant.createdAt"
                  class="mb-3"
                >
                  <small class="text-muted">
                    <strong>Created:</strong> {{ new Date(tenant.createdAt).toLocaleDateString() }}
                  </small>
                </div>

                <!-- Action Buttons -->
                <div class="d-flex gap-2">
                  <button
                    v-if="canManageTenant(tenant)"
                    class="btn btn-sm btn-outline-primary"
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
    <div
      v-if="deletingTenant"
      class="modal show d-block"
      tabindex="-1"
      style="background-color: rgba(0, 0, 0, 0.5)"
    >
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title">Delete Workspace</h5>
            <button
              type="button"
              class="btn-close"
              :disabled="deleting"
              @click="cancelDelete"
            />
          </div>
          <div class="modal-body">
            <ErrorDisplay
              v-model:show="showDeleteError"
              :problem="deleteError"
            />

            <p>
              Are you sure you want to delete the workspace
              <strong>{{ deletingTenant.name }}</strong
              >?
            </p>
            <p class="text-danger">
              <strong>Warning:</strong> This action cannot be undone. All data associated with this
              workspace will be permanently deleted.
            </p>
          </div>
          <div class="modal-footer">
            <button
              type="button"
              class="btn btn-secondary"
              :disabled="deleting"
              @click="cancelDelete"
            >
              Cancel
            </button>
            <button
              type="button"
              class="btn btn-danger"
              :disabled="deleting"
              @click="deleteWorkspace"
            >
              <BaseSpinner
                v-if="deleting"
                size="sm"
                class="me-1"
              />
              {{ deleting ? 'Deleting...' : 'Delete' }}
            </button>
          </div>
        </div>
      </div>
    </div>
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
