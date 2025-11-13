<script setup lang="ts">
import { ref, onMounted } from "vue"
import * as api from "../utils/apiclient"

useHead({
  title: 'Weather',
});

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
 */
const { baseUrl } = useApiBaseUrl();
const client = new api.WeatherClient(baseUrl)

/**
 * Get items from the server
 */
 async function getData() {
  forecasts.value = undefined
  isLoading.value = true

  client.getWeatherForecasts()
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
        <h1>Weather</h1>

        <p>This component demonstrates showing data loaded from a backend API service.</p>

        <p v-if="isLoading"><em>Loading...</em></p>
        <table v-else class="table">
            <thead>
            <tr>
                <th>Date</th>
                <th>Temp.</th>
                <th>Summary</th>
            </tr>
            </thead>
            <tbody data-test-id="forecast-table-body">
            <tr v-for="forecast in forecasts" :key="forecast.id">
                <td>{{ forecast.date?.toLocaleDateString() }} {{ forecast.date?.toLocaleTimeString() }}</td>
                <td>{{ forecast.temperatureC }}°C / {{ forecast.temperatureF }}°F</td>
                <td>{{ forecast.summary }}</td>
            </tr>
            </tbody>
        </table>
    </main>

</template>
