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
  if (tslot) {
    const nodes = tslot()
    const node = nodes[0]
    if (!node?.el) {
      console.error('DropDownClientOnly: No trigger element found')
      return
    }
    const el = node.el as HTMLElement
    toggleEl.value = el

    if (toggleEl.value && !Dropdown.getInstance(toggleEl.value)) {
      dropdown.value = new Dropdown(toggleEl.value)
      // TODO: This is never fired??
      console.log('dropdown element:', el)
    }
  }
})
</script>
<template>
  <div class="dropdown d-flex">
    <slot name="trigger" />
    <slot />
  </div>
</template>
