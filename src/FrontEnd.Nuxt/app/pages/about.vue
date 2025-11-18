<script setup lang="ts">
import versionText from './assets/version.txt?raw';
import * as api from "../utils/apiclient"
definePageMeta({
    title: 'About',
    order: 4
})

/**
 * Client for communicating with server
 */
const { baseUrl } = useApiBaseUrl();
const client = new api.VersionClient(baseUrl)

/**
 * Version data to display
 */

const version = ref<string>("Loading...")
const runtimeConfig = useRuntimeConfig()
const frontEndVersion = runtimeConfig.public.solutionVersion

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
    <table>
        <tr>
            <td><strong class="me-2">Version.Txt</strong></td>
            <td>{{ versionText }}</td>
        </tr>
        <tr>
            <td><strong class="me-2">Front End Version</strong></td>
            <td>{{ frontEndVersion }}</td>
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
