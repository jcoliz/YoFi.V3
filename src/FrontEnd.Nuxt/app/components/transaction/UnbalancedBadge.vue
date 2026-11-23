<script setup lang="ts">
/**
 * Renders the compact server-derived unbalanced indicator used by transaction list rows.
 * It accepts no amounts and emits no events.
 */
</script>

<template>
  <span
    class="badge text-bg-danger"
    title="The split amounts do not match the transaction amount."
    data-test-id="transaction-unbalanced-badge"
  >
    <FeatherIcon
      icon="alert-triangle"
      size="12"
      class="me-1"
    />
    Unbalanced
  </span>
</template>
