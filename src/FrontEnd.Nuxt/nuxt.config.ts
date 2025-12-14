// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  modules: ['@nuxt/eslint', '@sidebase/nuxt-auth', '@pinia/nuxt'],

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
    originEnvKey: 'NUXT_PUBLIC_API_BASE_URL',
    // disableServerSideAuth: true, // enabling this helps correlation, but fails UserViewsTheirAccountDetails test :(
    provider: {
      type: 'local',
      endpoints: {
        signIn: { path: '/api/auth/login', method: 'post' }, // ADD THIS LINE
        signOut: { path: '/api/auth/logout', method: 'post' },
        getSession: { path: '/api/auth/user' },
        signUp: { path: '/api/auth/signup', method: 'post' },
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
        endpoint: { path: '/api/auth/refresh', method: 'post' },
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
      // IMPORTANT: Disabled periodic refresh to prevent race conditions with token updates
      // The aggressive 5-second interval was causing multiple simultaneous refresh calls
      // before the state could update, leading to 401 errors with reused refresh tokens
      enablePeriodically: 60000, // for testing
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
      // This value is overwritten during the static generation step
      // by the NUXT_PUBLIC_API_BASE_URL environment variable.
      // Don't fill this in with a default value here, or it will cause problems!
      apiBaseUrl: ``,
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
