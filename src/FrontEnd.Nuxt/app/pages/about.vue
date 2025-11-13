<script setup lang="ts">
import versionText from './assets/version.txt?raw';
import * as api from "../utils/apiclient"

useHead({
  title: 'About',
});

/**
 * Client for communicating with server
 */
const { baseUrl } = useApiBaseUrl();
const client = new api.VersionClient(baseUrl)

/**
 * Version data to display
 */

const version = ref<string>("Loading...")

/**
 * When mounted, get the view data from server
 */
 onMounted(() => {
  client.getVersion()
    .then((result) => {
      version.value = result
    })
})

</script>
<template>
    <h1>About</h1>
    <table>
        <tr>
            <td><strong class="me-2">Front End Version</strong></td>
            <td>{{ versionText }}</td>
        </tr>
        <tr>
            <td><strong class="me-2">Back End Version</strong></td>
            <td>{{ version }}</td>
        </tr>
        <tr>
            <td><strong class="me-2">API Base URL</strong></td>
            <td>{{ baseUrl }}</td>
        </tr>
    </table>
</template>
