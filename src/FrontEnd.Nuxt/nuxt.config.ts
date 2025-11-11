// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  devServer: {
    port: parseInt(process.env.PORT ?? "5173"),
  },
  vite: {
    css: {
      preprocessorOptions: {
        scss: {
          quietDeps: true,
          // You can also silence specific deprecation types from your own code if needed
          silenceDeprecations: ['import', 'color-functions'],
        },
      },
    },
  },
  css: [
    '~/assets/scss/custom.scss'
  ],
  router: {
    options: {
      linkActiveClass: 'active'
    }
  },
  routeRules: {
    // This is used to proxy API requests during **development**
    '/api/**': { cors: true, proxy: `${process.env.services__backend__http__0}/api/**` }
  },
  runtimeConfig: {
    public: {
      // For **CI** and **production**, the frontend is statically generated using
      // `nuxt generate`. At that time, the NUXT_PUBLIC_API_BASE_URL environment variable
      // is read from the build environment (e.g., Docker build ARG).
      // NOTE: Nuxt generation reads NUXT_PUBLIC_* environment variables at build time
      // ALSO NOTE: Using this `apiBaseUrl` is still a work in progress.
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL || 'http://localhost:5379'
    }
  }
})
