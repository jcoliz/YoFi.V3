<script setup lang="ts">
/**
 * Import Page (Temporary)
 *
 * Temporary page to test and demonstrate the FileUploadSection component.
 * This will be expanded with the full import workflow in future iterations.
 */

// Page metadata
definePageMeta({
  layout: 'default',
  auth: false, // Temporary: No auth required for testing
})

// State
const selectedFiles = ref<File[]>([])
const uploadInProgress = ref(false)

/**
 * Handles file selection from FileUploadSection component
 */
const handleFilesSelected = (files: File[]) => {
  console.log('Files selected:', files)
  selectedFiles.value = files

  // Display selected file names
  const fileNames = files.map((f) => f.name).join(', ')
  console.log('File names:', fileNames)
}

/**
 * Simulates uploading the selected files
 */
const uploadFiles = async () => {
  if (selectedFiles.value.length === 0) {
    return
  }

  uploadInProgress.value = true

  try {
    // Simulate upload delay
    await new Promise((resolve) => setTimeout(resolve, 1500))

    console.log('Upload complete!')
    alert(`Successfully selected ${selectedFiles.value.length} file(s) for upload`)

    // Clear selection after upload
    selectedFiles.value = []
  } finally {
    uploadInProgress.value = false
  }
}
</script>

<template>
  <div class="container py-4">
    <div class="row">
      <div class="col-lg-8 mx-auto">
        <h1
          class="mb-4"
          data-test-id="page-heading"
        >
          Import Bank Transactions
        </h1>

        <div class="card">
          <div class="card-body">
            <h5 class="card-title">Upload Files</h5>
            <p class="text-muted mb-4">
              Select OFX or QFX files from your bank to import transactions.
            </p>

            <!-- File Upload Section Component -->
            <FileUploadSection
              :disabled="uploadInProgress"
              accept=".ofx,.qfx"
              :multiple="true"
              @files-selected="handleFilesSelected"
            />

            <!-- Selected Files Display -->
            <div
              v-if="selectedFiles.length > 0"
              class="mt-3"
            >
              <h6>Selected Files:</h6>
              <ul class="list-group">
                <li
                  v-for="(file, index) in selectedFiles"
                  :key="index"
                  class="list-group-item"
                >
                  <strong>{{ file.name }}</strong>
                  <span class="text-muted ms-2">({{ Math.round(file.size / 1024) }} KB)</span>
                </li>
              </ul>

              <!-- Upload Button -->
              <button
                type="button"
                class="btn btn-primary mt-3"
                data-test-id="upload-button"
                :disabled="uploadInProgress"
                @click="uploadFiles"
              >
                <BaseSpinner
                  v-if="uploadInProgress"
                  size="sm"
                  class="me-1"
                />
                {{ uploadInProgress ? 'Uploading...' : 'Upload Files' }}
              </button>
            </div>

            <!-- Empty State -->
            <div
              v-else
              class="alert alert-info mt-3"
            >
              <strong>No files selected.</strong> Click "Browse..." to select files.
            </div>
          </div>
        </div>

        <!-- Temporary Notice -->
        <div class="alert alert-warning mt-4">
          <strong>⚠️ Temporary Page</strong><br />
          This is a temporary implementation to demonstrate the FileUploadSection component. The
          full import workflow (transaction review, duplicate detection, etc.) will be added in
          future iterations.
        </div>
      </div>
    </div>
  </div>
</template>
