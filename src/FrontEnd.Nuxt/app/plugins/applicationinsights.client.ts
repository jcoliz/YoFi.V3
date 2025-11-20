/**
 * Application Insights Client Plugin
 *
 * This Nuxt plugin initializes Microsoft Application Insights for client-side telemetry.
 * It runs only on the client (note the .client.ts suffix) to track user interactions,
 * page views, AJAX calls, errors, and performance metrics.
 *
 * Configuration:
 * - Reads connection string from runtime config (set via NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING)
 * - If no connection string is provided, the plugin gracefully exits with a warning
 *
 * Features enabled:
 * - Auto route tracking: Automatically tracks SPA route changes
 * - Request/Response header tracking: Captures HTTP headers for debugging
 * - AJAX performance tracking: Monitors API call performance
 * - Unhandled promise rejection tracking: Catches async errors
 * - CORS correlation: Enables distributed tracing across origins
 * - W3C distributed tracing: Uses standard trace context format
 * - Page visit time tracking: Measures how long users spend on each page
 *
 * Current limitations:
 * - Sets all users as "anonymous" for privacy (see TODOs for potential improvements)
 * - Connection string is exposed client-side (consider backend-only initialization for logged-in users)
 *
 * Usage:
 * Access via `const { $appInsights } = useNuxtApp()` in any component or composable
 */
import { ApplicationInsights } from '@microsoft/applicationinsights-web'

export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()

  const connectionString = config.public.applicationInsightsConnectionString

  if (!connectionString) {
    console.warn('Application Insights connection string not configured')
    return
  }

  // Initialize Application Insights
  const appInsights = new ApplicationInsights({
    config: {
      connectionString: connectionString,
      enableAutoRouteTracking: true,
      enableRequestHeaderTracking: true,
      enableResponseHeaderTracking: true,
      enableAjaxErrorStatusText: true,
      enableAjaxPerfTracking: true,
      enableUnhandledPromiseRejectionTracking: true,
      disableFetchTracking: false,
      enableCorsCorrelation: true,
      distributedTracingMode: 2, // W3C standard
      autoTrackPageVisitTime: true,
    },
  })

  appInsights.loadAppInsights()

  // TODO: Set user context if available
  // TODO: Consider ONLY enabling app insights when user is logged in. This has the benefit that
  // app insights connection string can be proteced on the backend and not exposed to the public.
  // However, it means we lose telemetry on anonymous user behavior.
  // For now, we set a generic anonymous user context.
  appInsights.setAuthenticatedUserContext('anonymous', 'anonymous', true)

  console.log('Application Insights initialized successfully')

  // Make available globally
  return {
    provide: {
      appInsights: appInsights.appInsights as unknown as ApplicationInsights,
    },
  }
})
