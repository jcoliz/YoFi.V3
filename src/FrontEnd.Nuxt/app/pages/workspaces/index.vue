<script setup lang="ts">
/**
 * Workspaces Page
 *
 * Lists accessible workspaces and supports workspace creation. Opening a card
 * selects its workspace; Manage opens the route-targeted settings page.
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

/**
 * This page requires an authenticated user. If not authenticated, redirect to login page.
 * Only after we've confirmed authentication, do we call loadTenants() to load the data for this page.
 */
useAuthenticationGuard().requireAuthenticatedUser(afterMounted)

// Load tenants on mount
async function afterMounted() {
  userPreferencesStore.loadFromStorage()
  await loadTenants()
}

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
        <h1>Workspaces</h1>
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
            class="card h-100 workspace-card workspace-card-navigable"
            :class="{
              'border-primary': tenant.key === userPreferencesStore.getCurrentTenantKey,
            }"
            :data-test-id="`workspace-card-${tenant.key}`"
          >
            <div class="card-body">
              <div>
                <div class="d-flex justify-content-between align-items-start mb-3">
                  <CardTitle
                    class="mb-0"
                    data-test-id="workspace-name"
                    :title="tenant.name ?? 'No name'"
                    :subtitle="tenant.description ?? 'No description'"
                  />
                  <span
                    v-if="tenant.key === userPreferencesStore.getCurrentTenantKey"
                    class="badge bg-success ms-2"
                    data-test-id="current-workspace-badge"
                  >
                    Current
                  </span>
                </div>

                <div class="mb-3">
                  <small class="text-muted">
                    <strong>Key:</strong> <code data-test-id="workspace-key">{{ tenant.key }}</code>
                  </small>
                  <WorkspaceCopyKeyButton
                    v-if="tenant.key"
                    :workspace-key="tenant.key"
                    compact
                    class="ms-2 position-relative z-2"
                  />
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

                <div class="mb-3">
                  <small class="text-muted">
                    <strong>Role:</strong>
                    <span data-test-id="workspace-role-badge">
                      {{ getRoleName(tenant.role) }}
                    </span>
                  </small>
                </div>

                <!-- Action Buttons -->
                <div class="d-flex gap-2 align-items-center flex-wrap">
                  <NuxtLink
                    to="/transactions"
                    class="btn btn-sm btn-outline-primary stretched-link workspace-open-link me-auto"
                    :aria-label="`Open workspace ${tenant.name}`"
                    data-test-id="open-workspace-link"
                    @click="userPreferencesStore.setCurrentTenant(tenant)"
                  >
                    <FeatherIcon
                      icon="folder"
                      size="14"
                      class="me-1"
                      aria-hidden="true"
                    />
                    Open
                  </NuxtLink>
                  <NuxtLink
                    :to="`/workspaces/${tenant.key}`"
                    class="btn btn-sm btn-outline-primary position-relative z-2"
                    :aria-label="`Manage workspace ${tenant.name}`"
                    data-test-id="manage-workspace-button"
                  >
                    <FeatherIcon
                      icon="settings"
                      size="14"
                      class="me-1"
                    />
                    Manage
                  </NuxtLink>
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
  </div>
</template>

<style scoped>
.workspace-card-navigable {
  cursor: pointer;
}

.workspace-card-navigable:hover,
.workspace-card-navigable:focus-within {
  background-color: var(--bs-tertiary-bg);
}

.workspace-card:has(.workspace-open-link:focus-visible) {
  outline: 2px solid var(--bs-primary);
  outline-offset: 2px;
}

.workspace-selector-container {
  background-color: #f8f9fa;
  border-bottom: 1px solid #dee2e6;
  padding: 0.75rem 1rem;
}
</style>
