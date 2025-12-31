<script setup lang="ts">
/**
 * ImportDuplicatesAlert.vue
 *
 * Bootstrap warning alert that displays when potential duplicates are detected in import review.
 * Shows informational message explaining what potential duplicates are.
 *
 * Props:
 * - show: boolean - Whether to display the alert (computed from hasPotentialDuplicates)
 *
 * Events: None (informational only, not dismissible)
 */

interface Props {
  show?: boolean
}

withDefaults(defineProps<Props>(), {
  show: false,
})
</script>

<template>
  <div
    v-if="show"
    class="alert alert-warning d-flex align-items-start"
    role="alert"
    data-test-id="duplicates-alert"
  >
    <span class="me-2 fs-5">⚠</span>
    <div>
      <strong>Note:</strong> Potential duplicates detected and highlighted. Transactions have the
      same identifier as another transaction, but differ in payee or amount.
    </div>
  </div>
</template>

<style scoped>
.alert {
  margin-bottom: 1rem;
}
</style>
