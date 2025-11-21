<script setup lang="ts">
import { ref } from 'vue'
import type { ApplicationInsights } from '@microsoft/applicationinsights-web'

definePageMeta({
  title: 'Counter',
  order: 3,
  middleware: 'sidebase-auth',
})

const count = ref(0)

// Prototype Application Insights integration

const { $appInsights } = useNuxtApp()
const appInsights = $appInsights as ApplicationInsights

if (!appInsights) {
  console.warn('Counter Page: Application Insights not initialized')
}

const increaseCount = () => {
  count.value++

  if (appInsights) {
    appInsights.trackEvent({ name: 'CountIncreased', properties: { count: count.value } })
  }
}
</script>

<template>
  <p>
    <output>Current count: {{ count }}</output>
  </p>
  <button
    class="btn btn-primary"
    label="Click me"
    @click="increaseCount"
  >
    Click me
  </button>
</template>
