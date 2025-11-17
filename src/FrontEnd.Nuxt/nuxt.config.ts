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
  runtimeConfig: {
    public: {
      // For **CI** and **production**, the frontend is statically generated using
      // `nuxt generate`. At that time, the NUXT_PUBLIC_API_BASE_URL environment variable
      // is read from the build environment (e.g., Docker build ARG).
      // NOTE: Nuxt generation reads **only** NUXT_PUBLIC_* environment variables at build time
      apiBaseUrl: process.env.NODE_ENV === 'development'
        ? (process.env.services__backend__http__0) // During development, Aspire will provide the backend URL on this variable
        : (process.env.NUXT_PUBLIC_API_BASE_URL) // For production or container, the backend URL **must** be provided at build time
    }
  },
  appConfig:
  {
    name: "YoFi.V3"
  }
})
