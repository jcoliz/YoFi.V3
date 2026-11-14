<script setup lang="ts">
/**
 * Bootstrap drop-down
 *
 * This component can only be used client-side. Be sure to wrap it in
 * <ClientOnly>
 *
 * Note: We cannot use slot VNode `.el` to find the trigger element, because
 * calling a slot function in script returns detached VNodes (never mounted by
 * Vue's renderer), so `.el` is always null. Instead we use a templateRef on
 * the wrapper div and access `firstElementChild` to reach the actual mounted
 * trigger button.
 */

import { Dropdown } from 'bootstrap'
import { ref, onMounted, onBeforeUnmount } from 'vue'

const props = defineProps<{
  /**
   * Uses Popper's fixed strategy so menus can escape scrollable table overflow.
   * Positioning is viewport-relative unless an ancestor establishes a fixed containing block
   * (for example, via a transform). Omitted or false preserves Bootstrap's default strategy.
   */
  fixed?: boolean
}>()

const { logger } = useDebugLog('DropDownClientOnly', { minimumLevel: 'info' })

const dropdown = ref<Dropdown>()
const triggerWrapperRef = ref<HTMLElement>()

onMounted(() => {
  // Wait for DOM to fully render
  nextTick(() => {
    // Use the templateRef on the wrapper div to reach the actual mounted
    // trigger element. Slot VNode `.el` is always null for detached VNodes
    // returned by calling a slot function in script.
    const el = triggerWrapperRef.value?.firstElementChild as HTMLElement | null

    if (!el) {
      logger.error('No trigger element found')
      return
    }

    if (!Dropdown.getInstance(el)) {
      dropdown.value = new Dropdown(
        el,
        props.fixed
          ? {
              popperConfig: { strategy: 'fixed' },
            }
          : {},
      )
      logger.debug('OK')
    }
  })
})

onBeforeUnmount(() => {
  dropdown.value?.dispose()
})
</script>
<template>
  <div
    ref="triggerWrapperRef"
    class="dropdown d-flex"
  >
    <slot name="trigger" />
    <slot />
  </div>
</template>
