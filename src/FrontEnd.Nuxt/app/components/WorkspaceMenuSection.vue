<script setup lang="ts">
/**
 * Workspace menu section component
 *
 * Displays current workspace with quick-switch functionality for other workspaces.
 * Designed to be used within the LoginState dropdown menu.
 */

import { TenantClient, TenantRole, type TenantRoleResultDto } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'

const userPreferencesStore = useUserPreferencesStore()
const workspaces = ref<TenantRoleResultDto[]>([])
const loading = ref(false)

const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const tenantClient = new TenantClient(baseUrl, authFetch)

onMounted(async () => {
  userPreferencesStore.loadFromStorage()
  await loadWorkspaces()
})

async function loadWorkspaces() {
  loading.value = true
  try {
    workspaces.value = await tenantClient.getTenants()

    // Verify stored tenant still exists and refresh with latest data
    const storedKey = userPreferencesStore.getCurrentTenantKey
    if (storedKey) {
      const updatedTenant = workspaces.value.find((t) => t.key === storedKey)
      if (updatedTenant) {
        userPreferencesStore.setCurrentTenant(updatedTenant)
      }
    }
  } catch (error) {
    console.error('Failed to load workspaces:', error)
  } finally {
    loading.value = false
  }
}

const currentWorkspace = computed(() => userPreferencesStore.currentTenant)

const otherWorkspaces = computed(() => {
  const currentKey = currentWorkspace.value?.key
  return workspaces.value.filter((w) => w.key !== currentKey)
})

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

async function switchWorkspace(workspace: TenantRoleResultDto) {
  userPreferencesStore.setCurrentTenant(workspace)
  await navigateTo(window.location.pathname)
}
</script>

<template>
  <li class="px-3 py-2">
    <div class="mb-2">
      <small class="text-muted text-uppercase fw-bold">Workspace</small>
    </div>

    <!-- Current Workspace -->
    <div
      v-if="currentWorkspace"
      class="workspace-current mb-2 p-2 bg-light rounded"
    >
      <div class="d-flex align-items-center justify-content-between">
        <div>
          <div class="fw-semibold">{{ currentWorkspace.name }}</div>
          <small class="text-muted">{{ getRoleName(currentWorkspace.role) }}</small>
        </div>
        <FeatherIcon
          icon="check"
          size="16"
          class="text-success"
        />
      </div>
    </div>

    <!-- Quick Switch List (max 4) -->
    <div v-if="otherWorkspaces.length > 0">
      <a
        v-for="workspace in otherWorkspaces.slice(0, 4)"
        :key="workspace.key"
        class="dropdown-item dropdown-item-sm py-1"
        @click="switchWorkspace(workspace)"
      >
        {{ workspace.name }}
      </a>
    </div>

    <!-- Loading State -->
    <div
      v-if="loading"
      class="text-center py-2"
    >
      <BaseSpinner size="sm" />
    </div>
  </li>
  <!-- Link to Full Page -->
  <li><hr class="dropdown-divider" /></li>
  <li class="px-3 py-2">
    <div class="mb-2">
      <small class="text-muted text-uppercase fw-bold">Account</small>
    </div>
  </li>

  <!-- User Actions -->
  <li>
    <NuxtLink
      class="dropdown-item"
      to="/workspaces"
      data-test-id="all-workspaces-link"
      >Workspaces</NuxtLink
    >
  </li>
</template>

<style scoped>
.workspace-current {
  border-left: 3px solid var(--bs-success);
}

.dropdown-item-sm {
  font-size: 0.875rem;
  padding: 0.25rem 0.5rem;
}
</style>
