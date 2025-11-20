// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  modules: ['@nuxt/eslint'],
  devtools: { enabled: true },
  eslint: {
    config: {
      stylistic: false,
    },
  },
  devServer: {
    port: parseInt(process.env.PORT ?? '5173'),
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
  css: ['~/assets/scss/custom.scss'],
  router: {
    options: {
      linkActiveClass: 'active',
    },
  },
  runtimeConfig: {
    public: {
      // This will be replaced by NUXT_PUBLIC_SOLUTION_VERSION during build
      solutionVersion: 'nuxt.config.ts',
      // During development, this value is supplied by Aspire in the AppHost
      // For Containers/Production, it is overwritten during the static generation step
      // by the NUXT_PUBLIC_API_BASE_URL environment variable
      apiBaseUrl: process.env.services__backend__http__0,
      // WARNING: Capitalization must match underscores exactly when overriding from environment variable
      applicationInsightsConnectionString: 'nuxt.config.ts',
    },
  },
  appConfig: {
    name: 'YoFi.V3',
  },
})
