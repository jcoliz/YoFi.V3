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

const technologies: Technology[] = [
  {
    name: '.NET 10',
    logo: '/images/dotnet-logo.svg',
    description: 'Modern backend framework for building robust APIs and web applications',
    link: 'https://dotnet.microsoft.com/',
    category: 'Backend',
  },
  {
    name: 'Nuxt 4',
    logo: '/images/nuxt-icon-green.svg',
    description: 'Full-stack Vue framework with SSR, file-based routing, and auto-imports',
    link: 'https://nuxt.com/',
    category: 'Frontend',
  },
  {
    name: 'Vue 3',
    logo: '/images/vue-logo.svg',
    description: 'Progressive JavaScript framework for building user interfaces',
    link: 'https://vuejs.org/',
    category: 'Frontend',
  },
  {
    name: 'TypeScript',
    logo: '/images/ts-logo-512.svg',
    description: 'Strongly typed programming language that builds on JavaScript',
    link: 'https://www.typescriptlang.org/',
    category: 'Language',
  },
  {
    name: 'Bootstrap 5',
    logo: '/images/bootstrap-logo.svg',
    description: 'CSS framework for building responsive, mobile-first websites',
    link: 'https://getbootstrap.com/',
    category: 'Styling',
  },
  {
    name: 'Entity Framework Core',
    logo: '/images/ef-logo.svg',
    description: 'Object-relational mapping framework for .NET applications',
    link: 'https://docs.microsoft.com/en-us/ef/',
    category: 'Database',
  },
  {
    name: 'pnpm',
    logo: '/images/pnpm-no-name-with-frame.svg',
    description: 'Fast, disk space efficient package manager for Node.js',
    link: 'https://pnpm.io/',
    category: 'Tools',
  },
  {
    name: 'Azure Static Web Apps',
    logo: '/images/01007-icon-service-Static-Apps.svg',
    description: 'Full-stack web app hosting with integrated CI/CD workflows',
    link: 'https://azure.microsoft.com/en-us/services/app-service/static/',
    category: 'Cloud',
  },
  {
    name: 'Azure App Services',
    logo: '/images/10035-icon-service-App-Services.svg',
    description: 'Platform for hosting web applications and APIs in the cloud',
    link: 'https://azure.microsoft.com/en-us/services/app-service/',
    category: 'Cloud',
  },
  {
    name: 'Azure Application Insights',
    logo: '/images/00012-icon-service-Application-Insights.svg',
    description: 'Application performance monitoring and analytics service',
    link: 'https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview',
    category: 'Monitoring',
  },
  {
    name: 'Azure Pipelines',
    logo: '/images/10261-icon-service-Azure-DevOps.svg',
    description: 'Cloud-based CI/CD service for building, testing, and deploying code',
    link: 'https://azure.microsoft.com/en-us/services/devops/pipelines/',
    category: 'DevOps',
  },
  {
    name: 'PostgreSQL',
    logo: '/images/postgres-elephant.png',
    description: 'Advanced open-source relational database system',
    link: 'https://www.postgresql.org/',
    category: 'Database',
  },
  {
    name: 'SQLite',
    logo: '/images/sqlite-logo.svg',
    description: 'Lightweight, serverless database engine for development and testing',
    link: 'https://www.sqlite.org/',
    category: 'Database',
  },
  {
    name: 'Docker',
    logo: '/images/docker-mark-blue.svg',
    description: 'Platform for developing, shipping, and running applications in containers',
    link: 'https://www.docker.com/',
    category: 'DevOps',
  },
  {
    name: 'Playwright',
    logo: '/images/playwright-logo.svg',
    description: 'End-to-end testing framework for modern web applications',
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
  <div class="container-fluid">
    <!-- Hero Section -->
    <div class="row bg-primary text-white py-5 mb-5">
      <div class="col-12 text-center">
        <div class="container">
          <h1 class="display-4 fw-bold mb-3">YoFi.V3 Reference Implementation</h1>
          <p class="lead mb-4">
            A modern .NET 10 + Nuxt 4 stack with authentication, authorization, and best practices
          </p>
          <p class="mb-4">
            This application serves as a comprehensive reference implementation showcasing the
            integration of cutting-edge technologies for building robust, scalable web applications.
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
            Explore the powerful technologies that make this application possible
          </p>
        </div>
      </div>

      <div class="row g-4">
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
              <div class="mt-auto">
                <span class="badge bg-secondary mb-2">{{ tech.category }}</span>
                <br />
                <a
                  :href="tech.link"
                  target="_blank"
                  rel="noopener"
                  class="btn btn-outline-primary btn-sm"
                >
                  Learn More <i class="bi bi-arrow-up-right"></i>
                </a>
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
