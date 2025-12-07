<script setup lang="ts">
/**
 * Bootstrap drop-down
 *
 * This component can only be used client-side. Be sure to wrap it in
 * <ClientOnly>
 */

import { Dropdown } from 'bootstrap'
import { useSlots, ref, onMounted } from 'vue'

const slots = useSlots()

const toggleEl = ref<HTMLElement>()
const dropdown = ref<Dropdown>()

onMounted(() => {
  const tslot = slots.trigger

  if (!tslot) {
    console.warn('DropDownClientOnly: No trigger slot provided')
    return
  }

  // Wait for DOM to fully render
  nextTick(() => {
    const nodes = tslot()

    if (!nodes || nodes.length === 0) {
      console.error('DropDownClientOnly: Trigger slot is empty')
      return
    }

    const node = nodes[0]
    const el = node?.el as HTMLElement

    if (!el) {
      console.error('DropDownClientOnly: No trigger element found', node)
      return
    }
    toggleEl.value = el

    if (toggleEl.value && !Dropdown.getInstance(toggleEl.value)) {
      dropdown.value = new Dropdown(toggleEl.value)
      // TODO: This is never fired??
      console.log('dropdown element:', el)
    }
  })
})
</script>
<template>
  <div class="dropdown d-flex">
    <slot name="trigger" />
    <slot />
  </div>
</template>
