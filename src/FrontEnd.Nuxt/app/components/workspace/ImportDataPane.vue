<script setup lang="ts">
/**
 * Previews and confirms automatic tenant-data imports from original or native YoFi workbooks.
 */
import {
  TenantDataAdminClient,
  type IProblemDetails,
  type TenantDataImportPreviewDto,
  type TenantDataImportResultDto,
  type TenantRoleResultDto,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'
import { useToastStore } from '~/stores/toast'

const props = defineProps<{
  workspace: TenantRoleResultDto
}>()

const emit = defineEmits<{
  imported: [result: TenantDataImportResultDto]
  'update:busy': [value: boolean]
}>()

const tenantDataAdminClient = useApiClient(TenantDataAdminClient)
const toastStore = useToastStore()
const selectedFile = ref<File>()
const preview = ref<TenantDataImportPreviewDto>()
const previewing = ref(false)
const importing = ref(false)
const showConfirm = ref(false)
const isDragging = ref(false)
const error = ref<IProblemDetails>()
const showError = ref(false)
const confirmError = ref<IProblemDetails>()
const showConfirmError = ref(false)

const busy = computed(() => previewing.value || importing.value)
const worksheets = computed(() => preview.value?.worksheets ?? [])
const formatLabel = computed(() => {
  switch (preview.value?.formatId) {
    case 'yofi.v3':
      return 'YoFi.V3'
    case 'original-yofi':
      return 'Original YoFi'
    default:
      return 'Unknown format'
  }
})
const isNative = computed(() => preview.value?.formatId === 'yofi.v3')
const hasNativeSplit = computed(() =>
  worksheets.value.some((worksheet) => worksheet.dataType === 'Splits'),
)
const hasNativeTransaction = computed(() =>
  worksheets.value.some((worksheet) => worksheet.dataType === 'Transactions'),
)
const issues = computed(() =>
  worksheets.value.flatMap((worksheet) =>
    [...(worksheet.errors ?? []), ...(worksheet.skippedRows ?? [])].map((issue) => ({
      worksheetName: worksheet.worksheetName ?? 'Unknown worksheet',
      issue,
    })),
  ),
)
const canImport = computed(
  () =>
    !!selectedFile.value &&
    !!preview.value?.sha256 &&
    preview.value.canImport === true &&
    worksheets.value.length > 0 &&
    (!hasNativeSplit.value || (isNative.value && hasNativeTransaction.value)) &&
    !busy.value,
)

function worksheetTestId(worksheet: { dataType?: string }): string {
  const slug = (worksheet.dataType ?? 'worksheet')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
  return `import-worksheet-preview-${slug}`
}

function worksheetLabel(worksheet: { dataType?: string; worksheetName?: string }): string {
  if (worksheet.dataType === 'PayeeMatchingRules') return 'Payee matching rules'
  if (worksheet.dataType === 'Transactions') return 'Transactions'
  if (worksheet.dataType === 'Splits') return 'Splits'
  return worksheet.worksheetName ?? 'Worksheet'
}

function payeeWarning(worksheet: { dataType?: string }): string {
  if (worksheet.dataType !== 'PayeeMatchingRules') return ''
  return isNative.value
    ? 'Native payee matching rules use matching Keys: existing matching Keys are updated, blank or unknown Keys create rules, and omitted rules remain unchanged.'
    : 'Original payees are added as create-only rules. Importing this workbook again creates duplicate rules; existing rules are not changed.'
}

function showProblem(problem: IProblemDetails) {
  error.value = problem
  showError.value = true
}

function selectFile(file?: File) {
  if (busy.value) return

  preview.value = undefined
  showConfirm.value = false
  error.value = undefined
  showError.value = false
  confirmError.value = undefined
  showConfirmError.value = false

  if (file && !file.name.toLowerCase().endsWith('.xlsx')) {
    selectedFile.value = undefined
    showProblem({
      title: 'Unsupported file',
      detail: 'Choose a YoFi .xlsx workbook.',
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

function formatImportResult(result: TenantDataImportResultDto): string {
  return (result.worksheets ?? [])
    .map((worksheet) => {
      const countEntries = (
        [
          ['created', worksheet.created],
          ['skipped', worksheet.skipped],
          ['updated', worksheet.updated],
          ['failed', worksheet.failed],
        ] as const
      ).filter(([, count]) => (count ?? 0) > 0)
      const counts = countEntries.map(([label, count]) => `${count} ${label}`)

      return counts.length > 0 ? `${worksheet.worksheetName}: ${counts.join(', ')}` : undefined
    })
    .filter((summary): summary is string => !!summary)
    .join('. ')
}

async function importData() {
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
    showConfirm.value = false
    const message = formatImportResult(importResult)
    toastStore.success(message || 'Workbook imported', 'Import complete', 'import-result')
    emit('imported', importResult)
  } catch (err) {
    const problem = handleApiError(err, 'Import Failed', 'Failed to import workbook')
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
    ...issues.value.map(({ worksheetName, issue }) => [
      worksheetName,
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
  link.download = 'import-errors.csv'
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
        subtitle="Import supported data from an original or native YoFi workbook."
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
          Choose a YoFi .xlsx workbook, or drop it here.
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
        <p>
          <strong>Detected format:</strong>
          <span data-test-id="import-format">{{ formatLabel }}</span>
        </p>

        <div
          v-if="worksheets.length"
          class="mb-3"
          data-test-id="import-worksheet-preview-list"
        >
          <section
            v-for="worksheet in worksheets"
            :key="`${worksheet.worksheetName}-${worksheet.dataType}`"
            class="border rounded p-2 mb-2"
            :data-test-id="worksheetTestId(worksheet)"
          >
            <strong>{{ worksheet.worksheetName }}</strong>
            <span class="text-muted"> ({{ worksheetLabel(worksheet) }})</span>
            <p class="mb-1">
              {{ worksheet.validRecordCount ?? 0 }} valid,
              {{ worksheet.skippedRecordCount ?? 0 }} skipped,
              {{ worksheet.errors?.length ?? 0 }} errors.
            </p>
            <p
              v-if="payeeWarning(worksheet)"
              class="small mb-1"
            >
              {{ payeeWarning(worksheet) }}
            </p>
            <HelpText v-if="worksheet.dataType === 'Splits'">
              Native and original split previews are applied with their transaction worksheet.
            </HelpText>
            <p
              v-if="worksheet.ignoredColumns?.length"
              class="small text-muted mb-0"
              data-test-id="import-ignored-columns"
            >
              Ignored columns: {{ worksheet.ignoredColumns.join(', ') }}.
            </p>
          </section>
        </div>
        <p
          v-else
          class="text-warning"
        >
          No supported worksheet was found. This workbook cannot be imported.
        </p>

        <div
          v-if="issues.length"
          class="mb-3"
          data-test-id="import-row-issues"
        >
          <h6>Rows needing attention</h6>
          <ul class="small ps-3 mb-2">
            <li
              v-for="(entry, index) in issues"
              :key="`${entry.worksheetName}-${index}`"
            >
              {{ entry.worksheetName }}: Row {{ entry.issue.rowNumber
              }}<span v-if="entry.issue.field">, {{ entry.issue.field }}</span
              >: {{ entry.issue.message }}
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

        <p
          v-if="preview.ignoredWorksheets?.length"
          class="small text-muted"
          data-test-id="import-ignored-worksheets"
        >
          Other worksheets will not be imported: {{ preview.ignoredWorksheets.join(', ') }}.
        </p>

        <button
          type="button"
          class="btn btn-primary mt-2"
          :disabled="!canImport"
          data-test-id="import-data-button"
          @click="openConfirm"
        >
          Import Data
        </button>
      </div>
    </div>

    <ModalDialog
      :show="showConfirm"
      title="Confirm Data Import"
      :loading="importing"
      :primary-button-text="importing ? 'Importing...' : 'Import Data'"
      primary-button-test-id="import-data-confirm-button"
      secondary-button-test-id="import-data-cancel-button"
      test-id="import-data-confirm-modal"
      @primary="importData"
      @update:show="showConfirm = $event"
    >
      <ErrorDisplay
        v-model:show="showConfirmError"
        :problem="confirmError"
        class="mb-3"
      />
      <p>
        Import the supported worksheets from <strong>{{ selectedFile?.name }}</strong> into
        <strong>{{ workspace.name }}</strong>.
      </p>
      <ul>
        <li
          v-for="worksheet in worksheets"
          :key="worksheet.worksheetName"
        >
          {{ worksheet.worksheetName }}: {{ worksheet.validRecordCount ?? 0 }} records will be
          considered, {{ worksheet.skippedRecordCount ?? 0 }} skipped.
        </li>
      </ul>
      <p
        v-if="isNative"
        class="text-muted mb-0"
      >
        Native payee and transaction matching Keys will be updated when present; missing or unknown
        Keys create new records. Omitted native payee and rule records remain unchanged.
      </p>
      <p
        v-else
        class="text-muted mb-0"
      >
        Original payee rules are create-only and may create duplicates. Original transactions are
        skipped when their legacy external reference already exists.
      </p>
    </ModalDialog>
  </section>
</template>
