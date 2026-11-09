<script setup lang="ts">
const runtimeConfig = useRuntimeConfig()
const frontEndVersion = runtimeConfig.public.solutionVersion
</script>

<template>
  <footer
    class="container mt-5 border-top py-3 text-body-secondary"
    data-test-id="site-footer"
  >
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3">
      <nav aria-label="Footer navigation">
        <ul class="nav">
          <li class="nav-item">
            <NuxtLink
              class="nav-link px-2"
              to="/about"
              >About</NuxtLink
            >
          </li>
          <li class="nav-item">
            <a
              class="nav-link px-2"
              href="https://github.com/jcoliz/YoFi.V3"
              target="_blank"
              rel="noopener noreferrer"
              >GitHub</a
            >
          </li>
        </ul>
      </nav>
      <small>Version {{ frontEndVersion }}</small>
    </div>
  </footer>
</template>
