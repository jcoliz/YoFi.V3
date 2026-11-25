<script setup lang="ts">
withDefaults(defineProps<{ disabled?: boolean }>(), { disabled: false })
</script>

<template>
  <button
    type="button"
    class="dropdown-item"
    :disabled="disabled"
    data-test-id="create-rule-button"
  >
    <FeatherIcon
      icon="zap"
      size="14"
      class="me-2"
    />
    Create Rule
  </button>
</template>
