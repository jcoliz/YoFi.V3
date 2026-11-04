<script setup lang="ts">
/**
 * Sample data section of Manage Workspace: choose a scenario and dates, preview
 * counts, load the complete sample dataset, and download matching OFX data.
 *
 * Loading is append-only and never deletes existing data. The parent should
 * re-key this component when the target workspace changes.
 */
import {
  ApiException,
  SampleDataClient,
  SampleDataRequest,
  type SampleDataLoadResultDto,
  type SampleDataPreviewDto,
  type SampleScenarioDto,
  type TenantRoleResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'
import { fromDateInputValue } from '~/utils/dateOnly'
import { defaultSampleDataDateRanges } from '~/utils/sampleDataDates'

const props = withDefaults(
  defineProps<{
    workspace: TenantRoleResultDto
    /** Changes whenever workspace data changes elsewhere, so the preview is refreshed. */
    dataVersion?: number
  }>(),
  { dataVersion: 0 },
)

const emit = defineEmits<{
  loaded: [result: SampleDataLoadResultDto]
  'update:busy': [value: boolean]
}>()

const sampleDataClient = useApiClient(SampleDataClient)
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()

const defaults = defaultSampleDataDateRanges()
const historicalStart = ref(defaults.historicalStart)
const historicalEnd = ref(defaults.historicalEnd)
const ofxStart = ref(defaults.ofxStart)
const ofxEnd = ref(defaults.ofxEnd)

const scenarios = ref<SampleScenarioDto[]>([])
const scenariosLoading = ref(true)
const selectedScenarioId = ref<string>()
const preview = ref<SampleDataPreviewDto>()
const previewLoading = ref(false)
const loadingSample = ref(false)
const downloading = ref(false)
const showConfirm = ref(false)
const loadResult = ref<SampleDataLoadResultDto>()

const error = ref<IProblemDetails>()
const showError = ref(false)
const confirmError = ref<IProblemDetails>()
const showConfirmError = ref(false)

let previewVersion = 0
let unmounted = false

const selectedScenario = computed(() =>
  scenarios.value.find((scenario) => scenario.id === selectedScenarioId.value),
)
const writing = computed(() => loadingSample.value || downloading.value)

function rangeProblem(start: string, end: string): string | undefined {
  if (!start || !end) return 'Enter both a start and an end date.'
  if (start > end) return 'The start date must be on or before the end date.'
  return undefined
}

const historicalRangeError = computed(() =>
  rangeProblem(historicalStart.value, historicalEnd.value),
)
const ofxRangeError = computed(() => rangeProblem(ofxStart.value, ofxEnd.value))

function buildRequest(start: string, end: string): SampleDataRequest | undefined {
  const scenario = selectedScenario.value
  if (!scenario?.id || scenario.revision === undefined) return undefined
  return new SampleDataRequest({
    scenarioId: scenario.id,
    revision: scenario.revision,
    startDate: fromDateInputValue(start),
    endDate: fromDateInputValue(end),
  })
}

function showProblem(problem: IProblemDetails) {
  error.value = problem
  showError.value = true
}

/** A lost connection or timeout does not prove whether a write committed. */
function isUncertainOutcome(problem: IProblemDetails): boolean {
  return (
    problem.status === 408 ||
    problem.title === 'Network Error' ||
    problem.title === 'Request timed out'
  )
}

onMounted(loadScenarios)
onBeforeUnmount(() => {
  unmounted = true
  previewVersion++
})

async function loadScenarios() {
  const workspaceKey = props.workspace.key
  if (!workspaceKey) return
  scenariosLoading.value = true
  try {
    const result = await sampleDataClient.getScenarios(workspaceKey)
    if (unmounted) return
    scenarios.value = result
    selectedScenarioId.value = result[0]?.id
  } catch (err) {
    if (unmounted) return
    showProblem(handleApiError(err, 'Load Failed', 'Failed to load sample data scenarios'))
  } finally {
    if (!unmounted) scenariosLoading.value = false
  }
}

watch([selectedScenarioId, historicalStart, historicalEnd], () => {
  loadResult.value = undefined
  refreshPreview()
})

watch(
  () => props.dataVersion,
  () => {
    loadResult.value = undefined
    refreshPreview()
  },
)

async function refreshPreview() {
  const version = ++previewVersion
  preview.value = undefined
  showConfirm.value = false

  const workspaceKey = props.workspace.key
  const request = historicalRangeError.value
    ? undefined
    : buildRequest(historicalStart.value, historicalEnd.value)
  if (!workspaceKey || !request) {
    previewLoading.value = false
    return
  }

  previewLoading.value = true
  try {
    const result = await sampleDataClient.preview(workspaceKey, request)
    if (version !== previewVersion) return
    preview.value = result
  } catch (err) {
    if (version !== previewVersion) return
    showProblem(handleApiError(err, 'Preview Failed', 'Failed to preview sample data'))
  } finally {
    if (version === previewVersion) previewLoading.value = false
  }
}

function openConfirm() {
  if (!preview.value || writing.value) return
  confirmError.value = undefined
  showConfirmError.value = false
  showConfirm.value = true
}

function closeConfirm(value: boolean) {
  if (loadingSample.value) return
  showConfirm.value = value
}

async function loadSampleData() {
  if (loadingSample.value) return

  const workspaceKey = props.workspace.key
  const request = preview.value?.inputs
    ? buildRequest(historicalStart.value, historicalEnd.value)
    : undefined
  if (!workspaceKey || !request) return

  loadingSample.value = true
  emit('update:busy', true)
  confirmError.value = undefined
  showConfirmError.value = false

  try {
    const result = await sampleDataClient.load(workspaceKey, request)
    if (unmounted) return
    showConfirm.value = false
    await refreshPreview()
    loadResult.value = result
    emit('loaded', result)
  } catch (err) {
    if (unmounted) return
    const problem = handleApiError(err, 'Load Failed', 'Failed to load sample data')
    if (isUncertainOutcome(problem)) {
      problem.detail = `${problem.detail} The sample data may still have been added. Check your transactions before trying again.`
    }
    confirmError.value = problem
    showConfirmError.value = true
  } finally {
    loadingSample.value = false
    emit('update:busy', false)
  }
}

function fileNameFrom(contentDisposition: string | null): string | undefined {
  if (!contentDisposition) return undefined
  const encoded = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(contentDisposition)
  if (encoded?.[1]) return decodeURIComponent(encoded[1].replace(/"/g, ''))
  const plain = /filename="?([^";]+)"?/i.exec(contentDisposition)
  return plain?.[1]
}

/**
 * Downloads sample OFX data.
 *
 * Calls the endpoint directly rather than through the generated client, which
 * serializes date-only query parameters as UTC timestamps instead of "yyyy-MM-dd".
 */
async function downloadOfx() {
  if (downloading.value || ofxRangeError.value) return

  const workspaceKey = props.workspace.key
  const scenario = selectedScenario.value
  if (!workspaceKey || !scenario?.id || scenario.revision === undefined) return

  const query = new URLSearchParams({
    ScenarioId: scenario.id,
    Revision: String(scenario.revision),
    StartDate: ofxStart.value,
    EndDate: ofxEnd.value,
  })
  const url = `${baseUrl}/api/tenant/${encodeURIComponent(workspaceKey)}/admin/sample-data/ofx?${query}`

  downloading.value = true
  emit('update:busy', true)

  try {
    const response = await authFetch.fetch(url, {
      method: 'GET',
      headers: { Accept: 'application/octet-stream' },
    })
    if (!response.ok) {
      const headers: Record<string, string> = {}
      response.headers.forEach((value, key) => (headers[key] = value))
      throw new ApiException(
        'A server side error occurred.',
        response.status,
        await response.text(),
        headers,
        null,
      )
    }

    const blob = await response.blob()
    const fileName =
      fileNameFrom(response.headers.get('content-disposition')) ??
      `sample-${scenario.id}-${ofxStart.value}-${ofxEnd.value}.ofx`
    const objectUrl = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = objectUrl
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(objectUrl)
  } catch (err) {
    if (unmounted) return
    showProblem(handleApiError(err, 'Download Failed', 'Failed to download sample OFX data'))
  } finally {
    downloading.value = false
    emit('update:busy', false)
  }
}
</script>

<template>
  <div data-test-id="sample-data-pane">
    <ErrorDisplay
      v-model:show="showError"
      :problem="error"
      class="mb-3"
    />

    <div
      v-if="scenariosLoading"
      class="text-muted"
      data-test-id="sample-scenarios-loading"
    >
      <BaseSpinner
        size="sm"
        class="me-1"
      />
      Loading scenarios...
    </div>

    <p
      v-else-if="scenarios.length === 0"
      class="text-muted mb-0"
      data-test-id="sample-scenarios-empty"
    >
      No sample data scenarios are available.
    </p>

    <template v-else>
      <div class="mb-3">
        <label
          for="sample-scenario"
          class="form-label"
          >Scenario</label
        >
        <select
          id="sample-scenario"
          v-model="selectedScenarioId"
          class="form-select"
          :disabled="writing"
          data-test-id="sample-scenario-select"
        >
          <option
            v-for="scenario in scenarios"
            :key="scenario.id"
            :value="scenario.id"
          >
            {{ scenario.name }}
          </option>
        </select>
        <div
          v-if="selectedScenario?.description"
          class="form-text"
          data-test-id="sample-scenario-description"
        >
          {{ selectedScenario.description }}
        </div>
      </div>

      <h6 class="mt-4">Load sample data</h6>
      <div class="row g-3 mb-2">
        <div class="col-sm-6">
          <label
            for="sample-start-date"
            class="form-label"
            >Start date</label
          >
          <input
            id="sample-start-date"
            v-model="historicalStart"
            type="date"
            class="form-control"
            :disabled="writing"
            data-test-id="sample-start-date"
          />
        </div>
        <div class="col-sm-6">
          <label
            for="sample-end-date"
            class="form-label"
            >End date</label
          >
          <input
            id="sample-end-date"
            v-model="historicalEnd"
            type="date"
            class="form-control"
            :disabled="writing"
            data-test-id="sample-end-date"
          />
        </div>
      </div>
      <p
        v-if="historicalRangeError"
        class="small text-danger"
        data-test-id="sample-range-error"
      >
        {{ historicalRangeError }}
      </p>

      <div
        class="mb-3"
        data-test-id="sample-preview"
      >
        <span
          v-if="previewLoading"
          class="text-muted"
        >
          <BaseSpinner
            size="sm"
            class="me-1"
          />
          Calculating...
        </span>
        <span v-else-if="preview?.counts">
          This adds
          <strong data-test-id="sample-preview-transactions">{{
            preview.counts.transactions
          }}</strong>
          transactions and
          <strong data-test-id="sample-preview-rules">{{
            preview.counts.payeeMatchingRules
          }}</strong>
          payee matching rules.
        </span>
      </div>

      <button
        type="button"
        class="btn btn-primary"
        :disabled="!preview || previewLoading || writing"
        data-test-id="load-sample-data-button"
        @click="openConfirm"
      >
        Load Sample Data
      </button>

      <div
        v-if="loadResult?.created"
        class="alert alert-success mt-3 mb-0"
        role="status"
        data-test-id="sample-load-result"
      >
        Added
        <strong data-test-id="sample-created-transactions">{{
          loadResult.created.transactions
        }}</strong>
        transactions and
        <strong data-test-id="sample-created-rules">{{
          loadResult.created.payeeMatchingRules
        }}</strong>
        payee matching rules.
        <div class="mt-2 d-flex flex-wrap gap-3">
          <NuxtLink
            to="/transactions"
            data-test-id="sample-result-transactions-link"
            >View Transactions</NuxtLink
          >
          <NuxtLink
            to="/payee-rules"
            data-test-id="sample-result-payee-rules-link"
            >View Payee Rules</NuxtLink
          >
          <NuxtLink
            to="/import"
            data-test-id="sample-result-import-link"
            >Go to Bank Import</NuxtLink
          >
        </div>
      </div>

      <h6 class="mt-4">Download sample bank data</h6>
      <p class="small text-muted">
        Downloads an OFX file you can upload on the Bank Import page. It doesn't change your
        workspace. Load the sample data first so its payee matching rules categorize the imported
        transactions.
      </p>
      <div class="row g-3 mb-2">
        <div class="col-sm-6">
          <label
            for="sample-ofx-start-date"
            class="form-label"
            >Start date</label
          >
          <input
            id="sample-ofx-start-date"
            v-model="ofxStart"
            type="date"
            class="form-control"
            :disabled="writing"
            data-test-id="sample-ofx-start-date"
          />
        </div>
        <div class="col-sm-6">
          <label
            for="sample-ofx-end-date"
            class="form-label"
            >End date</label
          >
          <input
            id="sample-ofx-end-date"
            v-model="ofxEnd"
            type="date"
            class="form-control"
            :disabled="writing"
            data-test-id="sample-ofx-end-date"
          />
        </div>
      </div>
      <p
        v-if="ofxRangeError"
        class="small text-danger"
        data-test-id="sample-ofx-range-error"
      >
        {{ ofxRangeError }}
      </p>
      <button
        type="button"
        class="btn btn-outline-primary"
        :disabled="!selectedScenario || !!ofxRangeError || writing"
        data-test-id="download-sample-ofx-button"
        @click="downloadOfx"
      >
        <BaseSpinner
          v-if="downloading"
          size="sm"
          class="me-1"
        />
        Download Sample OFX
      </button>
    </template>

    <ModalDialog
      :show="showConfirm"
      title="Load Sample Data"
      :loading="loadingSample"
      :primary-button-text="loadingSample ? 'Loading...' : 'Load Sample Data'"
      primary-button-test-id="load-sample-submit-button"
      secondary-button-test-id="load-sample-cancel-button"
      test-id="load-sample-modal"
      @primary="loadSampleData"
      @update:show="closeConfirm"
    >
      <ErrorDisplay
        v-model:show="showConfirmError"
        :problem="confirmError"
      />
      <p>
        This adds
        <strong>{{ preview?.counts?.transactions }}</strong> sample transactions and
        <strong>{{ preview?.counts?.payeeMatchingRules }}</strong> payee matching rules to
        <strong data-test-id="load-sample-workspace-name">{{ workspace.name }}</strong
        >.
      </p>
      <p
        v-if="(preview?.existingTransactionCount ?? 0) > 0"
        data-test-id="load-sample-existing-warning"
      >
        This workspace already has
        <strong>{{ preview?.existingTransactionCount }}</strong> transactions.
      </p>
      <p class="text-muted mb-0">
        Existing data will not be removed or updated. Loading sample data again adds another copy,
        including additional matching rules. Existing and sample rules may affect categorization of
        future imports.
      </p>
    </ModalDialog>
  </div>
</template>
