import type { ApplicationInsights } from '@microsoft/applicationinsights-web'

/**
 * Composable providing safe access to Application Insights.
 *
 * Returns a null-object wrapper: all methods are no-ops when App Insights
 * is not initialized (missing connection string, Playwright guard, etc.).
 * Mirrors the SDK shape so SDK documentation applies directly at call sites.
 */
export function useAppInsights() {
  const { $appInsights } = useNuxtApp()
  const ai = $appInsights as ApplicationInsights | undefined

  return {
    trackEvent: (event: { name: string }, properties?: Record<string, unknown>) =>
      ai?.trackEvent(event, properties),
    trackMetric: (
      metric: { name: string; average: number },
      properties?: Record<string, unknown>,
    ) => ai?.trackMetric(metric, properties),
    trackException: (exception: { exception: Error }, properties?: Record<string, unknown>) =>
      ai?.trackException(exception, properties),
  }
}
