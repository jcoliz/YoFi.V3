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
 *
 * Therefore, this brochure page serves to inform visitors about the
 * technology stack. We want to show off all the various technology
 * components which come together to make this application work.
 *
 * For each, we'd want: Name, logo (image), short explaanation of what
 * we use it for, and a link to the official website.
 *
 * Technologies to highlight:
 * - .NET 10
 * - Nuxt 4
 * - Vue 3
 * - TypeScript
 * - Bootstrap 5
 * - Entity Framework Core
 * - pnpm
 * - Azure Static Web Apps
 * - Azure App Services
 * - Azure Application Insights
 * - Azure Log Analytics
 * - Azure Storage Accounts
 * - Azure Pipelines
 * - .NET Aspire
 * - Docker
 * - PostgreSQL
 * - Sqlite
 * - Playwright
 *
 * Links to my source code repos
 * - YoFi.V3 (this project)
 * - NuxtIdentity
 * - @coliz/vue-base-controls
 *
 * Calls to action
 * - Sign up for an account, and look around
 * - Pull the docker compose project from Docker Hub and run locally
 * - Check out the source code on GitHub
 * - Give feedback via issues or discussions on GitHub
 */
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
    Language: { bg: '#F4E8E8', text: '#8C6B6B' },
    Styling: { bg: '#F4F0E8', text: '#7C7560' },
    Tools: { bg: '#E8EEF4', text: '#5B6B7C' },
    Monitoring: { bg: '#F4E8F0', text: '#7C5B6B' },
    DevOps: { bg: '#E8F0F4', text: '#5B6B7C' },
    Testing: { bg: '#F4EEE8', text: '#7C715B' },
    Authentication: { bg: '#E8F4F8', text: '#4A7C8C' },
  }

  const colors = categoryColors[category] || { bg: '#E9ECEF', text: '#6C757D' }
  return `background-color: ${colors.bg}; color: ${colors.text};`
}

const technologies: Technology[] = [
  {
    name: '.NET 10',
    logo: '/images/dotnet-logo.svg',
    description: 'Powers our REST API backend with ASP.NET Core, featuring Clean Architecture with dependency injection, Entity Framework Core for data access, and JWT-based authentication',
    link: 'https://dotnet.microsoft.com/',
    category: 'Backend',
  },
  {
    name: 'Nuxt 4',
    logo: '/images/nuxt-icon-green.svg',
    description: 'Orchestrates our Vue frontend with file-based routing, auto-imported composables, server-side rendering capabilities, and static site generation for Azure deployment',
    link: 'https://nuxt.com/',
    category: 'Frontend',
  },
  {
    name: 'Vue 3',
    logo: '/images/vue-logo.svg',
    description: 'Drives our reactive UI components using Composition API, with type-safe props and automatic component registration via Nuxt',
    link: 'https://vuejs.org/',
    category: 'Frontend',
  },
  {
    name: 'TypeScript',
    logo: '/images/ts-logo-512.svg',
    description: 'Provides type safety across our frontend with auto-generated API client types from backend OpenAPI spec, ensuring compile-time contract validation',
    link: 'https://www.typescriptlang.org/',
    category: 'Language',
  },
  {
    name: 'NuxtAuth',
    logo: '/images/nuxt-auth.png',
    description: 'Handles client-side authentication using local provider pattern, managing JWT tokens from our .NET Identity backend with automatic refresh and secure session storage',
    link: 'https://auth.sidebase.io/',
    category: 'Authentication',
  },
  {
    name: 'Bootstrap 5',
    logo: '/images/bootstrap-logo.svg',
    description: 'Styles our responsive UI with utility classes and components, integrated via custom SCSS for theme customization and @coliz/vue-base-controls',
    link: 'https://getbootstrap.com/',
    category: 'Styling',
  },
  {
    name: 'Entity Framework Core',
    logo: '/images/ef-logo.svg',
    description: 'Manages our database access layer with code-first migrations, SQLite provider for development, and support for switching to PostgreSQL in production',
    link: 'https://docs.microsoft.com/en-us/ef/',
    category: 'Database',
  },
  {
    name: 'pnpm',
    logo: '/images/pnpm-no-name-with-frame.svg',
    description: 'Manages our frontend dependencies with efficient disk usage and fast installs, integrated into development scripts and Azure Pipelines CI/CD',
    link: 'https://pnpm.io/',
    category: 'Tools',
  },
  {
    name: '.NET Aspire',
    logo: '/images/dotnet-logo.svg',
    description: 'Orchestrates our development environment with unified dashboard, service discovery between frontend and backend, and built-in observability during local development',
    link: 'https://learn.microsoft.com/en-us/dotnet/aspire/',
    category: 'Tools',
  },
  {
    name: 'Azure Static Web Apps',
    logo: '/images/01007-icon-service-Static-Apps.svg',
    description: 'Hosts our statically-generated Nuxt frontend with global CDN distribution, custom domain support, and direct integration with our App Service backend API',
    link: 'https://azure.microsoft.com/en-us/services/app-service/static/',
    category: 'Cloud',
  },
  {
    name: 'Azure App Services',
    logo: '/images/10035-icon-service-App-Services.svg',
    description: 'Runs our .NET backend API with persistent storage for SQLite database, providing authentication endpoints and business logic for the static frontend',
    link: 'https://azure.microsoft.com/en-us/services/app-service/',
    category: 'Cloud',
  },
  {
    name: 'Azure Application Insights',
    logo: '/images/00012-icon-service-Application-Insights.svg',
    description: 'Monitors our production application with distributed tracing, custom telemetry events, and real-time performance metrics integrated via .NET Service Defaults',
    link: 'https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview',
    category: 'Monitoring',
  },
  {
    name: 'Azure Pipelines',
    logo: '/images/10261-icon-service-Azure-DevOps.svg',
    description: 'Automates our build, test, and deployment workflow with containerized functional tests, parallel frontend/backend deployments, and Bicep infrastructure provisioning',
    link: 'https://azure.microsoft.com/en-us/services/devops/pipelines/',
    category: 'DevOps',
  },
  {
    name: 'PostgreSQL',
    logo: '/images/postgres-elephant.png',
    description: 'Our planned database for scaled production deployments, leveraging Entity Framework Core\'s provider model to migrate from SQLite while maintaining identical data access code',
    link: 'https://www.postgresql.org/',
    category: 'Database',
  },
  {
    name: 'SQLite',
    logo: '/images/sqlite-logo.svg',
    description: 'Serves as our single database implementation across development, CI/CD, and production, eliminating environment-specific configuration with file-based storage',
    link: 'https://www.sqlite.org/',
    category: 'Database',
  },
  {
    name: 'Docker',
    logo: '/images/docker-mark-blue.svg',
    description: 'Containerizes our application for functional testing in CI/CD pipelines, with docker-compose orchestration for running complete stack locally',
    link: 'https://www.docker.com/',
    category: 'DevOps',
  },
  {
    name: 'Playwright',
    logo: '/images/playwright-logo.svg',
    description: 'Runs our end-to-end functional tests with C# bindings, validating user workflows against containerized application in CI/CD pipeline',
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
            This reference implementation demonstrates integrating .NET 10, Nuxt 4, authentication, and best practices for building reliable, scalable web applications.
          </p>
        </div>
      </div>
    </div>

    <!-- Technology Showcase -->
    <div class="container mb-5">
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
                >{{ tech.category }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Source Code Repositories -->
    <div class="bg-light py-5 mb-5">
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
