<script setup lang="ts">
/**
 * Workspace (Tenant) Selector Component
 *
 * Displays the current workspace and allows users to switch between
 * their available workspaces. Uses the TenantClient API to fetch
 * and manage workspace data.
 */

import { TenantClient, TenantEditDto, type TenantRoleResultDto } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'

// Store
const userPreferencesStore = useUserPreferencesStore()

// Emits
const emit = defineEmits<{
  change: [tenant: TenantRoleResultDto]
  created: [tenant: TenantRoleResultDto]
}>()

// State
const tenants = ref<TenantRoleResultDto[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const showCreateForm = ref(false)
const creating = ref(false)
const createError = ref<string | null>(null)
const newWorkspaceName = ref('')
const newWorkspaceDescription = ref('')

// Computed
const currentTenant = computed(() => {
  // First check if we have it in the store
  if (userPreferencesStore.currentTenant) {
    return userPreferencesStore.currentTenant
  }
  // Otherwise find it in the loaded tenants list
  const key = userPreferencesStore.getCurrentTenantKey
  return tenants.value.find((t) => t.key === key) || null
})

// API Client
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const tenantClient = new TenantClient(baseUrl, authFetch)

// Load tenants on mount
onMounted(async () => {
  // Load preferences from localStorage first
  userPreferencesStore.loadFromStorage()

  // Then load available tenants
  await loadTenants()
})

// Methods
async function loadTenants() {
  loading.value = true
  error.value = null

  try {
    tenants.value = await tenantClient.getTenants()

    // If we have a stored tenant, verify it still exists in the available tenants
    // and refresh it with the latest data from the API
    const storedKey = userPreferencesStore.getCurrentTenantKey
    if (storedKey) {
      const updatedTenant = tenants.value.find((t) => t.key === storedKey)
      if (updatedTenant) {
        // Update with fresh data from API
        userPreferencesStore.setCurrentTenant(updatedTenant)
      } else {
        // Stored tenant no longer available, clear it
        userPreferencesStore.clearPreferences()
      }
    }

    // If no current tenant is set but we have tenants, select the first one
    if (!userPreferencesStore.hasTenant && tenants.value.length > 0 && tenants.value[0]) {
      selectTenant(tenants.value[0])
    }
  } catch (err: any) {
    console.error('Failed to load tenants:', err)
    error.value = err.message || 'Failed to load workspaces'
  } finally {
    loading.value = false
  }
}

function selectTenant(tenant: TenantRoleResultDto) {
  if (tenant.key === userPreferencesStore.getCurrentTenantKey) return

  // Update the store (which will also persist to localStorage)
  userPreferencesStore.setCurrentTenant(tenant)

  // Emit change event for any listeners
  emit('change', tenant)
}

function toggleCreateForm() {
  showCreateForm.value = !showCreateForm.value
  if (!showCreateForm.value) {
    // Reset form when hiding
    newWorkspaceName.value = ''
    newWorkspaceDescription.value = ''
    createError.value = null
  }
}

async function createWorkspace() {
  if (!newWorkspaceName.value.trim()) {
    createError.value = 'Workspace name is required'
    return
  }

  creating.value = true
  createError.value = null

  try {
    const newTenant = new TenantEditDto({
      name: newWorkspaceName.value.trim(),
      description: newWorkspaceDescription.value.trim() || undefined,
    })

    const createdTenant = await tenantClient.createTenant(newTenant)

    // Reload tenants to get the full list including the new one
    await loadTenants()

    // Find the newly created tenant with role info
    const tenantWithRole = tenants.value.find((t) => t.key === createdTenant.key)

    if (tenantWithRole) {
      // Select the newly created workspace
      selectTenant(tenantWithRole)
      emit('created', tenantWithRole)
    }

    // Reset form and hide it
    newWorkspaceName.value = ''
    newWorkspaceDescription.value = ''
    showCreateForm.value = false
  } catch (err: any) {
    console.error('Failed to create workspace:', err)
    createError.value = err.message || 'Failed to create workspace'
  } finally {
    creating.value = false
  }
}

function cancelCreate() {
  showCreateForm.value = false
  newWorkspaceName.value = ''
  newWorkspaceDescription.value = ''
  createError.value = null
}

// Expose methods for parent component
defineExpose({
  loadTenants,
  currentTenant,
})
</script>

<template>
  <DropDownPortable
    class="workspace-selector d-flex align-items-center"
    data-test-id="workspace-selector"
  >
    <template #trigger>
      <a
        class="d-flex align-items-center link-body-emphasis text-decoration-none p-0 dropdown-toggle"
        data-bs-toggle="dropdown"
        aria-expanded="false"
      >
        <FeatherIcon
          icon="briefcase"
          size="20"
          class="me-2"
        />
        <span
          v-if="currentTenant"
          class="workspace-name"
          data-test-id="current-workspace"
          :title="currentTenant.name"
        >
          {{ currentTenant.name }}
        </span>
        <span
          v-else-if="loading"
          class="text-muted"
        >
          Loading...
        </span>
        <span
          v-else
          class="text-muted"
        >
          Select Workspace
        </span>
      </a>
    </template>
    <template #default>
      <ul class="dropdown-menu dropdown-menu-end text-small shadow">
        <li
          v-if="error"
          class="px-3 py-2 text-danger"
        >
          <small>{{ error }}</small>
        </li>
        <li
          v-else-if="loading"
          class="px-3 py-2 text-muted"
        >
          <BaseSpinner size="sm" />
          <small class="ms-2">Loading workspaces...</small>
        </li>
        <template v-else>
          <!-- Existing workspaces list -->
          <template v-if="tenants.length > 0">
            <li class="px-3 py-1">
              <small class="text-muted">Switch Workspace</small>
            </li>
            <li><hr class="dropdown-divider" /></li>
            <li
              v-for="tenant in tenants"
              :key="tenant.key"
            >
              <a
                class="dropdown-item d-flex align-items-start"
                :class="{ active: tenant.key === userPreferencesStore.getCurrentTenantKey }"
                :data-test-id="`workspace-${tenant.key}`"
                @click="selectTenant(tenant)"
              >
                <div class="flex-grow-1">
                  <div class="fw-semibold">{{ tenant.name }}</div>
                  <small
                    v-if="tenant.description"
                    class="text-muted d-block"
                  >
                    {{ tenant.description }}
                  </small>
                </div>
                <FeatherIcon
                  v-if="tenant.key === userPreferencesStore.getCurrentTenantKey"
                  icon="check"
                  size="16"
                  class="ms-2 text-success"
                />
              </a>
            </li>
            <li><hr class="dropdown-divider" /></li>
          </template>

          <!-- No workspaces message (only when not showing create form) -->
          <template v-else-if="!showCreateForm">
            <li class="px-3 py-2 text-muted text-center">
              <small>No workspaces yet</small>
            </li>
            <li><hr class="dropdown-divider" /></li>
          </template>

          <!-- Create New Workspace Section -->
          <li v-if="!showCreateForm">
            <a
              class="dropdown-item d-flex align-items-center text-primary"
              data-test-id="create-workspace-button"
              @click="toggleCreateForm"
            >
              <FeatherIcon
                icon="plus-circle"
                size="16"
                class="me-2"
              />
              <span>Create New Workspace</span>
            </a>
          </li>

          <!-- Create Workspace Form -->
          <li
            v-else
            class="px-3 py-3"
          >
            <div
              v-if="createError"
              class="alert alert-danger alert-sm mb-2 py-1"
            >
              <small>{{ createError }}</small>
            </div>
            <div class="mb-2">
              <label
                for="workspace-name"
                class="form-label form-label-sm mb-1"
              >
                <small>Workspace Name</small>
              </label>
              <input
                id="workspace-name"
                v-model="newWorkspaceName"
                type="text"
                class="form-control form-control-sm"
                placeholder="My Workspace"
                :disabled="creating"
                @keyup.enter="createWorkspace"
                @keyup.escape="cancelCreate"
              />
            </div>
            <div class="mb-2">
              <label
                for="workspace-description"
                class="form-label form-label-sm mb-1"
              >
                <small>Description (optional)</small>
              </label>
              <textarea
                id="workspace-description"
                v-model="newWorkspaceDescription"
                class="form-control form-control-sm"
                rows="2"
                placeholder="Brief description..."
                :disabled="creating"
              />
            </div>
            <div class="d-flex gap-2">
              <button
                class="btn btn-primary btn-sm flex-grow-1"
                :disabled="creating || !newWorkspaceName.trim()"
                data-test-id="create-workspace-submit"
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
                class="btn btn-secondary btn-sm"
                :disabled="creating"
                data-test-id="create-workspace-cancel"
                @click="cancelCreate"
              >
                Cancel
              </button>
            </div>
          </li>
        </template>
      </ul>
    </template>
  </DropDownPortable>
</template>

<style scoped>
.workspace-selector {
  cursor: pointer;
}

.workspace-name {
  font-weight: 500;
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dropdown-menu {
  min-width: 280px;
  max-width: 400px;
}

.dropdown-item {
  cursor: pointer;
  padding: 0.5rem 1rem;
}

.dropdown-item:hover {
  background-color: var(--bs-dropdown-link-hover-bg);
}

.dropdown-item.active {
  background-color: var(--bs-dropdown-link-active-bg);
  color: var(--bs-dropdown-link-active-color);
}

.dropdown-item .fw-semibold {
  font-size: 0.95rem;
}

.dropdown-item small {
  font-size: 0.8rem;
  line-height: 1.3;
}

.text-primary {
  color: var(--bs-primary) !important;
}

.form-label-sm {
  font-weight: 500;
  margin-bottom: 0.25rem;
}

.alert-sm {
  font-size: 0.875rem;
  padding: 0.25rem 0.5rem;
}
</style>
