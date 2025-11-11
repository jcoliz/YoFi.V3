# 0007. Proxy to backend or make direct calls?

Date: 2025-11-11

## Status

Draft

## Context

### Question

Should we use Azure Static Web Apps [linked backend](https://learn.microsoft.com/en-us/azure/static-web-apps/apis-app-service) feature, or directly call the backend from JS running in browser?

### Background

As documented in [ADR 0006](./0006-production-infrastructure.md), I'm planning to use Azure Static Web Apps for the frontend and Azure App Service for the backend.

## Recommendation

Prompt: "Consider this ADR. Would it be better to use the backend proxy provided by Azure Static Web apps, or instead to directly call backend from the frontend JS code?"

I'll help you analyze the tradeoffs between using the Azure Static Web Apps backend proxy versus direct backend calls from the frontend. Let me first gather some context about your current setup. Based on my analysis of your codebase and the ADR, here's my assessment:

### Analysis: Backend Proxy vs. Direct API Calls

#### Current Situation
Your Nuxt frontend currently uses **proxying** (in `nuxt.config.ts`) to route `/api/**` requests to the backend during development. The API client in `apiclient.ts` makes relative calls (e.g., `/api/Weather`).

#### Recommendation: **Direct API Calls** ✅

**You should call the backend directly from the frontend JS code.** Here's why:

#### Cost Savings
- **Azure Static Web Apps Free Tier**: Supports custom domains with HTTPS
- **Eliminates Standard tier** requirement (~$9/month savings)
- Standard tier is **only** needed for the managed backend integration feature

#### Technical Advantages
1. **CORS is straightforward**: Just configure CORS in your .NET backend (`Program.cs`)
2. **Better for static generation**: When you run `nuxt generate`, the site is truly static—no server-side proxying needed
3. **Simpler architecture**: Frontend and backend are independently deployable
4. **Better observability**: Direct calls make it easier to trace requests
5. **CDN-friendly**: Static Web Apps CDN works optimally with static content

#### Implementation Changes Needed

````typescript
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },

  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL || 'http://localhost:5379'
    }
  },

  // ...existing code...

  // Remove the proxy configuration
  // routeRules: {
  //   '/api/**': { proxy: `http://localhost:5379/api/**` }
  // }
})
````

````typescript
<script setup lang="ts">
import { ref, onMounted } from "vue"
import * as api from "../utils/apiclient"

const config = useRuntimeConfig()

// ...existing code...

// Pass the base URL to the client
const client = new api.WeatherClient(config.public.apiBaseUrl)

// ...existing code...
</script>
````

#### Backend CORS Configuration

````csharp
// ...existing code...

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",  // Local dev
            "https://your-custom-domain.com"  // Production
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

// ...existing code...

app.UseCors();

// ...existing code...
````

#### Deployment Configuration
Set `NUXT_PUBLIC_API_BASE_URL` environment variable in Azure Static Web Apps to point to your App Service backend URL.

#### The Only Downside
You lose the "single URL" convenience—users see the backend API URL in network requests. However, this is a **minor** concern compared to the cost savings and architectural benefits.

#### Updated ADR Recommendation
I'd suggest **removing** the "For future consideration" section and making direct API calls the **primary decision** to maximize cost efficiency.

## Decision

The decision is still open, pending research of GitHub Copilot's recommendation.

## Consequences

What becomes easier or more difficult to do because of this change?

## Related Decisions

- [0006. Production Infrastructure](0006-production-infrastructure.md) - Production infrastructure context for this decision
