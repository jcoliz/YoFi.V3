<script setup lang="ts">
/**
 * Copies a text value to the clipboard, reporting success and failure visibly.
 */
const props = withDefaults(
  defineProps<{
    value: string
    /** Lowercase description of the value, used in the button label and messages */
    label: string
    compact?: boolean
    testIdPrefix?: string
  }>(),
  { compact: false, testIdPrefix: 'copy' },
)

const copied = ref(false)
const copying = ref(false)
const copyError = ref('')
let resetTimer: ReturnType<typeof setTimeout> | undefined

function resetFeedback() {
  clearTimeout(resetTimer)
  copied.value = false
  copyError.value = ''
}

watch(() => props.value, resetFeedback)
onBeforeUnmount(() => clearTimeout(resetTimer))

async function copyValue() {
  const value = props.value
  resetFeedback()
  copying.value = true
  try {
    if (!navigator.clipboard?.writeText) {
      throw new Error('Clipboard access is unavailable')
    }
    await navigator.clipboard.writeText(value)
    if (value === props.value) {
      copied.value = true
      resetTimer = setTimeout(() => (copied.value = false), 2000)
    }
  } catch (error) {
    console.error(`Failed to copy ${props.label}:`, error)
    if (value === props.value) {
      copyError.value = `Could not copy ${props.label}. Check clipboard permissions and try again.`
    }
  } finally {
    copying.value = false
  }
}
</script>

<template>
  <span class="d-inline-flex flex-wrap align-items-center gap-1">
    <button
      type="button"
      class="btn btn-sm btn-link text-secondary text-decoration-none"
      :disabled="copying"
      :aria-label="`Copy ${label}`"
      :title="`Copy ${label}`"
      :data-test-id="`${testIdPrefix}-button`"
      @click.stop="copyValue"
    >
      <FeatherIcon
        :icon="copied ? 'check' : 'clipboard'"
        size="14"
        :class="{ 'me-1': !compact }"
        aria-hidden="true"
      />
      <span v-if="!compact">Copy {{ label }}</span>
    </button>
    <span
      role="status"
      class="small text-success"
      :data-test-id="`${testIdPrefix}-status`"
      >{{ copied ? 'Copied!' : '' }}</span
    >
    <span
      v-if="copyError"
      role="alert"
      class="small text-danger"
      :data-test-id="`${testIdPrefix}-error`"
      >{{ copyError }}</span
    >
  </span>
</template>
