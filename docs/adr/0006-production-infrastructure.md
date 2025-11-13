# 0006. Production infrastructure

Date: 2025-11-09

## Status

Accepted

## Context

### Question

What infrastructure should we deploy this app into in production?

### Requirements

1. Low-cost at low usage
2. Services based in Azure
3. Served on a custom domain with HTTPS certificate

### Composition

We will likely have just two components to the app itself.
* Back End .NET API service
* Front End Nuxt UI

### Possibilities

1. Azure Container Apps (ACA). Seems like the default way to deploy Aspire apps
2. Azure Kubernetes Service. Like ACA, only more complicated.
3. Azure Static Web Apps for the front-end, with ACA as a backend.
4. Azure Static Web Apps for the front-end, with Azure App Service as a backend.
5. Azure Blob Storage for the front-end. However, requirement #3 is not natively met by Azure Blob Storage. It requires Azure Front Door, which is kind of expensive

## Decision

**Frontend**: Azure Static Web Apps
- Nuxt static generation (`nuxt generate`)
- Includes custom domain + HTTPS
- Supports proxy to Azure App Service Backend (see [docs](https://learn.microsoft.com/en-us/azure/static-web-apps/apis-app-service))
- Linkage can be done [in ARM template](https://learn.microsoft.com/en-us/azure/templates/microsoft.web/staticsites/linkedbackends?pivots=deployment-language-bicep)
- Standard (not free) tier required for backend linkage
- Global CDN distribution

**Backend**: Azure App Service (Basic B1 tier)
- Single container deployment
- Persistent storage for SQLite database (contemplated in [ADR 0005](./0005-database-backend.md))
- Cost-effective for low, steady traffic (~$13/month)

### Alternative Considered: Azure Container Apps

ACA was considered but rejected for cost reasons. At low volumes, the consumption plan still incurs minimum daily charges (~$12/month minimum). App Service B1 provides better value with predictable pricing for this use case.

## Consequences

**Easier**:
- Simple deployment pipeline from GitHub Actions
- Predictable, low monthly costs
- SQLite works naturally with App Service persistent storage
- Native HTTPS on custom domain for both frontend and backend
- Less configuration complexity than ACA
- Can deploy directly from GitHub Actions
- `.PublishAsDockerFile()` in `AppHost.cs` already supports this

**More Difficult**:
- Less auto-scaling capability (must manually scale App Service)
- Cannot easily scale to zero like ACA
- May need to migrate to ACA later if traffic patterns become highly variable

### For future consideration

Should we use Azure Static Web Apps [linked backend](https://learn.microsoft.com/en-us/azure/static-web-apps/apis-app-service) feature, or directly call the backend from JS running in browser?

See [ADR 0007](./0007-backend-proxy-or-direct.md) for a deeper discussion.

## Related Decisions

- [0004. Aspire Development](0004-aspire-development.md) - Development orchestration differs from production deployment
- [0005. Database Backend](0005-database-backend.md) - SQLite database stored on App Service persistent storage
- [0007. Proxy to backend or make direct calls?](0007-backend-proxy-or-direct.md) - Frontend-to-backend communication approach

## Concerns identified

There is some conflict between the approach documented here of using static site generation, and the system I'm evaluating for identity, in [0008. Identity](./0008-identity.md).

Question: "Will this identity system work with static site generation??"

Answer: **No, this design will NOT work with static site generation (SSG)**. Here's why and what you can do about it:

## Why It Won't Work with SSG

### 1. Server-Side Auth Handler
Your design includes this server-side authentication handler:

````typescript
export default NuxtAuthHandler({...})
````

**Problem**: SSG pre-generates all pages at build time. There's no server to run the auth handler.

### 2. Runtime Environment Variables
Your config uses runtime secrets:

````typescript
runtimeConfig: {
  authSecret: process.env.NUXT_AUTH_SECRET, // ❌ Not available in SSG
}
````

**Problem**: Runtime config requires a server to provide values at request time.

### 3. Session Management
`@sidebase/nuxt-auth` relies on server-side session handling, which requires a running server.

## Solutions

### Option 1: Use SSR/SPA Mode Instead
**Recommended for your use case**

````typescript
export default defineNuxtConfig({
  ssr: true, // ✅ Server-side rendering (hybrid)
  // OR
  ssr: false, // ✅ Single Page App mode
  
  modules: ['@sidebase/nuxt-auth'],
  // ... rest of your config
})
````

**Why this works better:**
- ✅ Server can handle authentication
- ✅ Dynamic user sessions
- ✅ Real-time authorization checks
- ✅ Your current design works as-is

### Option 2: Client-Side Only Auth (for SSG)
If you must use SSG, you'd need to completely redesign the auth:

````typescript
export default defineNuxtConfig({
  ssr: false, // SPA mode
  nitro: {
    preset: 'static' // Static generation
  },
  
  // Remove @sidebase/nuxt-auth
  // modules: ['@sidebase/nuxt-auth'], // ❌ Remove this
  
  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL
    }
  }
})
````

Then implement client-side auth manually:

````typescript
export const useAuth = () => {
  const user = ref(null)
  const token = useCookie('auth-token', { 
    secure: true, 
    httpOnly: false, // Must be false for client access
    sameSite: 'strict'
  })

  const login = async (credentials) => {
    const response = await $fetch('/api/auth/login', {
      method: 'POST',
      body: credentials,
      baseURL: useRuntimeConfig().public.apiBaseUrl
    })
    
    if (response.token) {
      token.value = response.token
      user.value = response.user
      await navigateTo('/dashboard')
    }
  }

  const logout = () => {
    token.value = null
    user.value = null
    navigateTo('/login')
  }

  return { user, login, logout, token }
}
````

### Option 3: Hybrid Approach
Use Nuxt's hybrid rendering for different page types:

````typescript
export default defineNuxtConfig({
  nitro: {
    routeRules: {
      // Static pages
      '/': { prerender: true },
      '/about': { prerender: true },
      '/pricing': { prerender: true },
      
      // Dynamic auth-required pages
      '/dashboard/**': { ssr: true },
      '/accounts/**': { ssr: true },
      '/login': { ssr: true },
    }
  },
  
  modules: ['@sidebase/nuxt-auth'],
  // ... your current config
})
````

## Recommendation for Your Project

Based on your identity design document, **I recommend Option 1 (SSR/SPA mode)** because:

1. **Your design is sophisticated** - Claims-based auth, JWT tokens, real-time updates
2. **Financial app** - Likely needs dynamic, personalized content anyway
3. **User-specific data** - Account access, transactions, preferences
4. **Security** - Server-side session validation is more secure

### Why SSG Doesn't Fit Your Use Case

Your app appears to be:
- ✅ **User-centric** (personal financial data)
- ✅ **Dynamic content** (transactions, accounts)
- ✅ **Authenticated workflows** (login required)
- ✅ **Real-time features** (claims updates)

SSG is better for:
- ❌ **Content sites** (blogs, marketing pages)
- ❌ **Public content** (documentation, landing pages)
- ❌ **Minimal interactivity** (mostly read-only)

## Quick Fix

If you want to keep your current design, just ensure SSR is enabled:

````typescript
export default defineNuxtConfig({
  ssr: true, // ✅ Add this line
  modules: ['@sidebase/nuxt-auth'],
  // ... rest of your current config stays the same
})
````

Your authentication design is excellent for a server-rendered application - it just needs a server to work! 🚀