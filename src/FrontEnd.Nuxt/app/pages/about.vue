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

interface Technology {
  name: string
  logo: string
  description: string
  link: string
  category: string
}

const getCategoryStyle = (category: string) => {
  const categoryColors: Record<string, { bg: string; text: string }> = {
    Frontend: { bg: '#E8F4F8', text: '#4A7C8C' },
    Backend: { bg: '#F0E8F4', text: '#6B5B7C' },
    Database: { bg: '#E8F4E8', text: '#5B7C5B' },
    Cloud: { bg: '#FFF4E8', text: '#8C7C5B' },
    'Dev Tools': { bg: '#E8EEF4', text: '#5B6B7C' },
    Testing: { bg: '#F4EEE8', text: '#7C715B' },
  }

  const colors = categoryColors[category] || { bg: '#E9ECEF', text: '#6C757D' }
  return `background-color: ${colors.bg}; color: ${colors.text};`
}

const technologies: Technology[] = [
  {
    name: '.NET 10',
    logo: '/images/dotnet-logo.svg',
    description:
      'Powers our REST API backend with ASP.NET Core, featuring Clean Architecture with dependency injection, Entity Framework Core for data access, and JWT-based authentication',
    link: 'https://dotnet.microsoft.com/',
    category: 'Backend',
  },
  {
    name: 'Nuxt 4',
    logo: '/images/nuxt-icon-green.svg',
    description:
      'Orchestrates our Vue frontend with file-based routing, auto-imported composables, server-side rendering capabilities, and static site generation for Azure deployment',
    link: 'https://nuxt.com/',
    category: 'Frontend',
  },
  {
    name: 'Vue 3',
    logo: '/images/vue-logo.svg',
    description:
      'Drives our reactive UI components using Composition API, with type-safe props and automatic component registration via Nuxt',
    link: 'https://vuejs.org/',
    category: 'Frontend',
  },
  {
    name: 'TypeScript',
    logo: '/images/ts-logo-512.svg',
    description:
      'Provides type safety across our frontend with auto-generated API client types from backend OpenAPI spec, ensuring compile-time contract validation',
    link: 'https://www.typescriptlang.org/',
    category: 'Frontend',
  },
  {
    name: 'NuxtAuth',
    logo: '/images/nuxt-auth.png',
    description:
      'Handles client-side authentication using local provider pattern, managing JWT tokens from our .NET Identity backend with automatic refresh and secure session storage',
    link: 'https://auth.sidebase.io/',
    category: 'Frontend',
  },
  {
    name: 'Bootstrap 5',
    logo: '/images/bootstrap-logo.svg',
    description:
      'Styles our responsive UI with utility classes and components, integrated via custom SCSS for theme customization and @coliz/vue-base-controls',
    link: 'https://getbootstrap.com/',
    category: 'Frontend',
  },
  {
    name: 'pnpm',
    logo: '/images/pnpm-no-name-with-frame.svg',
    description:
      'Manages our frontend dependencies with efficient disk usage and fast installs, integrated into development scripts and Azure Pipelines CI/CD',
    link: 'https://pnpm.io/',
    category: 'Frontend',
  },
  {
    name: 'Entity Framework Core',
    logo: '/images/dotnet-logo.svg',
    description:
      'Manages our database access layer with code-first migrations, SQLite provider for development, and support for switching to PostgreSQL in production',
    link: 'https://docs.microsoft.com/en-us/ef/',
    category: 'Database',
  },
  {
    name: '.NET Aspire',
    logo: '/images/dotnet-logo.svg',
    description:
      'Orchestrates our development environment with unified dashboard, service discovery between frontend and backend, and built-in observability during local development',
    link: 'https://learn.microsoft.com/en-us/dotnet/aspire/',
    category: 'Dev Tools',
  },
  {
    name: 'Azure Static Web Apps',
    logo: '/images/01007-icon-service-Static-Apps.svg',
    description:
      'Hosts our statically-generated Nuxt frontend with global CDN distribution, custom domain support, and direct integration with our App Service backend API',
    link: 'https://azure.microsoft.com/en-us/services/app-service/static/',
    category: 'Cloud',
  },
  {
    name: 'Azure App Services',
    logo: '/images/10035-icon-service-App-Services.svg',
    description:
      'Runs our .NET backend API with persistent storage for SQLite database, providing authentication endpoints and business logic for the static frontend',
    link: 'https://azure.microsoft.com/en-us/services/app-service/',
    category: 'Cloud',
  },
  {
    name: 'Azure Application Insights',
    logo: '/images/00012-icon-service-Application-Insights.svg',
    description:
      'Monitors our production application with distributed tracing, custom telemetry events, and real-time performance metrics integrated via .NET Service Defaults',
    link: 'https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview',
    category: 'Cloud',
  },
  {
    name: 'Azure Pipelines',
    logo: '/images/10261-icon-service-Azure-DevOps.svg',
    description:
      'Automates our build, test, and deployment workflow with containerized functional tests, parallel frontend/backend deployments, and Bicep infrastructure provisioning',
    link: 'https://azure.microsoft.com/en-us/services/devops/pipelines/',
    category: 'Dev Tools',
  },
  {
    name: 'PostgreSQL',
    logo: '/images/postgres-elephant.png',
    description:
      "Our planned database for scaled production deployments, leveraging Entity Framework Core's provider model to migrate from SQLite while maintaining identical data access code",
    link: 'https://www.postgresql.org/',
    category: 'Database',
  },
  {
    name: 'SQLite',
    logo: '/images/SQLite-Logo-square.svg',
    description:
      'Serves as our single database implementation across development, CI/CD, and production, eliminating environment-specific configuration with file-based storage',
    link: 'https://www.sqlite.org/',
    category: 'Database',
  },
  {
    name: 'Docker',
    logo: '/images/docker-mark-blue.svg',
    description:
      'Containerizes our application for functional testing in CI/CD pipelines, with docker-compose orchestration for running complete stack locally',
    link: 'https://www.docker.com/',
    category: 'Dev Tools',
  },
  {
    name: 'Playwright',
    logo: '/images/playwright-logo.svg',
    description:
      'Runs our end-to-end functional tests with C# bindings, validating user workflows against containerized application in CI/CD pipeline',
    link: 'https://playwright.dev/',
    category: 'Testing',
  },
]

const sourceRepos = [
  {
    name: 'YoFi.V3',
    description: 'This complete reference implementation project',
    link: 'https://github.com/jcoliz/YoFi.V3',
  },
  {
    name: 'NuxtIdentity',
    description: 'Nuxt module for .NET Identity authentication',
    link: 'https://github.com/jcoliz/NuxtIdentity',
  },
  {
    name: '@coliz/vue-base-controls',
    description: 'Reusable Vue components library',
    link: 'https://github.com/jcoliz/vue-base-controls',
  },
]
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

  <!-- Technology Showcase -->
  <div class="container mt-4 mb-5">
    <div class="row mb-4">
      <div class="col-12 text-center">
        <h2 class="display-5 mb-3">Technology Stack</h2>
        <p class="lead text-muted">
          Explore the modern technologies that make this application possible
        </p>
      </div>
    </div>

    <div
      class="row g-4"
      data-test-id="tech-card"
    >
      <div
        v-for="tech in technologies"
        :key="tech.name"
        class="col-md-6 col-lg-4"
      >
        <div class="card h-100 shadow-sm">
          <div class="card-body d-flex flex-column">
            <div class="d-flex align-items-center mb-3">
              <img
                :src="tech.logo"
                :alt="tech.name + ' logo'"
                class="me-3"
                style="width: 40px; height: 40px; object-fit: contain"
              />
              <h5 class="card-title mb-0">{{ tech.name }}</h5>
            </div>
            <p class="card-text flex-grow-1">{{ tech.description }}</p>
            <div class="mt-auto d-flex justify-content-between align-items-end">
              <a
                :href="tech.link"
                target="_blank"
                rel="noopener"
                class="btn btn-outline-primary btn-sm"
              >
                Learn More <i class="bi bi-arrow-up-right"></i>
              </a>
              <span
                class="badge"
                :style="getCategoryStyle(tech.category)"
                >{{ tech.category }}</span
              >
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Source Code Repositories -->
  <div class="bg-light py-4 mb-5">
    <div class="container">
      <div class="row mb-4">
        <div class="col-12 text-center">
          <h2 class="display-5 mb-3">Source Code</h2>
          <p class="lead text-muted">
            Explore the open-source repositories that power this project
          </p>
        </div>
      </div>

      <div class="row g-4">
        <div
          v-for="repo in sourceRepos"
          :key="repo.name"
          class="col-md-4"
        >
          <div class="card h-100">
            <div class="card-body d-flex flex-column">
              <h5 class="card-title"><i class="bi bi-github me-2"></i>{{ repo.name }}</h5>
              <p class="card-text flex-grow-1">{{ repo.description }}</p>
              <a
                :href="repo.link"
                target="_blank"
                rel="noopener"
                class="btn btn-primary mt-auto"
              >
                View on GitHub <i class="bi bi-arrow-up-right"></i>
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
