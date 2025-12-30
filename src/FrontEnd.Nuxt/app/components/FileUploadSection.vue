<script setup lang="ts">
/**
 * FileUploadSection.vue
 *
 * Generic file upload component with "Choose Files" button and hidden file input.
 * Emits filesSelected event when user selects files.
 *
 * Props:
 * - disabled: boolean - Disables file selection
 * - accept: string - File types to accept (e.g., '.ofx,.qfx')
 * - multiple: boolean - Allow multiple file selection
 *
 * Events:
 * - @filesSelected(files: File[]) - Emitted when files are selected
 */

interface Props {
  disabled?: boolean
  accept?: string
  multiple?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
  accept: '*',
  multiple: false,
})

const emit = defineEmits<{
  filesSelected: [files: File[]]
}>()

const fileInput = ref<HTMLInputElement | null>(null)

/**
 * Triggers the hidden file input when user clicks "Browse..." button
 */
const triggerFileInput = () => {
  if (!props.disabled && fileInput.value) {
    fileInput.value.click()
  }
}

/**
 * Handles file selection from the input element
 */
const handleFileChange = (event: Event) => {
  const target = event.target as HTMLInputElement
  if (target.files && target.files.length > 0) {
    const filesArray = Array.from(target.files)
    emit('filesSelected', filesArray)

    // Reset input so the same file can be selected again
    target.value = ''
  }
}
</script>

<template>
  <div class="file-upload-section">
    <div class="d-flex align-items-center gap-3">
      <label class="mb-0 fw-semibold">Choose Files:</label>
      <button
        type="button"
        class="btn btn-outline-primary"
        data-test-id="browse-files-button"
        :disabled="disabled"
        @click="triggerFileInput"
      >
        Browse...
      </button>
    </div>

    <!-- Hidden file input -->
    <input
      ref="fileInput"
      type="file"
      class="d-none"
      :accept="accept"
      :multiple="multiple"
      :disabled="disabled"
      data-test-id="file-input"
      @change="handleFileChange"
    >
  </div>
</template>

<style scoped>
.file-upload-section {
  padding: 1rem 0;
}
</style>
