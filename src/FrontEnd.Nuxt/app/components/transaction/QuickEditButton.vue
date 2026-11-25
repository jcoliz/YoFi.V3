<script setup lang="ts">
withDefaults(defineProps<{ disabled?: boolean }>(), { disabled: false })
</script>

<template>
  <button
    type="button"
    class="dropdown-item"
    :disabled="disabled"
    data-test-id="edit-transaction-button"
  >
    <FeatherIcon
      icon="edit"
      size="14"
      class="me-2"
    />
    Quick Edit
  </button>
</template>
