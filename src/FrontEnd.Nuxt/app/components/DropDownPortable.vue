<script setup lang="ts">
/**
 * Portable drop-down, suitable for server or client rendering
 *
 * Bootstrap dropdowns only work on client rendering. So to render
 * then on server, we show only the trigger, but do not set up the
 * interactive logic.
 */
</script>
<template>
  <div>
    <ClientOnly>
      <DropDownClientOnly>
        <template #trigger>
          <slot name="trigger" />
        </template>
        <template #default>
          <slot />
        </template>
      </DropDownClientOnly>
      <template #fallback>
        <div>
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
