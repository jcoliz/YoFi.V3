<script setup lang="ts">
definePageMeta({
  title: 'Home',
  order: 1,
  layout: 'chrome',
  auth: false,
})

/**
 * Brochure page accessible to all
 *
 * This page is designed to advertise the features of the application.
 * Currently, there aren't any actual application features. Instead,
 * in its current form, the app is more of a reference implementation
 * for a modern .NET 10 + Nuxt 4 stack with authentication, authorization,
 * and a variety of best practices.
 */
</script>

<template>
  <div
    class="container-fluid"
    data-test-id="brochure-section"
  >
    <!-- Hero Section -->
    <div class="row bg-primary text-white py-5 mb-5">
      <div class="col-12 text-center">
        <div class="container">
          <h1 class="display-4 fw-bold mb-3">YoFi.V3 Reference Implementation</h1>
          <p class="lead mb-4">
            A modern .NET 10 + Nuxt 4 stack with authentication, authorization, and best practices
          </p>
          <p class="mb-4">
            This reference implementation demonstrates integrating .NET 10, Nuxt 4, authentication,
            and best practices for building reliable, scalable web applications.
          </p>
        </div>
      </div>
    </div>

    <!-- Call to Action -->
    <div class="container mb-5">
      <div class="row">
        <div class="col-12 text-center">
          <h2 class="display-5 mb-4">Get Started</h2>
          <p class="lead mb-4">Ready to explore or contribute? Here are your next steps:</p>

          <div class="row g-3 justify-content-center">
            <div class="col-auto">
              <NuxtLink
                to="/register"
                class="btn btn-primary btn-lg"
              >
                <i class="bi bi-person-plus me-2"></i>Sign Up & Explore
              </NuxtLink>
            </div>
            <div class="col-auto">
              <a
                href="https://hub.docker.com/r/jcoliz/yofi-v3"
                target="_blank"
                rel="noopener"
                class="btn btn-outline-primary btn-lg"
              >
                <i class="bi bi-download me-2"></i>Run Locally with Docker
              </a>
            </div>
            <div class="col-auto">
              <a
                href="https://github.com/jcoliz/YoFi.V3"
                target="_blank"
                rel="noopener"
                class="btn btn-outline-secondary btn-lg"
              >
                <i class="bi bi-code-slash me-2"></i>View Source Code
              </a>
            </div>
          </div>

          <div class="mt-4">
            <p class="text-muted">
              Have feedback or questions?
              <a
                href="https://github.com/jcoliz/YoFi.V3/issues"
                target="_blank"
                rel="noopener"
              >
                Open an issue
              </a>
              or start a
              <a
                href="https://github.com/jcoliz/YoFi.V3/discussions"
                target="_blank"
                rel="noopener"
              >
                discussion on GitHub </a
              >.
            </p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
