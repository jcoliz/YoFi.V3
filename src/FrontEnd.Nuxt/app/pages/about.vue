<script setup lang="ts">
/**
 * About Page
 *
 * Displays version information for both frontend and backend, along with
 * configuration details like API base URL and Application Insights connection.
 * Demonstrates authenticated API calls to retrieve backend version.
 */

import * as api from '../utils/apiclient'
import { useAuthFetch } from '../composables/useAuthFetch'
definePageMeta({
  title: 'About',
  auth: true,
})

/**
 * Client for communicating with server
 */
const { baseUrl } = useApiBaseUrl()
const authFetch = useAuthFetch()
const client = new api.VersionClient(baseUrl, authFetch)

/**
 * Version data to display
 */

const version = ref<string>('Loading...')
const runtimeConfig = useRuntimeConfig()
const frontEndVersion = runtimeConfig.public.solutionVersion
const applicationInsightsConnectionString = runtimeConfig.public.applicationInsightsConnectionString

/**
 * When mounted, get the view data from server
 */
onMounted(() => {
  client.getVersion().then((result) => {
    version.value = result
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
        <div class="row mb-3">
          <div class="col-sm-4">
            <strong>API Base URL</strong>
          </div>
          <div class="col-sm-8">
            {{ baseUrl }}
          </div>
        </div>
        <div class="row mb-3">
          <div class="col-sm-4">
            <strong>Application Insights Connection String</strong><br />
            <small class="text-muted"
              >runtimeConfig.public.applicationInsightsConnectionString</small
            >
          </div>
          <div class="col-sm-8">
            {{ applicationInsightsConnectionString }}
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
