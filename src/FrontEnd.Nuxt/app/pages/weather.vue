<script setup lang="ts">
/**
 * Weather Page
 *
 * Displays weather forecast data fetched from the backend API.
 * Demonstrates authenticated API calls with automatic token refresh
 * and client-side data rendering with loading states.
 */

import { ref, onMounted } from 'vue'
import * as api from '../utils/apiclient'
import { useAuthFetch } from '../composables/useAuthFetch'
definePageMeta({
  title: 'Weather',
  auth: true,
})

/**
 * Forecast data to display
 */

const forecasts = ref<api.IWeatherForecast[]>()

/**
 * Whether we are loading data from the server presently
 */
const isLoading = ref(false)

/**
 * Client for communicating with server
 * Using auth-aware fetch to automatically handle token refresh
 */
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const client = new api.WeatherClient(baseUrl, authFetch)

/**
 * Get items from the server
 */
async function getData() {
  forecasts.value = undefined
  isLoading.value = true

  client
    .getWeatherForecasts()
    .then((result) => {
      forecasts.value = result
    })
    .finally(() => {
      isLoading.value = false
    })
}

/**
 * When mounted, get the view data from server
 */
onMounted(() => {
  getData()
})
</script>

<template>
  <main>
    <p>This component demonstrates showing data loaded from a backend API service.</p>

    <ClientOnly>
      <p v-if="isLoading"><em>Loading...</em></p>
      <table
        v-else
        class="table"
      >
        <thead>
          <tr>
            <th>Date</th>
            <th>Temp.</th>
            <th>Summary</th>
            <th>ID</th>
          </tr>
        </thead>
        <tbody data-test-id="forecast-table-body">
          <tr
            v-for="forecast in forecasts"
            :key="forecast.id"
          >
            <td>
              {{ forecast.date?.toLocaleDateString() }}
            </td>
            <td>{{ forecast.temperatureC }}°C / {{ forecast.temperatureF }}°F</td>
            <td>{{ forecast.summary }}</td>
            <td>{{ forecast.id }}</td>
          </tr>
        </tbody>
      </table>
      <template #fallback>
        <p><em>Please wait...</em></p>
      </template>
    </ClientOnly>
  </main>
</template>
