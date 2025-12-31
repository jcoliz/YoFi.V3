<script setup lang="ts">
/**
 * UploadStatusPane.vue
 *
 * Reusable dismissible status pane for displaying upload/processing messages.
 * Uses Bootstrap alert styling with dismiss functionality.
 *
 * Props:
 * - statusMessages: string[] - Array of status messages to display
 * - show: boolean - Controls visibility of the pane
 * - variant: string - Bootstrap alert variant (info, warning, success, danger)
 *
 * Events:
 * - @close - Emitted when user dismisses the alert
 */

interface Props {
  statusMessages?: string[]
  show?: boolean
  variant?: 'info' | 'warning' | 'success' | 'danger'
}

withDefaults(defineProps<Props>(), {
  statusMessages: () => [],
  show: false,
  variant: 'info',
})

const emit = defineEmits<{
  close: []
}>()

/**
 * Handles the dismiss button click
 */
const handleClose = () => {
  emit('close')
}
</script>

<template>
  <div
    v-if="show && statusMessages.length > 0"
    class="alert alert-dismissible fade show"
    :class="`alert-${variant}`"
    role="alert"
    data-test-id="upload-status-pane"
  >
    <div
      v-for="(message, index) in statusMessages"
      :key="index"
      class="mb-0"
      :class="{ 'mb-1': index < statusMessages.length - 1 }"
    >
      {{ message }}
    </div>
    <button
      type="button"
      class="btn-close"
      data-test-id="close-status-pane"
      aria-label="Close"
      @click="handleClose"
    />
  </div>
</template>

<style scoped>
.alert {
  position: relative;
}

/* Ensure proper spacing for multi-line messages */
.alert > div:not(:last-child) {
  margin-bottom: 0.5rem;
}
</style>
