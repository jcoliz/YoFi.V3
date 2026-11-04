<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    workspaceKey: string
    compact?: boolean
  }>(),
  { compact: false },
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

watch(() => props.workspaceKey, resetFeedback)
onBeforeUnmount(() => clearTimeout(resetTimer))

async function copyKey() {
  const key = props.workspaceKey
  resetFeedback()
  copying.value = true
  try {
    if (!navigator.clipboard?.writeText) {
      throw new Error('Clipboard access is unavailable')
    }
    await navigator.clipboard.writeText(key)
    if (key === props.workspaceKey) {
      copied.value = true
      resetTimer = setTimeout(() => (copied.value = false), 2000)
    }
  } catch (error) {
    console.error('Failed to copy workspace key:', error)
    if (key === props.workspaceKey) {
      copyError.value = 'Could not copy workspace key. Check clipboard permissions and try again.'
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
      aria-label="Copy workspace key"
      title="Copy workspace key"
      data-test-id="copy-workspace-key-button"
      @click.stop="copyKey"
    >
      <FeatherIcon
        :icon="copied ? 'check' : 'clipboard'"
        size="14"
        :class="{ 'me-1': !compact }"
        aria-hidden="true"
      />
      <span v-if="!compact">Copy workspace key</span>
    </button>
    <span
      role="status"
      class="small text-success"
      data-test-id="copy-workspace-key-status"
      >{{ copied ? 'Copied!' : '' }}</span
    >
    <span
      v-if="copyError"
      role="alert"
      class="small text-danger"
      data-test-id="copy-workspace-key-error"
      >{{ copyError }}</span
    >
  </span>
</template>
