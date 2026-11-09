<script setup lang="ts">
import {
  ReportsClient,
  type ReportDefinitionDto,
  type ReportResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Reports',
  order: 5,
  auth: false,
  layout: 'chrome',
})

const preferences = useUserPreferencesStore()
const reportsClient = useApiClient(ReportsClient)
const ready = ref(false)
const loading = ref(false)
const definitions = ref<ReportDefinitionDto[]>([])
const selectedReportId = ref('')
const report = ref<ReportResultDto>()
const error = ref<IProblemDetails>()
const showError = ref(false)
const tenantKey = computed(() => preferences.getCurrentTenantKey)

// Only the latest workspace/selection request may update the page.
let requestVersion = 0
onBeforeUnmount(() => requestVersion++)

useAuthenticationGuard().requireAuthenticatedUser(async () => {
  preferences.loadFromStorage()
  ready.value = true
})

watch([ready, tenantKey], () => {
  if (ready.value) loadWorkspace()
})

async function loadWorkspace() {
  const version = ++requestVersion
  const key = tenantKey.value
  definitions.value = []
  selectedReportId.value = ''
  report.value = undefined
  error.value = undefined
  showError.value = false
  loading.value = Boolean(key)
  if (!key) return

  try {
    const available = await reportsClient.getAvailableReports(key)
    if (version !== requestVersion) return
    const defaultId = available[0]?.id
    if (!defaultId || available.some((definition) => !definition.id)) {
      throw new Error('The report catalog has no valid default report.')
    }
    definitions.value = available
    const savedId = preferences.getSelectedReportId(key)
    selectedReportId.value = available.some((definition) => definition.id === savedId)
      ? savedId!
      : defaultId
    await loadSelectedReport()
  } catch (err) {
    if (version !== requestVersion) return
    error.value = handleApiError(err, 'Load Failed', 'Failed to load reports')
    showError.value = true
  } finally {
    if (version === requestVersion) loading.value = false
  }
}

async function loadSelectedReport() {
  const version = ++requestVersion
  const key = tenantKey.value
  const id = selectedReportId.value
  report.value = undefined
  error.value = undefined
  showError.value = false
  if (!key || !id) return
  loading.value = true
  try {
    preferences.setSelectedReportId(key, id)
    const result = await reportsClient.getReport(id, undefined, key)
    if (version === requestVersion) report.value = result
  } catch (err) {
    if (version !== requestVersion) return
    error.value = handleApiError(err, 'Load Failed', 'Failed to load the selected report')
    showError.value = true
  } finally {
    if (version === requestVersion) loading.value = false
  }
}
</script>

<template>
  <div>
    <div class="workspace-selector-container">
      <WorkspaceSelector />
    </div>
    <div class="container py-4">
      <h1
        class="mb-4"
        data-test-id="page-heading"
      >
        Reports
      </h1>
      <div
        v-if="ready && !tenantKey"
        class="alert alert-warning"
        data-test-id="no-workspace-warning"
      >
        Please select a workspace to view reports
      </div>
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-4"
      />
      <template v-if="ready && tenantKey">
        <div
          v-if="definitions.length"
          class="mb-4"
        >
          <label
            for="report-selection"
            class="form-label"
            >Report</label
          >
          <select
            id="report-selection"
            v-model="selectedReportId"
            class="form-select"
            data-test-id="report-selector"
            @change="loadSelectedReport"
          >
            <option
              v-for="definition in definitions"
              :key="definition.id"
              :value="definition.id"
            >
              {{ definition.name }}
            </option>
          </select>
        </div>
        <div
          v-if="loading"
          class="text-center py-5"
          role="status"
          data-test-id="loading-state"
        >
          <BaseSpinner />
          <div class="mt-2 text-muted">Loading report...</div>
        </div>
        <section
          v-else-if="report"
          aria-labelledby="report-heading"
          data-test-id="report-result"
        >
          <h2
            id="report-heading"
            class="h4 mb-3"
          >
            {{ report.name }} &mdash; {{ report.year }}
          </h2>
          <div class="card">
            <ReportsTable
              :report="report"
              :display-depth="
                definitions.find((definition) => definition.id === report?.id)?.defaultDepth
              "
            />
          </div>
        </section>
        <button
          v-else-if="showError"
          class="btn btn-secondary"
          @click="loadWorkspace"
        >
          Retry
        </button>
      </template>
    </div>
  </div>
</template>
<style scoped>
.workspace-selector-container {
  background-color: #f8f9fa;
  border-bottom: 1px solid #dee2e6;
  padding: 0.75rem 1rem;
}
</style>
