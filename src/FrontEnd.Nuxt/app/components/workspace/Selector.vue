<script setup lang="ts">
/**
 * Workspace (Tenant) Selector Component
 *
 * Displays the current workspace and allows users to switch between
 * their available workspaces. The options panel mirrors the workspace
 * section of the account menu (WorkspaceMenuSection), listing every
 * workspace and adding a Manage Workspaces link.
 */

import { TenantClient, type TenantRoleResultDto } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'

/**
 * User preferences store for managing current tenant selection.
 */
const userPreferencesStore = useUserPreferencesStore()

/**
 * Component events emitted by WorkspaceSelector.
 */
const emit = defineEmits<{
  /** Emitted when user changes the current workspace */
  change: [tenant: TenantRoleResultDto]

  /** Emitted when a new workspace is created */
  created: [tenant: TenantRoleResultDto]
}>()

/**
 * List of available tenants/workspaces for the current user.
 */
const tenants = ref<TenantRoleResultDto[]>([])

/**
 * Whether the component is currently loading tenant data.
 */
const loading = ref(false)

/**
 * Error message from failed operations.
 */
const error = ref<string>('')

/**
 * Computes the currently selected tenant/workspace.
 * First checks the store, then searches the loaded tenants list.
 *
 * @returns The current tenant or undefined if none is selected
 */
const currentTenant = computed(() => {
  // First check if we have it in the store
  if (userPreferencesStore.currentTenant) {
    return userPreferencesStore.currentTenant
  }
  // Otherwise find it in the loaded tenants list
  const key = userPreferencesStore.getCurrentTenantKey
  return tenants.value.find((t) => t.key === key)
})

/**
 * Workspaces available to switch to, excluding the current one.
 */
const otherTenants = computed(() => {
  const currentKey = currentTenant.value?.key
  return tenants.value.filter((t) => t.key !== currentKey)
})

/**
 * API client for tenant operations.
 */
const tenantClient = useApiClient(TenantClient)

/**
 * Load tenants on component mount.
 * First loads preferences from localStorage, then fetches available tenants.
 */
onMounted(async () => {
  // Load preferences from localStorage first
  userPreferencesStore.loadFromStorage()

  // Then load available tenants
  await loadTenants()
})

/**
 * Loads the list of available tenants from the API.
 * Verifies stored tenant still exists and selects first tenant if none selected.
 */
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
  } catch (err: unknown) {
    console.error('Failed to load tenants:', err)
    error.value = (err instanceof Error && err.message) || 'Failed to load workspaces'
  } finally {
    loading.value = false
  }
}

/**
 * Selects a tenant as the current workspace.
 * Updates the store and emits a change event.
 *
 * @param tenant - The tenant to select as current
 */
function selectTenant(tenant: TenantRoleResultDto) {
  if (tenant.key === userPreferencesStore.getCurrentTenantKey) return

  // Update the store (which will also persist to localStorage)
  userPreferencesStore.setCurrentTenant(tenant)

  // Emit change event for any listeners
  emit('change', tenant)
}

/**
 * Expose methods and computed properties for parent component access.
 */
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
          <div class="px-3 py-2">
            <div class="mb-2">
              <small class="text-muted text-uppercase fw-bold">Workspace</small>
            </div>

            <!-- Error State -->
            <div
              v-if="error"
              class="text-danger"
            >
              <small>{{ error }}</small>
            </div>

            <!-- Loading State -->
            <div
              v-else-if="loading"
              class="text-center py-2"
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

            <template v-else>
              <!-- Current Workspace -->
              <div
                v-if="currentTenant"
                class="workspace-active mb-2 p-2 bg-light rounded"
                data-test-id="selector-current-workspace"
              >
                <div class="d-flex align-items-center justify-content-between">
                  <div class="fw-semibold">{{ currentTenant.name }}</div>
                  <FeatherIcon
                    icon="check"
                    size="16"
                    class="text-success"
                  />
                </div>
              </div>
              <div
                v-else
                class="mb-2 text-muted"
                data-test-id="no-workspace-message"
              >
                <small>No workspace selected</small>
              </div>
              <!-- Switch List -->
              <div class="workspace-list">
                <button
                  v-for="tenant in otherTenants"
                  :key="tenant.key"
                  type="button"
                  class="dropdown-item dropdown-item-sm py-1"
                  data-test-id="workspace-option"
                  @click="selectTenant(tenant)"
                >
                  {{ tenant.name }}
                </button>
              </div>
              <div
                v-if="tenants.length === 0"
                class="text-muted"
              >
                <small>No workspaces available</small>
              </div>
            </template>
          </div>

          <hr class="dropdown-divider" />

          <!-- Manage Workspaces Link -->
          <div class="px-3 py-2">
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
              All Workspaces
            </NuxtLink>
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
  min-width: 260px;
  max-width: 400px;
}

.workspace-active {
  border-left: 3px solid var(--bs-success);
}

.workspace-list {
  max-height: 50vh;
  overflow-y: auto;
}

.dropdown-item-sm {
  font-size: 0.875rem;
  padding: 0.25rem 0.5rem;
}
</style>
