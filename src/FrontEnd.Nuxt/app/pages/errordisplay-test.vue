<script setup lang="ts">
/**
 * Error Display Test Page
 *
 * Test page for demonstrating the ErrorDisplay component with various error types
 * from the backend TestControl API. This page is for development/testing purposes only.
 */

import { TestControlClient, type ErrorCodeInfo, type IProblemDetails } from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'

definePageMeta({
  title: 'Error Display Test',
  auth: false, // Allow anonymous access
  layout: 'default',
})

// API Client
const { baseUrl } = useApiBaseUrl()
const testControlClient = new TestControlClient(baseUrl)

// State
const errorTypes = ref<ErrorCodeInfo[]>([])
const selectedErrorCode = ref<string>('')
const loading = ref(false)
const loadingErrors = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)

// Load error types on mount
onMounted(async () => {
  await loadErrorTypes()
})

// Methods
async function loadErrorTypes() {
  loadingErrors.value = true
  error.value = undefined
  showError.value = false

  try {
    errorTypes.value = await testControlClient.listErrors()

    // Select first error by default if available
    if (errorTypes.value.length > 0 && errorTypes.value[0]?.code) {
      selectedErrorCode.value = errorTypes.value[0].code ?? ''
    }
  } catch (err) {
    error.value = handleApiError(err, 'Load Failed', 'Failed to load error types')
    showError.value = true
  } finally {
    loadingErrors.value = false
  }
}

async function triggerError() {
  if (!selectedErrorCode.value) {
    error.value = {
      title: 'Validation Error',
      detail: 'Please select an error type',
    }
    showError.value = true
    return
  }

  loading.value = true
  error.value = undefined
  showError.value = false

  try {
    await testControlClient.returnError(selectedErrorCode.value)

    // If we get here, no error was thrown (shouldn't happen)
    error.value = {
      title: 'Unexpected Success',
      detail: 'The endpoint was expected to return an error but succeeded instead',
    }
    showError.value = true
  } catch (err) {
    error.value = handleApiError(err, 'Error Triggered', 'An error was triggered as expected')
    showError.value = true
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="container py-4">
    <div class="row justify-content-center">
      <div class="col-lg-8">
        <div class="alert alert-info mb-4">
          <FeatherIcon
            icon="info"
            size="16"
            class="me-2"
          />
          This page demonstrates the ErrorDisplay component by triggering various error types from
          the backend.
        </div>

        <!-- Error Display -->
        <ErrorDisplay
          v-model:show="showError"
          :problem="error"
          class="mb-4"
        />

        <!-- Raw Problem Details Display -->
        <div
          v-if="error"
          class="card mb-4"
        >
          <div class="card-header">
            <h5 class="mb-0">Raw Problem Details</h5>
          </div>
          <div class="card-body">
            <pre
              class="mb-0"
              data-test-id="raw-problem-details"
            ><code>{{ JSON.stringify(error, null, 2) }}</code></pre>
          </div>
        </div>

        <!-- Test Controls -->
        <div class="card">
          <div class="card-header">
            <h5 class="mb-0">Test Controls</h5>
          </div>
          <div class="card-body">
            <!-- Loading State -->
            <div
              v-if="loadingErrors"
              class="text-center py-3"
            >
              <BaseSpinner />
              <div class="mt-2">
                <small class="text-muted">Loading error types...</small>
              </div>
            </div>

            <!-- Error Type Selection -->
            <div
              v-else
              class="mb-3"
            >
              <label
                for="errorType"
                class="form-label"
              >
                Error Type
              </label>
              <select
                id="errorType"
                v-model="selectedErrorCode"
                class="form-select"
                data-test-id="error-type-select"
                :disabled="loading || errorTypes.length === 0"
              >
                <option
                  value=""
                  disabled
                >
                  Select an error type
                </option>
                <option
                  v-for="errorType in errorTypes"
                  :key="errorType.code"
                  :value="errorType.code"
                >
                  {{ errorType.code }} - {{ errorType.description }}
                </option>
              </select>
              <div class="form-text">Select an error type to test from the available options</div>
            </div>

            <!-- Trigger Button -->
            <button
              class="btn btn-danger"
              data-test-id="trigger-error-button"
              :disabled="loading || loadingErrors || !selectedErrorCode"
              @click="triggerError"
            >
              <span
                v-if="loading"
                class="spinner-border spinner-border-sm me-2"
                role="status"
                aria-hidden="true"
              />
              {{ loading ? 'Triggering Error...' : 'Trigger Error' }}
            </button>

            <button
              class="btn btn-secondary ms-2"
              data-test-id="refresh-button"
              :disabled="loading || loadingErrors"
              @click="loadErrorTypes"
            >
              <FeatherIcon
                icon="refresh-cw"
                size="16"
                class="me-1"
              />
              Refresh List
            </button>
          </div>
        </div>

        <!-- Available Error Types -->
        <div
          v-if="errorTypes.length > 0 && !loadingErrors"
          class="card mt-4"
        >
          <div class="card-header">
            <h5 class="mb-0">Available Error Types</h5>
          </div>
          <div class="card-body">
            <div class="table-responsive">
              <table class="table table-sm">
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Description</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="errorType in errorTypes"
                    :key="errorType.code"
                  >
                    <td>
                      <code>{{ errorType.code }}</code>
                    </td>
                    <td>{{ errorType.description }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
code {
  background-color: #f8f9fa;
  padding: 0.2rem 0.4rem;
  border-radius: 0.25rem;
  font-size: 0.875em;
}
</style>
