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
const applicationInsightsConnectionString = runtimeConfig.public.applicationInsightsConnectionString

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
  <div class="row">
    <div class="col-lg-8">
      <div class="card">
        <div class="card-header">
          <h4 class="card-title mb-0">Version Information</h4>
        </div>
        <div class="card-body">
          <div class="row mb-3">
            <div class="col-sm-4">
              <strong>Front End Version</strong><br>
              <small class="text-muted">runtimeConfig.public.solutionVersion</small>
            </div>
            <div class="col-sm-8">
              {{ frontEndVersion }}
            </div>
          </div>
          <div class="row mb-3">
            <div class="col-sm-4">
              <strong>Back End Version</strong><br>
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
              <strong>Application Insights Connection String</strong><br>
              <small class="text-muted">runtimeConfig.public.applicationInsightsConnectionString</small>
            </div>
            <div class="col-sm-8">
              {{ applicationInsightsConnectionString }}
            </div>
          </div>        
        </div>
      </div>
    </div>

    <div class="col-lg-4">
      <div class="card">
        <div class="card-header">
          <h5 class="card-title mb-0">Application Info</h5>
        </div>
        <div class="card-body">
          <p class="text-muted">
            <small>YoFi V3 is a personal finance management application built with Nuxt 4 and ASP.NET Core.</small>
          </p>
          <div class="d-grid gap-2 mt-3">
            <a href="https://github.com/jcoliz/YoFi.V3" target="_blank" rel="noopener noreferrer" class="btn btn-outline-primary btn-sm">
              <i class="bi bi-github me-1"/>
              YoFi V3 Repository
            </a>
            <a href="https://github.com/jcoliz/YoFi" target="_blank" rel="noopener noreferrer" class="btn btn-outline-secondary btn-sm">
              <i class="bi bi-github me-1"/>
              YoFi Original Repository
            </a>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
