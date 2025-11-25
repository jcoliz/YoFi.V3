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

const more = computed(() => {
  const traceId = props.problem?.instance
  const status = props.problem?.status

  if (status !== undefined && status >= 500 && traceId) {
    return traceId
  }
  return undefined
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
    <strong>{{ problem?.title || 'Please fix the following errors:' }}</strong
    ><br />
    <span>
      {{ problem?.detail }}
    </span>
    <div
      v-if="more"
      class="mt-2"
    >
      <a
        href="#"
        class="small text-danger text-decoration-none"
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
        class="mt-2 small text-muted"
        style="white-space: pre-wrap; word-break: break-word"
      >
        {{ more }}
      </div>
    </div>
    <button
      type="button"
      class="btn-close"
      aria-label="Close"
      @click="close"
    ></button>
  </div>
</template>
