# Nuxt Front-End Integration with Application Insights

This document describes how Microsoft Application Insights is wired into the Nuxt front-end across all three environments (production, container, and local development), and covers additional capabilities to consider when integrating App Insights into a new project.

---

## How the Connection String Flows

The connection string follows the same three-step pattern in every environment:

1. **Provision** — Azure resources are deployed and the App Insights connection string is retrieved
2. **Inject** — The connection string is injected as `NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING`
3. **Consume** — Nuxt's runtime config override mechanism picks it up and the plugin initializes

The key Nuxt mechanism: `NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING` overrides the `runtimeConfig.public.applicationInsightsConnectionString` key declared in `src\FrontEnd.Nuxt\nuxt.config.ts`. The capitalization must match exactly — the env var name is the camelCase key converted to `SCREAMING_SNAKE_CASE` with the `NUXT_PUBLIC_` prefix. A mismatch silently fails with no error.

> **Important:** Because Nuxt uses `npm run generate` (static site generation), the connection string is **baked into the generated JS bundle at build time**, not injected at runtime. You cannot rotate the connection string without a redeploy.

---

## Cloud Resources

1. `infra\main.bicep` uses `AzDeploy.Bicep/Web/webapp-appinsights.bicep` to deploy a web app with an Application Insights instance
2. The name of that resource is included in the deployment output as `appInsightsName`
3. `scripts\Provision-Resources.ps1` uses the Azure CLI to obtain the connection string for that resource and prints it as `appInsightsConnectionString`

---

## Production Environment

4. Pipeline administrator adds a variable to the Release Pipeline (defined in `.azure\pipelines\cd.yaml`) called `insightsConnectionString`
5. `.azure\pipelines\steps\build-nuxt.yaml` picks that up and sets `NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING` as an environment variable during generation
6. This overrides the `runtimeConfig.public.applicationInsightsConnectionString` setting in `src\FrontEnd.Nuxt\nuxt.config.ts`
7. This value is picked up by `src\FrontEnd.Nuxt\app\plugins\applicationinsights.client.ts`
8. Which constructs a new `ApplicationInsights` using that connection string — **this is our own code**, not a Microsoft boilerplate. The plugin includes project-specific logic: a Playwright detection guard, anonymous user context, and specific feature flags
9. The above requires `@microsoft/applicationinsights-web` in the project

---

## Container Environment

Steps 1–3 same as above.

4. Developer adds a line to `docker\.env`:
   ```
   APPLICATIONINSIGHTS_CONNECTION_STRING=...
   ```
5. `docker\docker-compose-ci.yml` picks that up and passes it as `NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING` as a build arg to the Docker build command
6. `src\FrontEnd.Nuxt\docker\Dockerfile` picks up that arg and sets an environment variable of the same name
7. This is referenced by `npm run generate`

Steps 6 and beyond continue as above.

---

## Local Development

Steps 1–3 same as above.

4. Developer adds a line to `src\AppHost\.env`:
   ```
   APPLICATIONINSIGHTS_CONNECTION_STRING=...
   ```
5. `src\AppHost\AppHost.cs` loads environment variables into configuration using `DotNetEnv.Env.Load()`
6. It then pulls the `APPLICATIONINSIGHTS_CONNECTION_STRING` configuration value out of the app builder
7. And sets `NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING` in the environment of the JS app component
8. This is `pnpm run dev` running (not generating), but this still overrides the `runtimeConfig.public.applicationInsightsConnectionString` setting in `src\FrontEnd.Nuxt\nuxt.config.ts`

Steps 7 and beyond above continue as above.

> **Note:** The `.env` file must never be committed to source control. Verify your `.gitignore` excludes `src/AppHost/.env` before committing.

> **Note:** The backend also receives the connection string — `AppHost.cs` passes `APPLICATIONINSIGHTS_CONNECTION_STRING` to the backend service as well. Both frontend and backend can share the same App Insights resource but use different env var names.

---

## What the Plugin Does

The plugin at `src\FrontEnd.Nuxt\app\plugins\applicationinsights.client.ts` (note the `.client.ts` suffix — it runs only in the browser) enables the following features automatically with no additional code:

| Feature | Status |
|---|---|
| Auto route tracking (SPA page views) | ✅ Enabled |
| AJAX / Fetch performance tracking | ✅ Enabled |
| Request and response header tracking | ✅ Enabled |
| Unhandled promise rejection tracking | ✅ Enabled |
| W3C distributed tracing / CORS correlation | ✅ Enabled |
| Page visit time tracking | ✅ Enabled |

The plugin also includes a **Playwright detection guard**: if `navigator.webdriver` is `true`, App Insights is disabled. This prevents CORS errors when Playwright injects custom HTTP headers during functional tests.

---

## Custom Telemetry

Beyond automatic instrumentation, you can call App Insights methods directly. Access the instance in any component or composable via:

```ts
const { $appInsights } = useNuxtApp()
const appInsights = $appInsights as ApplicationInsights
```

### Custom Events

Used in `src\FrontEnd.Nuxt\app\pages\counter.vue` as a prototype:

```ts
if (appInsights) {
  appInsights.trackEvent({ name: 'CountIncreased', properties: { count: count.value } })
}
```

### Custom Exceptions (Centralized Error Handler)

The centralized error handler at `src\FrontEnd.Nuxt\app\utils\errorHandler.ts` is the ideal place to add `trackException`. Every API error in the application flows through `handleApiError()`. Adding App Insights there gives structured exception telemetry in Azure for every error, not just console output.

The pattern to add to `handleApiError()`:

```ts
// At the top of handleApiError(), after the console.error call:
const { $appInsights } = useNuxtApp()
const appInsights = $appInsights as ApplicationInsights | undefined

if (appInsights && err instanceof Error) {
  appInsights.trackException({ exception: err })
}
```

This ensures every `ApiException`, `ProblemDetails`, and unexpected network error is captured as a structured exception in Application Insights with a full stack trace.

---

## Authenticated User Context

Currently the plugin sets a static `'anonymous'` context for all users. This means all telemetry in Azure is attributed to the same anonymous user, making it impossible to filter by user or correlate sessions.

### What data is available

The `useAuth()` composable (from `@sidebase/nuxt-auth`) exposes a `data` object with:

```ts
data.value?.id      // string — user GUID (always safe to log per logging policy)
data.value?.name    // string — username (production-restricted)
data.value?.email   // string — email address (production-restricted)
data.value?.roles   // string[]
```

The `useUserPreferencesStore()` exposes `getCurrentTenantKey` — the current tenant GUID (always safe to log).

### The fix: reactive watch

The plugin initializes once at app startup before the user logs in. The fix is to **watch the auth state reactively** and update the context when it changes. Replace the static call at line 75 of the plugin with:

```ts
// Watch auth state and update user context reactively
const { data, status } = useAuth()
const userPrefs = useUserPreferencesStore()

watch(
  [status, () => data.value?.id, () => userPrefs.getCurrentTenantKey],
  ([authStatus, userId, tenantKey]) => {
    if (authStatus === 'authenticated' && userId) {
      // Use user GUID and tenant GUID — both always safe per logging policy
      appInsights.setAuthenticatedUserContext(
        userId,
        tenantKey || undefined,
        true  // storeInCookie — persists across page loads
      )
    } else {
      appInsights.clearAuthenticatedUserContext()
    }
  },
  { immediate: true }
)
```

### What this gives you in Azure

Once set, every telemetry item (page views, AJAX calls, exceptions, custom events) carries:
- `user_AuthenticatedId` = the user's GUID
- `user_AccountId` = the tenant key GUID

This enables:
- Filter all telemetry for a specific user: `user_AuthenticatedId == "some-guid"`
- Filter by tenant: `user_AccountId == "tenant-guid"`
- See the full session journey of a specific user across page views and API calls
- Correlate frontend telemetry with backend telemetry (which already logs tenant/user GUIDs per the logging policy)

The `storeInCookie: true` parameter persists the user context in a cookie (`ai_authUser`) so it survives page refreshes. The cookie contains only the GUID — no PII concern.

> **Logging policy reminder:** User GUIDs and tenant GUIDs are always safe to log in any environment. Do **not** use `data.value?.email` or `data.value?.name` as the authenticated user ID in production — these are production-restricted per `docs\LOGGING-POLICY.md`.

---

## Capabilities Summary

| Capability | Status in this project |
|---|---|
| Auto route tracking | ✅ Automatic |
| AJAX/Fetch performance | ✅ Automatic |
| Unhandled promise rejections | ✅ Automatic |
| W3C distributed tracing | ✅ Automatic |
| Page visit time | ✅ Automatic |
| Custom events (`trackEvent`) | ✅ Used in `counter.vue` |
| Playwright guard | ✅ Implemented |
| Authenticated user context | ⚠️ Hardcoded to `'anonymous'` — fix described above |
| Custom exceptions (`trackException`) | ⚠️ Not yet wired into `errorHandler.ts` — fix described above |
| Custom metrics (`trackMetric`) | ❌ Not used |
