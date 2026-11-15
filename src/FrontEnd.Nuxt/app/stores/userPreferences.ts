/**
 * User Preferences Store
 *
 * Manages user preferences including the currently selected workspace (tenant).
 * This store persists the user's workspace selection across sessions using localStorage.
 */

import { defineStore } from 'pinia'
import type { TenantRoleResultDto } from '~/utils/apiclient'

export interface ReportSettings {
  year?: number
}

export const useUserPreferencesStore = defineStore('userPreferences', () => {
  // State
  const currentTenant = ref<TenantRoleResultDto | undefined>(undefined)

  // Getters
  const getCurrentTenantKey = computed((): string => {
    return currentTenant.value?.key || ''
  })

  const getCurrentTenant = computed((): TenantRoleResultDto | undefined => {
    return currentTenant.value
  })

  const hasTenant = computed((): boolean => {
    return currentTenant.value !== undefined
  })

  // Actions
  function setCurrentTenant(tenant: TenantRoleResultDto | undefined) {
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
    currentTenant.value = undefined

    if (import.meta.client) {
      localStorage.removeItem('userPreferences:tenant')
    }
  }

  function getSelectedReportId(tenantKey: string): string | null {
    if (typeof window === 'undefined') return null
    return localStorage.getItem(`userPreferences:tenant:${tenantKey}:reports:selectedId`)
  }

  function setSelectedReportId(tenantKey: string, reportId: string) {
    if (typeof window === 'undefined') return
    localStorage.setItem(`userPreferences:tenant:${tenantKey}:reports:selectedId`, reportId)
  }

  function getReportSettings(tenantKey: string, reportId: string): ReportSettings {
    if (typeof window === 'undefined') return {}
    const stored = localStorage.getItem(
      `userPreferences:tenant:${tenantKey}:reports:${reportId}:settings`,
    )
    if (stored === null) return {}

    try {
      const settings: unknown = JSON.parse(stored)
      if (typeof settings !== 'object' || settings === null || Array.isArray(settings)) {
        throw new Error('Report settings must be an object.')
      }
      const year = 'year' in settings ? settings.year : undefined
      if (
        year !== undefined &&
        (typeof year !== 'number' || !Number.isInteger(year) || year < 1 || year > 9999)
      ) {
        throw new Error('Report year must be an integer between 1 and 9999.')
      }
      return { ...settings, year }
    } catch (error) {
      console.error('Failed to parse stored report settings:', error)
      return {}
    }
  }

  function updateReportSettings(tenantKey: string, reportId: string, settings: ReportSettings) {
    if (typeof window === 'undefined') return
    const existing = getReportSettings(tenantKey, reportId)
    localStorage.setItem(
      `userPreferences:tenant:${tenantKey}:reports:${reportId}:settings`,
      JSON.stringify({ ...existing, ...settings }),
    )
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
    getSelectedReportId,
    setSelectedReportId,
    getReportSettings,
    updateReportSettings,
  }
})
