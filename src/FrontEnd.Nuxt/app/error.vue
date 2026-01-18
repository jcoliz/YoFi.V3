<template>
  <div class="min-vh-100 d-flex align-items-center justify-content-center bg-light px-4">
    <div
      class="text-center"
      style="max-width: 32rem"
    >
      <h1 class="display-1 fw-bold mb-4">
        {{ error.statusCode }}
      </h1>
      <h2 class="h3 mb-4">
        {{ error.statusMessage || 'Internal Server Error' }}
      </h2>
      <p class="text-muted mb-4">
        {{ error.message }}
      </p>
      <button
        class="btn btn-primary"
        @click="handleError"
      >
        Go back home
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { NuxtError } from '#app'

defineProps<{
  error: NuxtError
}>()

const handleError = () => clearError({ redirect: '/' })
</script>
