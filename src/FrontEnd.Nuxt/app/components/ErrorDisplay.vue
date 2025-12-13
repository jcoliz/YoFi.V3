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

const additionalFields = computed(() => {
  if (!props.problem) return undefined

  const standardFields = ['type', 'title', 'status', 'detail', 'instance']
  const entries = Object.entries(props.problem).filter(([key]) => !standardFields.includes(key))

  return entries.length > 0 ? Object.fromEntries(entries) : undefined
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
    <span data-test-id="detail-display">
      {{ problem?.detail }}
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
        <p class="mb-2 text-muted">
          Error details (provide this information if contacting support):
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
