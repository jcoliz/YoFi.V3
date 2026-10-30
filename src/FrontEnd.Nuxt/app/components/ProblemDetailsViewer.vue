<script setup lang="ts">
/**
 * Displays the shared application problem and clears it when dismissed.
 * ErrorDisplay owns presentation for both global and local problems.
 */
const { problem, clearError } = useProblemDetails()

function updateShow(show: boolean) {
  if (!show) {
    clearError()
  }
}
</script>

<template>
  <ErrorDisplay
    :show="!!problem"
    :problem="problem"
    data-test-id="problem-details-viewer"
    @update:show="updateShow"
  />
</template>
