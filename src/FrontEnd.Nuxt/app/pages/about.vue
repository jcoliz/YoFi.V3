<script setup lang="ts">
/**
 * About Page
 *
 * Displays version information for both frontend and backend, along with
 * information about the application and its source repository.
 */

import * as api from '../utils/apiclient'
definePageMeta({
  title: 'About',
})

/**
 * Client for communicating with server
 */
const client = useApiClient(api.VersionClient)

/**
 * Version data to display
 */

const version = ref<string>('Loading...')
const runtimeConfig = useRuntimeConfig()
const frontEndVersion = runtimeConfig.public.solutionVersion

/**
 * When mounted, get the view data from server
 */
onMounted(() => {
  client.index().then(async (response) => {
    version.value = await response.data.text()
  })
})
</script>
<template>
  <div class="row">
    <div class="col-lg-8">
      <InfoCard
        title="Version Information"
        header-tag="h4"
      >
        <div class="row mb-3">
          <div class="col-sm-4">
            <strong>Front End Version</strong><br />
            <small class="text-muted">runtimeConfig.public.solutionVersion</small>
          </div>
          <div class="col-sm-8">
            {{ frontEndVersion }}
          </div>
        </div>
        <div class="row mb-3">
          <div class="col-sm-4">
            <strong>Back End Version</strong><br />
            <small class="text-muted">/api/version</small>
          </div>
          <div class="col-sm-8">
            {{ version }}
          </div>
        </div>
      </InfoCard>
    </div>

    <div class="col-lg-4">
      <InfoCard
        title="Application Info"
        header-tag="h5"
      >
        <p class="text-muted">
          <small
            >YoFi V3 is a personal finance management application built with Nuxt 4 and ASP.NET
            Core.</small
          >
        </p>
        <div class="d-grid gap-2 mt-3">
          <a
            href="https://github.com/jcoliz/YoFi.V3"
            target="_blank"
            rel="noopener noreferrer"
            class="btn btn-outline-primary btn-sm"
          >
            <i class="bi bi-github me-1" />
            YoFi V3 Repository
          </a>
          <a
            href="https://github.com/jcoliz/YoFi"
            target="_blank"
            rel="noopener noreferrer"
            class="btn btn-outline-secondary btn-sm"
          >
            <i class="bi bi-github me-1" />
            YoFi Original Repository
          </a>
        </div>
      </InfoCard>
    </div>
  </div>
</template>
