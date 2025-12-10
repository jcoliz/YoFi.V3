<script setup lang="ts">
/**
 * Workspace Selector Demo Page
 *
 * Demonstrates the WorkspaceSelector component with Pinia store integration
 */

import { useUserPreferencesStore } from '~/stores/userPreferences'
import { TenantRole, type TenantRoleResultDto } from '~/utils/apiclient'

const userPreferencesStore = useUserPreferencesStore()

// Demo state
const changeLog = ref<string[]>([])

function handleWorkspaceChange(tenant: TenantRoleResultDto) {
  const timestamp = new Date().toLocaleTimeString()
  changeLog.value.unshift(`[${timestamp}] Changed to: ${tenant.name} (${tenant.key})`)
}

function clearLog() {
  changeLog.value = []
}

function getRoleName(role: TenantRole | undefined): string {
  if (!role) return 'N/A'
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

definePageMeta({
  title: 'Workspace Demo',
  order: 99, // Show at end of nav
})
</script>

<template>
  <div>
    <!-- Workspace Selector - Full Width -->
    <div class="workspace-selector-container">
      <WorkspaceSelector @change="handleWorkspaceChange" />
    </div>

    <!-- Demo Content -->
    <div class="container py-4">
      <h1 class="mb-4">Workspace Selector Demo</h1>

      <div class="row">
        <div class="col-md-6">

        <div class="card mb-4">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="mb-0">Current Store State</h5>
            <button
              class="btn btn-sm btn-outline-secondary"
              @click="userPreferencesStore.loadFromStorage()"
            >
              Reload from Storage
            </button>
          </div>
          <div class="card-body">
            <table class="table table-sm">
              <tbody>
                <tr>
                  <th>Has Tenant:</th>
                  <td>{{ userPreferencesStore.hasTenant ? 'Yes' : 'No' }}</td>
                </tr>
                <tr>
                  <th>Tenant Key:</th>
                  <td>
                    <code>{{ userPreferencesStore.getCurrentTenantKey || 'null' }}</code>
                  </td>
                </tr>
                <tr>
                  <th>Tenant Name:</th>
                  <td>{{ userPreferencesStore.getCurrentTenant?.name || 'N/A' }}</td>
                </tr>
                <tr>
                  <th>Description:</th>
                  <td>{{ userPreferencesStore.getCurrentTenant?.description || 'N/A' }}</td>
                </tr>
                <tr>
                  <th>Role:</th>
                  <td>{{ getRoleName(userPreferencesStore.getCurrentTenant?.role) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <div class="col-md-6">
        <div class="card">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="mb-0">Change Log</h5>
            <button
              v-if="changeLog.length > 0"
              class="btn btn-sm btn-outline-secondary"
              @click="clearLog"
            >
              Clear
            </button>
          </div>
          <div class="card-body">
            <div
              v-if="changeLog.length === 0"
              class="text-muted text-center py-3"
            >
              No changes yet. Select a workspace to see events.
            </div>
            <ul
              v-else
              class="list-group list-group-flush"
            >
              <li
                v-for="(entry, index) in changeLog"
                :key="index"
                class="list-group-item"
              >
                <small>{{ entry }}</small>
              </li>
            </ul>
          </div>
        </div>

        <div class="card mt-4">
          <div class="card-header">
            <h5 class="mb-0">Features</h5>
          </div>
          <div class="card-body">
            <ul class="mb-0">
              <li>Workspace selection persists in localStorage</li>
              <li>Store state updates automatically</li>
              <li>Changes emit events for integration</li>
              <li>Reload page to verify persistence</li>
            </ul>
          </div>
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

.list-group-item {
  border-left: none;
  border-right: none;
}

.list-group-item:first-child {
  border-top: none;
}
</style>
