<script setup lang="ts">
/**
 * Lets a workspace owner select data and date filters, then download a native
 * YoFi.V3 export workbook with per-worksheet record counts.
 */
import {
  TenantDataAdminClient,
  type IProblemDetails,
  type TenantDataExportTypeDto,
  type TenantRoleResultDto,
} from '~/utils/apiclient'
import { fromDateInputValue } from '~/utils/dateOnly'
import { handleApiError } from '~/utils/errorHandler'
import { useToastStore } from '~/stores/toast'

interface ExportCount {
  dataType: string
  recordCount: number
}

const props = defineProps<{
  /** Workspace whose data is exported. */
  workspace: TenantRoleResultDto
}>()

const emit = defineEmits<{
  /** Reports whether an export operation is active, so the parent can guard navigation. */
  'update:busy': [value: boolean]
}>()

const tenantDataAdminClient = useApiClient(TenantDataAdminClient)
const toastStore = useToastStore()
const dataTypes = ref<TenantDataExportTypeDto[]>([])
const years = ref<number[]>([])
const selectedTypes = ref<string[]>([])
const dateSelection = ref('all')
const customStartDate = ref('')
const customEndDate = ref('')
const loading = ref(true)
const exporting = ref(false)
const error = ref<IProblemDetails>()
const showError = ref(false)

const rangeError = computed(() => {
  if (
    dateSelection.value === 'custom' &&
    customStartDate.value &&
    customEndDate.value &&
    customStartDate.value > customEndDate.value
  ) {
    return 'The start date must be on or before the end date.'
  }
  return ''
})

const canExportSelection = computed(
  () => selectedTypes.value.length > 0 && !rangeError.value && !exporting.value,
)

function showProblem(problem: IProblemDetails) {
  error.value = problem
  showError.value = true
}

function typeLabel(type: TenantDataExportTypeDto): string {
  if (type.dataType === 'PayeeMatchingRules') return 'Payee Matching Rules'
  return type.worksheetName ?? type.dataType ?? 'Data'
}

async function loadOptions() {
  const workspaceKey = props.workspace.key
  if (!workspaceKey) return

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    const options = await tenantDataAdminClient.getExportOptions(workspaceKey)
    dataTypes.value = options.dataTypes ?? []
    years.value = options.years ?? []
    selectedTypes.value = dataTypes.value.flatMap((type) => (type.dataType ? [type.dataType] : []))
  } catch (err) {
    showProblem(handleApiError(err, 'Load Failed', 'Failed to load export options'))
  } finally {
    loading.value = false
  }
}

function selectAllTypes() {
  selectedTypes.value = dataTypes.value.flatMap((type) => (type.dataType ? [type.dataType] : []))
}

function selectNoTypes() {
  selectedTypes.value = []
}

function getDateBounds(): { startDate?: Date; endDate?: Date } {
  if (selectedYear.value) {
    const year = Number(selectedYear.value)
    return {
      startDate: fromDateInputValue(`${year}-01-01`),
      endDate: fromDateInputValue(`${year}-12-31`),
    }
  }

  if (dateSelection.value === 'custom') {
    return {
      ...(customStartDate.value ? { startDate: fromDateInputValue(customStartDate.value) } : {}),
      ...(customEndDate.value ? { endDate: fromDateInputValue(customEndDate.value) } : {}),
    }
  }

  return {}
}

const selectedYear = computed(() =>
  dateSelection.value.startsWith('year:') ? dateSelection.value.slice(5) : '',
)

function findHeader(
  headers: Record<string, unknown> | undefined,
  name: string,
): string | undefined {
  const key = Object.keys(headers ?? {}).find(
    (header) => header.toLowerCase() === name.toLowerCase(),
  )
  const value = key ? headers?.[key] : undefined
  return typeof value === 'string' ? value : undefined
}

function readCounts(headers: Record<string, unknown> | undefined): ExportCount[] {
  const countsHeader = findHeader(headers, 'X-YoFi-Export-Counts')
  if (!countsHeader) return []

  const parsed: unknown = JSON.parse(countsHeader)
  if (!Array.isArray(parsed)) {
    throw new Error('The export response contained invalid record counts.')
  }
  return parsed.map((item) => {
    if (
      typeof item !== 'object' ||
      item === null ||
      typeof (item as Record<string, unknown>).DataType !== 'string' ||
      typeof (item as Record<string, unknown>).RecordCount !== 'number'
    ) {
      throw new Error('The export response contained invalid record counts.')
    }
    const count = item as Record<string, unknown>
    return {
      dataType: count.DataType as string,
      recordCount: count.RecordCount as number,
    }
  })
}

function downloadFile(fileName: string | undefined, blob: Blob) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName ?? 'yofi-export.xlsx'
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

async function exportData(allData = false) {
  const workspaceKey = props.workspace.key
  if (!workspaceKey || exporting.value || (!allData && !canExportSelection.value)) return

  exporting.value = true
  emit('update:busy', true)
  error.value = undefined
  showError.value = false

  try {
    const bounds = allData ? {} : getDateBounds()
    const response = await tenantDataAdminClient.exportData(
      workspaceKey,
      allData ? [] : selectedTypes.value,
      bounds.startDate,
      bounds.endDate,
      allData,
    )
    const exportCounts = readCounts(response.headers)
    downloadFile(response.fileName, response.data)
    const countSummary =
      exportCounts
        .map((count) => {
          const type = dataTypes.value.find((item) => item.dataType === count.dataType)
          return `${type ? typeLabel(type) : count.dataType}: ${count.recordCount}`
        })
        .join('; ') || 'Record counts are unavailable.'
    const fileName = response.fileName ? `Downloaded ${response.fileName}. ` : 'Export downloaded. '
    toastStore.success(`${fileName}${countSummary}`, 'Export complete', 'workspace-export-complete')
  } catch (err) {
    showProblem(
      handleApiError(
        err,
        'Export Failed',
        err instanceof Error ? err.message : 'Failed to export workspace data',
      ),
    )
  } finally {
    exporting.value = false
    emit('update:busy', false)
  }
}

onMounted(loadOptions)
</script>

<template>
  <section
    class="card"
    aria-labelledby="export-data-heading"
    data-test-id="export-data-pane"
  >
    <div class="card-body">
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-3"
        data-test-id="export-error"
      />

      <div
        v-if="loading"
        class="text-muted"
        data-test-id="export-options-loading"
      >
        <BaseSpinner
          size="sm"
          class="me-1"
        />
        Loading export options...
      </div>

      <template v-else>
        <p
          v-if="dataTypes.length === 0"
          class="text-muted mb-0"
          data-test-id="export-types-empty"
        >
          No workspace data types are available to export.
        </p>

        <template v-else>
          <div class="d-flex align-items-center justify-content-between mb-2">
            <h3
              id="export-data-heading"
              class="h5 mb-0"
            >
              Export Data
            </h3>
            <div>
              <button
                type="button"
                class="btn btn-sm btn-link"
                :disabled="exporting"
                data-test-id="select-all-export-types"
                @click="selectAllTypes"
              >
                Select All
              </button>
              <button
                type="button"
                class="btn btn-sm btn-link"
                :disabled="exporting"
                data-test-id="clear-export-types"
                @click="selectNoTypes"
              >
                Clear All
              </button>
            </div>
          </div>

          <div class="mb-3">
            <div
              v-for="type in dataTypes"
              :key="type.dataType"
              class="form-check"
            >
              <input
                :id="`export-type-${type.dataType}`"
                v-model="selectedTypes"
                class="form-check-input"
                type="checkbox"
                :value="type.dataType"
                :disabled="exporting"
                data-test-id="export-type"
              />
              <label
                class="form-check-label"
                :for="`export-type-${type.dataType}`"
              >
                {{ typeLabel(type) }}
                <HelpText v-if="type.dataType === 'Transactions'"> (includes splits) </HelpText>
              </label>
            </div>
          </div>

          <div class="row g-3 mb-3">
            <div class="col-md-4">
              <label
                for="export-date-selection"
                class="form-label"
              >
                Transaction dates
              </label>
              <select
                id="export-date-selection"
                v-model="dateSelection"
                class="form-select"
                :disabled="exporting"
                data-test-id="export-date-selection"
              >
                <option value="all">All Years</option>
                <option
                  v-for="year in years"
                  :key="year"
                  :value="`year:${year}`"
                >
                  {{ year }}
                </option>
                <option value="custom">Custom date range</option>
              </select>
            </div>

            <template v-if="dateSelection === 'custom'">
              <div class="col-md-4">
                <label
                  for="export-start-date"
                  class="form-label"
                >
                  Start date (optional)
                </label>
                <input
                  id="export-start-date"
                  v-model="customStartDate"
                  type="date"
                  class="form-control"
                  :disabled="exporting"
                  data-test-id="export-start-date"
                />
              </div>
              <div class="col-md-4">
                <label
                  for="export-end-date"
                  class="form-label"
                >
                  End date (optional)
                </label>
                <input
                  id="export-end-date"
                  v-model="customEndDate"
                  type="date"
                  class="form-control"
                  :disabled="exporting"
                  data-test-id="export-end-date"
                />
              </div>
            </template>
          </div>

          <p
            v-if="rangeError"
            class="small text-danger"
            data-test-id="export-range-error"
          >
            {{ rangeError }}
          </p>
          <HelpText>
            Date filters apply to transactions. Payee Matching Rules are exported regardless of date
            selection.
          </HelpText>

          <div class="d-flex flex-wrap gap-2">
            <button
              type="button"
              class="btn btn-primary"
              :disabled="!canExportSelection"
              data-test-id="export-selected-button"
              @click="exportData()"
            >
              {{
                exporting ? 'Exporting...' : `Export Data (${selectedTypes.length} types selected)`
              }}
            </button>
            <button
              type="button"
              class="btn btn-outline-primary"
              :disabled="exporting"
              data-test-id="export-all-data-button"
              @click="exportData(true)"
            >
              {{ exporting ? 'Exporting...' : 'Export All Data' }}
            </button>
          </div>
        </template>
      </template>
    </div>
  </section>
</template>
