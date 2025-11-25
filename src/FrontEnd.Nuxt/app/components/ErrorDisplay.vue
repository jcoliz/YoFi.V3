<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  show: boolean
  title?: string
  details?: string
  code?: number
  traceId?: string
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
}>()

const showMore = ref(false)

const more = computed(() => {
  if (props.code !== undefined && props.code >= 500 && props.traceId) {
    return `Please contact support, and include this trace ID: ${props.traceId}`
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
    <strong>{{ title || 'Please fix the following errors:' }}</strong
    ><br />
    <span>
      {{ details }}
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
