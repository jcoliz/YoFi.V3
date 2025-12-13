<script setup lang="ts">
/**
 * Workspace (Tenant) Selector Component
 *
 * Displays the current workspace and allows users to switch between
 * their available workspaces. Uses the TenantClient API to fetch
 * and manage workspace data.
 */

import { TenantClient, TenantRole, type TenantRoleResultDto } from '~/utils/apiclient'
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
const error = ref<string>('')

// Computed
const currentTenant = computed(() => {
  // First check if we have it in the store
  if (userPreferencesStore.currentTenant) {
    return userPreferencesStore.currentTenant
  }
  // Otherwise find it in the loaded tenants list
  const key = userPreferencesStore.getCurrentTenantKey
  return tenants.value.find((t) => t.key === key)
})

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
  error.value = ''

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

function onWorkspaceChange(event: Event) {
  const select = event.target as HTMLSelectElement
  const selectedKey = select.value

  if (selectedKey) {
    const tenant = tenants.value.find((t) => t.key === selectedKey)
    if (tenant) {
      selectTenant(tenant)
    }
  }
}

// Expose methods for parent component
defineExpose({
  loadTenants,
  currentTenant,
})
</script>

<template>
  <div class="workspace-selector d-flex align-items-center justify-content-between w-100">
    <div class="workspace-info d-flex align-items-center">
      <h5 class="workspace-label mb-0 me-2">Workspace:</h5>
      <h5
        v-if="currentTenant"
        class="workspace-current mb-0"
        data-test-id="current-workspace"
      >
        {{ currentTenant.name }}
      </h5>
      <h5
        v-else-if="loading"
        class="workspace-current text-muted mb-0"
      >
        Loading...
      </h5>
      <h5
        v-else
        class="workspace-current text-muted mb-0"
      >
        not selected
      </h5>
    </div>

    <DropDownPortable data-test-id="workspace-selector-dropdown">
      <template #trigger>
        <button
          class="btn btn-sm btn-outline-secondary workspace-menu-trigger"
          data-bs-toggle="dropdown"
          aria-expanded="false"
          title="Workspace options"
        >
          <FeatherIcon
            icon="more-vertical"
            size="16"
          />
        </button>
      </template>
      <template #default>
        <div class="dropdown-menu dropdown-menu-end shadow workspace-panel">
          <!-- Error State -->
          <div
            v-if="error"
            class="p-3 text-danger"
          >
            <small>{{ error }}</small>
          </div>

          <!-- Loading State -->
          <div
            v-else-if="loading"
            class="p-3 text-center"
          >
            <BaseSpinner size="sm" />
            <div class="mt-2">
              <small
                class="text-muted"
                data-test-id="loading-workspaces-text"
                >Loading workspaces...</small
              >
            </div>
          </div>

          <!-- Main Content -->
          <div
            v-else
            class="p-3"
          >
            <!-- Current Workspace Details -->
            <div
              v-if="currentTenant"
              class="mb-3"
            >
              <label class="form-label mb-2">
                <strong>Current Workspace</strong>
              </label>
              <div class="workspace-details">
                <div class="mb-2">
                  <small class="text-muted d-block">Name:</small>
                  <div>{{ currentTenant.name }}</div>
                </div>
                <div
                  v-if="currentTenant.description"
                  class="mb-2"
                >
                  <small class="text-muted d-block">Description:</small>
                  <div>{{ currentTenant.description }}</div>
                </div>
                <div
                  v-if="currentTenant.role"
                  class="mb-2"
                >
                  <small class="text-muted d-block">Role:</small>
                  <div>
                    <span class="badge bg-secondary">{{ getRoleName(currentTenant.role) }}</span>
                  </div>
                </div>
                <div v-if="currentTenant.createdAt">
                  <small class="text-muted d-block">Created:</small>
                  <div>{{ new Date(currentTenant.createdAt).toLocaleDateString() }}</div>
                </div>
              </div>
            </div>

            <div
              v-else
              class="mb-3 text-muted text-center py-2"
              data-test-id="no-workspace-message"
            >
              <small>No workspace selected</small>
            </div>

            <hr class="my-3" />

            <!-- Change Workspace Dropdown -->
            <div class="mb-3">
              <label
                for="workspace-select"
                class="form-label mb-2"
              >
                <strong>Change Workspace</strong>
              </label>
              <select
                id="workspace-select"
                class="form-select form-select-sm"
                :value="userPreferencesStore.getCurrentTenantKey || ''"
                :disabled="tenants.length === 0"
                @change="onWorkspaceChange"
              >
                <option
                  value=""
                  disabled
                >
                  {{ tenants.length === 0 ? 'No workspaces available' : 'Select a workspace' }}
                </option>
                <option
                  v-for="tenant in tenants"
                  :key="tenant.key"
                  :value="tenant.key"
                >
                  {{ tenant.name }}
                </option>
              </select>
            </div>

            <hr class="my-3" />

            <!-- Manage Workspaces Link -->
            <div class="text-center">
              <NuxtLink
                to="/workspaces"
                class="btn btn-outline-primary btn-sm w-100"
                data-test-id="manage-workspaces-button"
              >
                <FeatherIcon
                  icon="settings"
                  size="14"
                  class="me-1"
                />
                Manage Workspaces
              </NuxtLink>
            </div>
          </div>
        </div>
      </template>
    </DropDownPortable>
  </div>
</template>

<style scoped>
.workspace-selector {
  font-size: 0.875rem;
}

.workspace-label {
  font-weight: 600;
}

.workspace-current {
  font-weight: 400;
}

.workspace-menu-trigger {
  padding: 0.25rem 0.5rem;
  line-height: 1;
}

.workspace-panel {
  min-width: 320px;
  max-width: 400px;
}

.alert-sm {
  font-size: 0.875rem;
}
</style>
