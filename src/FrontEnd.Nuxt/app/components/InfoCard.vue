<script setup lang="ts">
/**
 * InfoCard Component
 *
 * Reusable card component with header and body sections.
 * Provides consistent styling across pages while allowing customizable header sizes.
 */

interface Props {
  title?: string
  headerTag?: 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6'
}

withDefaults(defineProps<Props>(), {
  title: '',
  headerTag: 'h5',
})
</script>

<template>
  <div class="card">
    <div
      v-if="title || $slots.header"
      class="card-header"
    >
      <slot name="header">
        <component
          :is="headerTag"
          class="card-title mb-0"
        >
          {{ title }}
        </component>
      </slot>
    </div>
    <div class="card-body">
      <slot />
    </div>
  </div>
</template>
