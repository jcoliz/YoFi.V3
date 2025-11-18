<script setup lang="ts">
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
    <dl class="version-list">
        <div class="version-item">
            <dt><strong>Front End Version</strong><br/><small>runtimeConfig.public.solutionVersion</small></dt>
            <dd>{{ frontEndVersion }}</dd>
        </div>
        <div class="version-item">
            <dt><strong>Back End Version</strong><br/><small>/api/version</small></dt>
            <dd>{{ version }}</dd>
        </div>
        <div class="version-item">
            <dt><strong>API Base URL</strong></dt>
            <dd>{{ baseUrl }}</dd>
        </div>
    </dl>
</template>

<style scoped>
.version-list {
    display: grid;
    gap: 1rem;
}

.version-item {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 2rem;
}

.version-item dt {
    margin: 0;
}

.version-item dd {
    margin: 0;
}
</style>
