<script setup lang="ts">
import type { FeatherIconNames } from 'feather-icons'

/**
 * Single link on a pagination bar
 */

const props = defineProps<{
  /**
   * Which page to link to
   */
  page: number

  /**
   * What to display instead of the page (optional)
   *
   * Else will just display the page number
   */
  icon?: FeatherIconNames

  /**
   * Current page (optional)
   *
   * If no current page is set, link will never be active
   */
  current?: number

  /**
   * Test ID for the underlying link (optional)
   *
   * Else will calculate the test id based on current page
   */
  dataTestId?: string
}>()

const emit = defineEmits<{
  /**
   * User has selected a new page
   */
  pageUpdated: [page: number]
}>()

const testId = computed(() => {
  if (props.dataTestId) {
    return props.dataTestId
  }
  if (!props.current) {
    return undefined
  }
  if (props.current == props.page) {
    return 'page-current'
  }
  if (props.current - 1 == props.page) {
    return 'page-previous'
  }
  if (props.current + 1 == props.page) {
    return 'page-next'
  }
  return undefined
})
</script>

<template>
  <li
    class="page-item"
    :class="{ active: page === current }"
  >
    <button
      class="page-link"
      :data-test-id="testId"
      @click="emit('pageUpdated', page)"
    >
      <FeatherIcon
        v-if="icon"
        :icon="icon"
        size="1.2em"
      />
      <span v-else>{{ page }}</span>
    </button>
  </li>
</template>

<style scoped>
:deep(.feather) {
  padding-bottom: 3px;
}
</style>
