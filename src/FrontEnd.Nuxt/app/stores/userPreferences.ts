/**
 * User Preferences Store
 *
 * Manages user preferences including the currently selected workspace (tenant).
 * This store persists the user's workspace selection across sessions using localStorage.
 */

import { defineStore } from 'pinia'
import type { TenantRoleResultDto } from '~/utils/apiclient'

export const useUserPreferencesStore = defineStore('userPreferences', () => {
  // State
  const currentTenant = ref<TenantRoleResultDto | null>(null)

  // Getters
  const getCurrentTenantKey = computed((): string | null => {
    return currentTenant.value?.key || null
  })

  const getCurrentTenant = computed((): TenantRoleResultDto | null => {
    return currentTenant.value
  })

  const hasTenant = computed((): boolean => {
    return currentTenant.value !== null
  })

  // Actions
  function setCurrentTenant(tenant: TenantRoleResultDto | null) {
    currentTenant.value = tenant

    // Persist to localStorage
    if (import.meta.client) {
      if (tenant) {
        localStorage.setItem('userPreferences:tenant', JSON.stringify(tenant))
      } else {
        localStorage.removeItem('userPreferences:tenant')
      }
    }
  }

  function loadFromStorage() {
    if (import.meta.client) {
      const storedTenant = localStorage.getItem('userPreferences:tenant')

      if (storedTenant) {
        try {
          currentTenant.value = JSON.parse(storedTenant)
        } catch (error) {
          console.error('Failed to parse stored tenant:', error)
        }
      }
    }
  }

  function clearPreferences() {
    currentTenant.value = null

    if (import.meta.client) {
      localStorage.removeItem('userPreferences:tenant')
    }
  }

  return {
    // State
    currentTenant,
    // Getters
    getCurrentTenantKey,
    getCurrentTenant,
    hasTenant,
    // Actions
    setCurrentTenant,
    loadFromStorage,
    clearPreferences,
  }
})
