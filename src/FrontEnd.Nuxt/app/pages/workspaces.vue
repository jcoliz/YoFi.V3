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

// Edit state
const editingTenant = ref<TenantRoleResultDto | undefined>(undefined)

// Delete state
const deletingTenant = ref<TenantRoleResultDto | undefined>(undefined)
const showDeleteModal = ref(false)

// API Client
const tenantClient = useApiClient(TenantClient)

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

async function handleWorkspaceUpdated() {
  editingTenant.value = undefined
  await loadTenants()
}

// Delete operations
function startDelete(tenant: TenantRoleResultDto) {
  deletingTenant.value = tenant
  showDeleteModal.value = true
}

async function handleWorkspaceDeleted() {
  deletingTenant.value = undefined
  await loadTenants()
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
          @click="showCreateForm = true"
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

      <WorkspaceCreatePane
        v-model:show="showCreateForm"
        @created="loadTenants"
      />

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
              <WorkspaceEditPane
                v-if="editingTenant?.key === tenant.key"
                :workspace="tenant"
                @cancel="editingTenant = undefined"
                @updated="handleWorkspaceUpdated"
              />

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
                    @click="editingTenant = tenant"
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
          @click="showCreateForm = true"
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

    <WorkspaceDeleteModal
      v-model:show="showDeleteModal"
      :workspace="deletingTenant"
      @deleted="handleWorkspaceDeleted"
    />
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
