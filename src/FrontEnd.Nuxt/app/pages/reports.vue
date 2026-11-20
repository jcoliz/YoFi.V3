<script setup lang="ts">
import {
  ReportsClient,
  type ReportDefinitionDto,
  type ReportResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'
import { isSupportedReportDisplayDepth } from '~/utils/reportDisplay'

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
const years = ref<number[]>([])
const defaultYear = ref<number>()
const selectedYear = ref<number>()
const report = ref<ReportResultDto>()
const displayDepth = ref(1)
const showMonths = ref(false)
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

watch(
  [ready, tenantKey],
  () => {
    if (ready.value) loadWorkspace()
  },
  { flush: 'sync' },
)

async function loadWorkspace() {
  const version = ++requestVersion
  const key = tenantKey.value
  definitions.value = []
  selectedReportId.value = ''
  years.value = []
  defaultYear.value = undefined
  selectedYear.value = undefined
  report.value = undefined
  error.value = undefined
  showError.value = false
  loading.value = Boolean(key)
  if (!key) return

  try {
    const [available, availability] = await Promise.all([
      reportsClient.getAvailableReports(key),
      reportsClient.getAvailableYears(key),
    ])
    if (version !== requestVersion) return
    const defaultId = available[0]?.id
    if (!defaultId || available.some((definition) => !definition.id)) {
      throw new Error('The report catalog has no valid default report.')
    }
    if (
      !availability ||
      !Array.isArray(availability.years) ||
      !availability.years.length ||
      availability.years.some(
        (year, index, list) =>
          !Number.isInteger(year) ||
          year < 1 ||
          year > 9999 ||
          (index > 0 && year >= list[index - 1]!),
      ) ||
      !availability.years.includes(availability.defaultYear)
    ) {
      throw new Error('The report year list has no valid default year.')
    }
    definitions.value = available
    years.value = availability.years
    defaultYear.value = availability.defaultYear
    let savedId: string | null
    try {
      savedId = preferences.getSelectedReportId(key)
    } catch (err) {
      error.value = handleApiError(err, 'Persistence Failed', 'Failed to read report preferences')
      showError.value = true
      return
    }
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
    if (selectedYear.value === undefined) {
      const saved = preferences.getReportSettings(key).year
      selectedYear.value =
        saved !== undefined && years.value.includes(saved) ? saved : defaultYear.value
    }
  } catch (err) {
    if (version !== requestVersion) return
    error.value = handleApiError(err, 'Persistence Failed', 'Failed to read report preferences')
    showError.value = true
    loading.value = false
    return
  }

  const definition = definitions.value.find((item) => item.id === id)
  if (!definition) {
    error.value = handleApiError(
      new Error('The selected report is unavailable.'),
      'Load Failed',
      'Failed to load the selected report',
    )
    showError.value = true
    loading.value = false
    return
  }

  try {
    const savedDisplay = preferences.getReportDisplaySettings(key, id)
    displayDepth.value =
      savedDisplay.displayDepth !== undefined &&
      isSupportedReportDisplayDepth(savedDisplay.displayDepth)
        ? savedDisplay.displayDepth
        : (definition.defaultDepth ?? 1)
    showMonths.value =
      savedDisplay.showMonths !== undefined
        ? savedDisplay.showMonths
        : (definition.hasMonthColumns ?? false)
  } catch (err) {
    if (version !== requestVersion) return
    error.value = handleApiError(err, 'Persistence Failed', 'Failed to read report preferences')
    showError.value = true
    loading.value = false
    return
  }

  try {
    preferences.setSelectedReportId(key, id)
    preferences.updateReportSettings(key, { year: selectedYear.value })
  } catch (err) {
    if (version !== requestVersion) return
    error.value = handleApiError(err, 'Persistence Failed', 'Failed to save report preferences')
    showError.value = true
    loading.value = false
    return
  }

  try {
    const result = await reportsClient.getReport(
      id,
      selectedYear.value,
      displayDepth.value,
      showMonths.value,
      key,
    )
    if (version === requestVersion) report.value = result
  } catch (err) {
    if (version !== requestVersion) return
    error.value = handleApiError(err, 'Load Failed', 'Failed to load the selected report')
    showError.value = true
  } finally {
    if (version === requestVersion) loading.value = false
  }
}

function updateDisplayDepth(depth: number) {
  const key = tenantKey.value
  const id = selectedReportId.value
  if (!key || !id || !isSupportedReportDisplayDepth(depth)) return
  try {
    preferences.updateReportDisplaySettings(key, id, { displayDepth: depth })
  } catch (err) {
    error.value = handleApiError(err, 'Persistence Failed', 'Failed to save report preferences')
    showError.value = true
    return
  }
  displayDepth.value = depth
  void loadSelectedReport()
}

function updateShowMonths(months: boolean) {
  const key = tenantKey.value
  const id = selectedReportId.value
  if (!key || !id) return
  try {
    preferences.updateReportDisplaySettings(key, id, { showMonths: months })
  } catch (err) {
    error.value = handleApiError(err, 'Persistence Failed', 'Failed to save report preferences')
    showError.value = true
    return
  }
  showMonths.value = months
  void loadSelectedReport()
}

function resetDisplaySettings() {
  const key = tenantKey.value
  const id = selectedReportId.value
  const definition = definitions.value.find((item) => item.id === id)
  if (!key || !id || !definition) return
  try {
    preferences.resetReportDisplaySettings(key, id)
  } catch (err) {
    error.value = handleApiError(err, 'Persistence Failed', 'Failed to save report preferences')
    showError.value = true
    return
  }
  displayDepth.value = definition.defaultDepth ?? 1
  showMonths.value = definition.hasMonthColumns ?? false
  void loadSelectedReport()
}

function retry() {
  if (selectedReportId.value && defaultYear.value !== undefined) {
    loadSelectedReport()
  } else {
    loadWorkspace()
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
          v-if="definitions.length && selectedReportId"
          class="row g-3 mb-4"
        >
          <div class="col-md-9">
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
              @change="loadSelectedReport()"
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
          <div class="col-md-3">
            <label
              for="report-year-selection"
              class="form-label"
              >Year</label
            >
            <select
              id="report-year-selection"
              v-model.number="selectedYear"
              class="form-select"
              data-test-id="report-year-selector"
              @change="loadSelectedReport()"
            >
              <option
                v-for="year in years"
                :key="year"
                :value="year"
              >
                {{ year }}
              </option>
            </select>
          </div>
        </div>
        <ReportsDisplayControls
          v-if="definitions.length && selectedReportId"
          :definition="definitions.find((definition) => definition.id === selectedReportId)!"
          :display-depth="displayDepth"
          :show-months="showMonths"
          :disabled="loading"
          @update:display-depth="updateDisplayDepth"
          @update:show-months="updateShowMonths"
          @reset="resetDisplaySettings"
        />
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
            <ReportsTable :report="report" />
          </div>
        </section>
        <button
          v-else-if="showError"
          class="btn btn-secondary"
          @click="retry"
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
