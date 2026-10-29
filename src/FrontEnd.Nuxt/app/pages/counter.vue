<script setup lang="ts">
/**
 * Counter Page
 *
 * Simple interactive counter demonstrating client-side state management
 * and Application Insights telemetry integration. Each button click
 * increments the counter and logs a custom event to Azure Application Insights.
 */

import { ref } from 'vue'
import type { ApplicationInsights } from '@microsoft/applicationinsights-web'

definePageMeta({
  title: 'Counter',
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
/**
 * This page requires an authenticated user. If not authenticated, redirect to login page.
 * Only after we've confirmed authentication, do we call getData() to load the data for this page.
 */
useAuthenticationGuard().requireAuthenticatedUser()
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
