<script setup lang="ts">
/**
 * Import the Payee worksheet from an original YoFi XLSX export into a workspace.
 */
import {
  TenantDataAdminClient,
  type DataImportIssueDto,
  type IProblemDetails,
  type TenantDataImportPreviewDto,
  type TenantDataImportResultDto,
  type TenantRoleResultDto,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  workspace: TenantRoleResultDto
}>()

const emit = defineEmits<{
  imported: [result: TenantDataImportResultDto]
  'update:busy': [value: boolean]
}>()

const tenantDataAdminClient = useApiClient(TenantDataAdminClient)
const selectedFile = ref<File>()
const preview = ref<TenantDataImportPreviewDto>()
const result = ref<TenantDataImportResultDto>()
const previewing = ref(false)
const importing = ref(false)
const showConfirm = ref(false)
const isDragging = ref(false)
const error = ref<IProblemDetails>()
const showError = ref(false)
const confirmError = ref<IProblemDetails>()
const showConfirmError = ref(false)

const busy = computed(() => previewing.value || importing.value)
const payeeWorksheet = computed(() =>
  preview.value?.worksheets?.find(
    (worksheet) => worksheet.worksheetName?.trim().toLowerCase() === 'payee',
  ),
)
const issues = computed(() => [
  ...(payeeWorksheet.value?.errors ?? []),
  ...(payeeWorksheet.value?.skippedRows ?? []),
])
const canImport = computed(
  () =>
    !!selectedFile.value &&
    !!preview.value?.sha256 &&
    preview.value.canImport === true &&
    payeeWorksheet.value?.canImport === true &&
    !busy.value,
)

function showProblem(problem: IProblemDetails) {
  error.value = problem
  showError.value = true
}

function selectFile(file?: File) {
  if (busy.value) return

  preview.value = undefined
  result.value = undefined
  showConfirm.value = false
  error.value = undefined
  showError.value = false
  confirmError.value = undefined
  showConfirmError.value = false

  if (file && !file.name.toLowerCase().endsWith('.xlsx')) {
    selectedFile.value = undefined
    showProblem({
      title: 'Unsupported file',
      detail: 'Select an original YoFi .xlsx workbook.',
    })
    return
  }

  selectedFile.value = file
}

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  selectFile(input.files?.[0])
  input.value = ''
}

function handleDrop(event: DragEvent) {
  isDragging.value = false
  if (busy.value) return
  selectFile(event.dataTransfer?.files[0])
}

async function previewFile() {
  const file = selectedFile.value
  const workspaceKey = props.workspace.key
  if (!file || !workspaceKey || busy.value) return

  previewing.value = true
  emit('update:busy', true)
  preview.value = undefined
  result.value = undefined
  error.value = undefined
  showError.value = false

  try {
    preview.value = await tenantDataAdminClient.previewImport(workspaceKey, {
      data: file,
      fileName: file.name,
    })
  } catch (err) {
    showProblem(handleApiError(err, 'Preview Failed', 'Failed to preview workbook'))
  } finally {
    previewing.value = false
    emit('update:busy', false)
  }
}

function openConfirm() {
  if (!canImport.value) return
  confirmError.value = undefined
  showConfirmError.value = false
  showConfirm.value = true
}

async function importPayees() {
  const file = selectedFile.value
  const sha256 = preview.value?.sha256
  const workspaceKey = props.workspace.key
  if (!file || !sha256 || !workspaceKey || !canImport.value) return

  importing.value = true
  emit('update:busy', true)
  confirmError.value = undefined
  showConfirmError.value = false

  try {
    const importResult = await tenantDataAdminClient.importData(
      workspaceKey,
      { data: file, fileName: file.name },
      sha256,
    )
    result.value = importResult
    showConfirm.value = false
    emit('imported', importResult)
  } catch (err) {
    const problem = handleApiError(err, 'Import Failed', 'Failed to import payees')
    if (problem.status === 400) {
      preview.value = undefined
      showConfirm.value = false
      showProblem({
        ...problem,
        detail: `${problem.detail ?? 'The workbook could not be imported.'} Preview it again before importing.`,
      })
    } else {
      confirmError.value = problem
      showConfirmError.value = true
    }
  } finally {
    importing.value = false
    emit('update:busy', false)
  }
}

function csvCell(value: string | number | undefined): string {
  let text = String(value ?? '')
  if (/^[=+\-@]/.test(text)) text = `'${text}`
  return `"${text.replaceAll('"', '""')}"`
}

function downloadErrorReport() {
  if (issues.value.length === 0) return

  const rows = [
    ['Worksheet', 'Row Number', 'Field', 'Error Code', 'Message'],
    ...issues.value.map((issue: DataImportIssueDto) => [
      payeeWorksheet.value?.worksheetName ?? 'Payee',
      issue.rowNumber,
      issue.field,
      issue.code,
      issue.message,
    ]),
  ]
  const csv = rows.map((row) => row.map(csvCell).join(',')).join('\r\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = 'payee-import-errors.csv'
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <section
    class="card h-100"
    aria-labelledby="import-data-heading"
    data-test-id="import-data-card"
  >
    <div class="card-body">
      <CardTitle
        id="import-data-heading"
        data-test-id="import-data-title"
        class="mb-3"
        title="Import Data"
        subtitle="Import payee matching rules from an original YoFi workbook."
      />

      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-3"
      />

      <div
        class="border rounded p-3 text-center"
        :class="{ 'border-primary bg-light': isDragging }"
        data-test-id="import-file-dropzone"
        @dragover.prevent="isDragging = true"
        @dragleave.prevent="isDragging = false"
        @drop.prevent="handleDrop"
      >
        <label
          for="workspace-import-file"
          class="form-label"
        >
          Choose an original YoFi workbook (.xlsx), or drop it here.
        </label>
        <input
          id="workspace-import-file"
          type="file"
          class="form-control"
          accept=".xlsx"
          :disabled="busy"
          data-test-id="import-file-input"
          @change="handleFileChange"
        />
      </div>

      <p
        v-if="selectedFile"
        class="small text-muted mt-2 mb-3"
        data-test-id="import-selected-file"
      >
        Selected: {{ selectedFile.name }}
      </p>

      <button
        type="button"
        class="btn btn-outline-primary mt-3"
        :disabled="!selectedFile || busy"
        data-test-id="preview-import-button"
        @click="previewFile"
      >
        <BaseSpinner
          v-if="previewing"
          size="sm"
          class="me-1"
        />
        {{ previewing ? 'Previewing...' : 'Preview Import' }}
      </button>

      <div
        v-if="preview"
        class="mt-3"
        data-test-id="import-preview"
      >
        <template v-if="payeeWorksheet">
          <p>
            <strong>Payee worksheet:</strong>
            {{ payeeWorksheet.validRecordCount ?? 0 }} valid rules,
            {{ payeeWorksheet.skippedRecordCount ?? 0 }} skipped,
            {{ payeeWorksheet.errors?.length ?? 0 }} errors.
          </p>
          <p
            v-if="payeeWorksheet.ignoredColumns?.length"
            class="small text-muted"
            data-test-id="import-ignored-columns"
          >
            Ignored columns: {{ payeeWorksheet.ignoredColumns.join(', ') }}.
          </p>
          <div
            v-if="issues.length"
            class="mb-3"
            data-test-id="import-row-issues"
          >
            <h6>Rows needing attention</h6>
            <ul class="small ps-3 mb-2">
              <li
                v-for="(issue, index) in issues"
                :key="`${issue.rowNumber}-${issue.field}-${issue.code}-${index}`"
              >
                Row {{ issue.rowNumber }}<span v-if="issue.field">, {{ issue.field }}</span
                >:
                {{ issue.message }}
                <span v-if="payeeWorksheet.errors?.includes(issue)">(error)</span>
                <span v-else>(skipped)</span>
              </li>
            </ul>
            <button
              type="button"
              class="btn btn-sm btn-outline-secondary"
              data-test-id="download-import-errors-button"
              @click="downloadErrorReport"
            >
              Download CSV report
            </button>
          </div>
        </template>
        <p
          v-else
          class="text-warning"
          data-test-id="import-no-payee-sheet"
        >
          No Payee worksheet was found. This workbook cannot be imported.
        </p>

        <p
          v-if="preview.ignoredWorksheets?.length"
          class="small text-muted"
          data-test-id="import-ignored-worksheets"
        >
          Other worksheets will not be imported:
          {{ preview.ignoredWorksheets.join(', ') }}.
        </p>

        <div
          v-if="result"
          class="alert alert-success mt-3"
          role="status"
          data-test-id="import-result"
        >
          <template
            v-for="worksheet in result.worksheets ?? []"
            :key="worksheet.worksheetName"
          >
            {{ worksheet.worksheetName }}: {{ worksheet.created ?? 0 }} created,
            {{ worksheet.skipped ?? 0 }} skipped, {{ worksheet.updated ?? 0 }} updated,
            {{ worksheet.failed ?? 0 }} failed.
          </template>
        </div>

        <button
          type="button"
          class="btn btn-primary mt-2"
          :disabled="!canImport"
          data-test-id="import-payees-button"
          @click="openConfirm"
        >
          Import Payees
        </button>
      </div>
    </div>

    <ModalDialog
      :show="showConfirm"
      title="Import Payees"
      :loading="importing"
      :primary-button-text="importing ? 'Importing...' : 'Import Payees'"
      primary-button-test-id="import-payees-confirm-button"
      secondary-button-test-id="import-payees-cancel-button"
      test-id="import-payees-confirm-modal"
      @primary="importPayees"
      @update:show="showConfirm = $event"
    >
      <ErrorDisplay
        v-model:show="showConfirmError"
        :problem="confirmError"
        class="mb-3"
      />
      <p>
        This will add
        <strong>{{ payeeWorksheet?.validRecordCount ?? 0 }}</strong>
        payee matching rules to
        <strong>{{ workspace.name }}</strong
        >. Existing rules will not be changed.
      </p>
      <p class="text-muted mb-0">
        Importing this workbook again creates duplicate rules. Confirm only if you want to add these
        rules now.
      </p>
    </ModalDialog>
  </section>
</template>
