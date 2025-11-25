<script setup lang="ts">
defineProps<{
  show: boolean
  title?: string
  details?: string
  more?: string
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
}>()

const close = () => {
  emit('update:show', false)
}
</script>
<template>
  <div
    v-if="show"
    class="alert alert-danger alert-dismissible fade show"
    role="alert"
    data-test-id="error-display"
  >
    <strong>{{ title || 'Please fix the following errors:' }}</strong
    ><br />
    <span>
      {{ details }}
    </span>
    <button
      type="button"
      class="btn-close"
      aria-label="Close"
      @click="close"
    ></button>
  </div>
</template>
