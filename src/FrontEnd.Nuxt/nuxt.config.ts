// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  modules: ['@nuxt/eslint', '@sidebase/nuxt-auth'],

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
  auth: {
    baseURL: `${process.env.services__backend__http__0}/api/auth`, // Update this to your backend URL
    provider: {
      type: 'local',
      endpoints: {
        signIn: { path: '/login', method: 'post' }, // ADD THIS LINE
        signOut: { path: '/logout', method: 'post' },
        getSession: { path: '/user' },
        signUp: { path: '/signup', method: 'post' },
      },
      // not 'pages'??
      pages: {
        login: '/login', // Path to the login page (where unauthenticated users are sent)
      },
      token: {
        signInResponseTokenPointer: '/token/accessToken',
      },
      refresh: {
        isEnabled: true,
        endpoint: { path: '/refresh', method: 'post' },
        refreshOnlyToken: false,
        token: {
          signInResponseRefreshTokenPointer: '/token/refreshToken',
          refreshResponseTokenPointer: '/token/accessToken',
          refreshRequestTokenPointer: '/refreshToken',
        },
      },
      session: {
        dataType: {
          id: 'string',
          email: 'string',
          name: 'string',
          roles: 'string[]', // Updated to match your UserInfo model
          claims: '{ type:string, value:string }[]',
        },
        dataResponsePointer: '/user',
      },
    },
    sessionRefresh: {
      // Whether to refresh the session every time the browser window is refocused.
      enableOnWindowFocus: true,
      // Whether to refresh the session every `X` milliseconds. Set this to `false` to turn it off. The session will only be refreshed if a session already exists.
      enablePeriodically: 5000, // just for demo!!
      // Custom refresh handler - uncomment to use
      // handler: './config/AuthRefreshHandler'
    },
    globalAppMiddleware: {
      isEnabled: true,
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
  nitro: {
    prerender: {
      routes: ['/health'],
    },
  },
})
