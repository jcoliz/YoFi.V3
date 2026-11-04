<script setup lang="ts">
defineProps<{
  title: string
  subtitle?: string
}>()
</script>

<template>
  <div>
    <h5
      class="card-title"
      :class="{ 'mb-0': !subtitle }"
    >
      {{ title }}
    </h5>
    <p
      v-if="subtitle"
      class="text-muted mb-0"
    >
      {{ subtitle }}
    </p>
  </div>
</template>
