<script setup lang="ts">
/**
 * Upload dialog for validated XLSX transaction-split workbooks.
 *
 * It owns file selection and workbook-only validation, delegates uploads and
 * template downloads to the generated API clients, and reports committed data
 * to the transaction detail page.
 */
import {
  TransactionSplitTemplateClient,
  TransactionsClient,
  type FileParameter,
  type IProblemDetails,
  type TransactionDetailDto,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'

const props = defineProps<{
  /** Controls dialog visibility through v-model:show. */
  show: boolean
  /** Workspace that owns the upload target. */
  tenantKey: string
  /** Transaction that receives uploaded splits. */
  transactionKey: string
}>()

const emit = defineEmits<{
  /** Requests dialog visibility changes. */
  'update:show': [value: boolean]
  /** Reports whether upload or template download work is active. */
  'update:busy': [value: boolean]
  /** Supplies the server-committed transaction after a successful upload. */
  uploaded: [transaction: TransactionDetailDto]
}>()

const transactionsClient = useApiClient(TransactionsClient)
const templateClient = useApiClient(TransactionSplitTemplateClient)
const selectedFile = ref<File | null>(null)
const uploadActive = ref(false)
const templateActive = ref(false)
const error = ref<IProblemDetails | undefined>()
const showError = ref(false)
let active = true
let generation = 0
const operationActive = computed(() => uploadActive.value || templateActive.value)

watch(
  () => props.show,
  (show) => {
    generation++
    if (show) {
      selectedFile.value = null
      error.value = undefined
      showError.value = false
    }
  },
  { immediate: true },
)

watch(operationActive, (activeNow) => {
  emit('update:busy', activeNow)
})

function setShow(show: boolean) {
  if (!operationActive.value) emit('update:show', show)
}

function handleKeydown(event: KeyboardEvent) {
  if (!props.show || event.defaultPrevented || event.isComposing || event.key !== 'Escape') return
  event.preventDefault()
  event.stopPropagation()
  if (!event.repeat) setShow(false)
}

function validateSelectedFile(file: File): IProblemDetails | undefined {
  if (!/\.xlsx$/i.test(file.name)) {
    return {
      title: 'Unsupported workbook',
      detail: 'Select a file with an .xlsx filename.',
    }
  }
  if (file.size > 1024 * 1024) {
    return {
      title: 'Unsupported workbook',
      detail: 'The workbook must not exceed 1 MB.',
    }
  }
  return undefined
}

function clearSelection() {
  selectedFile.value = null
  error.value = undefined
  showError.value = false
}

function handleFileChange(event: Event) {
  if (operationActive.value) return
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  if (!file) {
    clearSelection()
    return
  }

  const validation = validateSelectedFile(file)
  if (validation) {
    selectedFile.value = null
    input.value = ''
    error.value = validation
    showError.value = true
    return
  }

  selectedFile.value = file
  error.value = undefined
  showError.value = false
}

async function downloadTemplate() {
  if (operationActive.value) return
  error.value = undefined
  showError.value = false
  templateActive.value = true
  const requestGeneration = generation

  try {
    const response = await templateClient.getSplitUploadTemplate()
    if (!active || generation !== requestGeneration) return
    const url = URL.createObjectURL(response.data)
    const link = document.createElement('a')
    link.href = url
    link.download = response.fileName || 'yofi-transaction-splits-template.xlsx'
    link.click()
    URL.revokeObjectURL(url)
  } catch (err) {
    if (active && generation === requestGeneration) {
      error.value = handleApiError(
        err,
        'Template Download Failed',
        'Failed to download the split workbook template',
      )
      showError.value = true
    }
  } finally {
    if (active && generation === requestGeneration) templateActive.value = false
  }
}

async function upload() {
  if (operationActive.value || !selectedFile.value) return
  error.value = undefined
  showError.value = false

  if (!props.tenantKey || !props.transactionKey) {
    error.value = {
      title: 'Upload unavailable',
      detail: 'Select a workspace and transaction before uploading.',
    }
    showError.value = true
    return
  }

  const validation = validateSelectedFile(selectedFile.value)
  if (validation) {
    error.value = validation
    showError.value = true
    return
  }

  const file = selectedFile.value
  const tenantKey = props.tenantKey
  const transactionKey = props.transactionKey
  const requestGeneration = generation
  uploadActive.value = true

  try {
    const parameter: FileParameter = { data: file, fileName: file.name }
    const updated = await transactionsClient.uploadSplits(transactionKey, tenantKey, parameter)
    if (
      !active ||
      generation !== requestGeneration ||
      props.tenantKey !== tenantKey ||
      props.transactionKey !== transactionKey
    )
      return
    emit('uploaded', updated)
    emit('update:show', false)
  } catch (err) {
    if (active && generation === requestGeneration) {
      error.value = handleApiError(
        err,
        'Split Upload Failed',
        'Failed to upload transaction splits',
      )
      showError.value = true
    }
  } finally {
    if (active && generation === requestGeneration) uploadActive.value = false
  }
}

onMounted(() => document.addEventListener('keydown', handleKeydown))
onBeforeUnmount(() => {
  active = false
  document.removeEventListener('keydown', handleKeydown)
  if (operationActive.value) emit('update:busy', false)
})
</script>

<template>
  <ModalDialog
    :show="show"
    title="Upload splits"
    :loading="operationActive"
    :primary-button-disabled="!selectedFile"
    primary-button-text="Upload"
    primary-button-test-id="upload-splits-submit-button"
    secondary-button-test-id="upload-splits-cancel-button"
    test-id="upload-splits-dialog"
    @update:show="setShow"
    @primary="upload"
  >
    <div data-test-id="upload-splits-error">
      <ErrorDisplay
        v-model:show="showError"
        :problem="error"
        class="mb-3"
      />
    </div>

    <fieldset :disabled="operationActive">
      <div class="mb-3">
        <label
          for="upload-splits-file"
          class="form-label"
        >
          XLSX workbook
        </label>
        <input
          id="upload-splits-file"
          type="file"
          accept=".xlsx"
          class="form-control"
          data-test-id="upload-splits-file"
          @change="handleFileChange"
        />
      </div>

      <div
        v-if="selectedFile"
        class="mb-3"
        data-test-id="upload-splits-selected-file"
      >
        {{ selectedFile.name }}
      </div>

      <p
        class="text-muted small mb-3"
        data-test-id="upload-splits-help"
      >
        Use Category, Amount, and optional Memo headers. Maximum size: 1 MB.
      </p>

      <button
        type="button"
        class="btn btn-outline-primary"
        data-test-id="download-splits-template-button"
        :disabled="operationActive"
        @click="downloadTemplate"
      >
        <BaseSpinner
          v-if="templateActive"
          size="sm"
          class="me-1"
        />
        Download template
      </button>
    </fieldset>

    <div
      v-if="operationActive"
      class="mt-3"
      data-test-id="upload-splits-pending"
    >
      <BaseSpinner
        size="sm"
        class="me-2"
      />
      {{ uploadActive ? 'Uploading...' : 'Downloading...' }}
    </div>
  </ModalDialog>
</template>
