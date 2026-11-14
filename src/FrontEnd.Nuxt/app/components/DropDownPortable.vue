<script setup lang="ts">
/**
 * Portable drop-down, suitable for server or client rendering
 *
 * Bootstrap dropdowns only work on client rendering. So to render
 * then on server, we show only the trigger, but do not set up the
 * interactive logic.
 */
defineProps<{
  /**
   * Opts into Popper's fixed positioning strategy for menus inside scrollable containers.
   * Positions the menu relative to the viewport rather than a positioned ancestor, allowing
   * it to escape table overflow clipping when no ancestor establishes a fixed containing block.
   * Defaults to Bootstrap's normal positioning when omitted.
   */
  fixed?: boolean
}>()
</script>
<template>
  <div>
    <ClientOnly>
      <DropDownClientOnly :fixed="fixed">
        <template #trigger>
          <slot name="trigger" />
        </template>
        <template #default>
          <slot />
        </template>
      </DropDownClientOnly>
      <template #fallback>
        <div class="opacity-50">
          <slot name="trigger" />
        </div>
      </template>
    </ClientOnly>
  </div>
</template>
<style scoped>
:deep(a) {
  cursor: pointer;
}
</style>
