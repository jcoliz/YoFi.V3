<script setup lang="ts">
/**
 * Error Display Component
 *
 * Displays error messages in a Bootstrap alert with optional expandable details.
 * Supports RFC 7807 Problem Details format and shows trace IDs for server errors.
 */

import { ref, computed } from 'vue'
import type { IProblemDetails } from '~/utils/apiclient'

/**
 * Component props
 * @property {boolean} show - Controls visibility of the error alert
 * @property {IProblemDetails} [problem] - Error details in RFC 7807 Problem Details format
 */
const props = defineProps<{
  show: boolean
  problem?: IProblemDetails
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
}>()

const showMore = ref(false)

const isServerError = computed(() => {
  const status = props.problem?.status
  return status !== undefined && status >= 500
})

const additionalFields = computed(() => {
  if (!props.problem) return undefined

  const standardFields = ['type', 'title', 'status', 'detail', 'instance']
  const entries = Object.entries(props.problem).filter(([key]) => !standardFields.includes(key))

  return entries.length > 0 ? Object.fromEntries(entries) : undefined
})

// Auto-expand for server errors when there are additional fields
// Reset when switching from server error to non-server error
watch(
  () => [props.problem, additionalFields.value, isServerError.value],
  () => {
    if (isServerError.value && additionalFields.value) {
      showMore.value = true
    } else if (!isServerError.value) {
      showMore.value = false
    }
  },
  { immediate: true },
)

const friendlyDetail = computed(() => {
  // If detail is provided, use it
  if (props.problem?.detail) {
    return props.problem.detail
  }

  // Otherwise, provide friendly message based on status code
  const status = props.problem?.status
  if (!status) return undefined

  const friendlyMessages: Record<number, string> = {
    400: 'Please check the information you provided and try again.',
    401: 'You need to be logged in to access this resource.',
    403: 'You do not have permission to access this resource.',
    404: 'The requested resource could not be found.',
    409: 'This operation conflicts with the current state of the resource.',
    500: 'An internal server error occurred. Please try again later.',
    502: 'The server received an invalid response from an upstream server.',
    503: 'The service is temporarily unavailable. Please try again later.',
  }

  return friendlyMessages[status] || 'An error occurred while processing your request.'
})

const close = () => {
  emit('update:show', false)
}

const toggleMore = () => {
  showMore.value = !showMore.value
}
</script>
<template>
  <div
    v-if="show"
    class="alert alert-danger alert-dismissible fade show"
    role="alert"
    data-test-id="error-display"
  >
    <strong data-test-id="title-display">{{
      problem?.title || 'Please fix the following errors:'
    }}</strong
    ><br />
    <span
      v-if="friendlyDetail"
      data-test-id="detail-display"
    >
      {{ friendlyDetail }}
    </span>
    <div
      v-if="additionalFields"
      class="mt-2"
    >
      <a
        href="#"
        class="small text-danger text-decoration-none"
        data-test-id="more-button"
        @click.prevent="toggleMore"
      >
        <FeatherIcon
          :icon="showMore ? 'chevron-up' : 'chevron-down'"
          size="16"
          class="me-1"
        />
        {{ showMore ? 'Hide details' : 'Show details' }}
      </a>
      <div
        v-if="showMore"
        class="mt-2 small"
        data-test-id="more-text"
      >
        <p class="mb-2">
          <strong v-if="isServerError"
            >Please contact support immediately so we can resolve this issue.</strong
          >
          <span
            v-else
            class="text-muted"
            >Error details (provide this information if contacting support):</span
          >
        </p>
        <div
          v-for="[key, value] in Object.entries(additionalFields)"
          :key="key"
          class="mb-1"
        >
          <strong>{{ key }}:</strong>
          <span class="ms-1">{{ typeof value === 'object' ? JSON.stringify(value) : value }}</span>
        </div>
      </div>
    </div>
    <button
      type="button"
      class="btn-close"
      aria-label="Close"
      data-test-id="close-button"
      @click="close"
    ></button>
  </div>
</template>
