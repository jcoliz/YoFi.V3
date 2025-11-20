// plugins/applicationinsights.client.ts
import { ApplicationInsights } from '@microsoft/applicationinsights-web'

export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()

  const connectionString = config.public.applicationInsightsConnectionString as string

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
      appInsights: appInsights.appInsights,
    },
  }
})
