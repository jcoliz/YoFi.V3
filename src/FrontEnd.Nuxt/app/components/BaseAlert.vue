<script setup lang="ts">
import type { FeatherIconNames } from 'feather-icons'

/**
 * Base Alert Component
 *
 * Displays a Bootstrap alert with a contextual feather icon.
 * Maps alert variants to appropriate icons automatically,
 * or accepts a custom icon override.
 */

const props = withDefaults(
  defineProps<{
    /**
     * Bootstrap alert variant: 'warning', 'danger', 'info', 'success'
     */
    variant?: 'warning' | 'danger' | 'info' | 'success'
    /**
     * Optional icon override. If not provided, an icon is chosen based on variant.
     */
    icon?: FeatherIconNames
  }>(),
  {
    variant: 'danger',
    icon: undefined,
  },
)

/**
 * Map alert variants to appropriate feather icons
 */
const defaultIcons: Record<string, FeatherIconNames> = {
  danger: 'alert-triangle',
  warning: 'alert-circle',
  info: 'info',
  success: 'check-circle',
}

const resolvedIcon = computed(() => props.icon ?? defaultIcons[props.variant])

const slots = useSlots()
</script>

<template>
  <div
    :class="['alert', `alert-${variant}`, 'd-flex', 'align-items-center']"
    role="alert"
  >
    <FeatherIcon
      class="flex-shrink-0 me-2"
      :icon="resolvedIcon"
      size="1.25em"
    />
    <div class="my-0">
      <strong data-test-id="title"><slot /></strong>
      <p
        v-if="slots.detail"
        class="my-0"
      >
        <slot name="detail" />
      </p>
    </div>
  </div>
</template>

<style scoped>
p {
  overflow-wrap: anywhere;
}
</style>
