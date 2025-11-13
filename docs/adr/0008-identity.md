# 0008. Identity system

Date: 2025-11-13

## Status

In Review

## Context

### Question

What software components and/or possible identity providers should we use for identity authentication and authorization?

In the past, I have rolled my own custom system. While I am happy with this, I've also received feedback that this is an unsafe practice, and should attempt to use as many off-the-shelf components as possible which have more security reviewers looking at them.

Some questions that come to mind:

* For the front-end, should I use NuxtAuth? This was recommended.
* ASP.NET includes some identity components. Can I leverage those? I know them well. Can I combine them with NuxtAuth??
* Should I use an external identity providers?
* How do I handle authorization? Certain users will have access to certain accounts, and this is information that is unique to the app, so I'll have to store it here.
* Are there any examples of good existing systems that combine Nuxt frontend with ASP.NET backend for identity?

### Analysis

Based on the existing architecture (Nuxt 4 + ASP.NET Core + direct API calls), this is a well-structured ADR that correctly identifies the key architectural questions around authentication and authorization.

## Decision

### Hybrid Approach with ASP.NET Core Identity + @sidebase/nuxt-auth

Given the technology stack and requirements, implement:

#### Frontend (Nuxt 4)
- **Use [@sidebase/nuxt-auth](https://auth.sidebase.com/)** - The modern successor to NuxtAuth for Nuxt 3/4
- Configure it to work with the ASP.NET Core backend as a custom provider

#### Backend (ASP.NET Core)
- **Use ASP.NET Core Identity** with JWT tokens for stateless authentication
- This gives battle-tested auth components while maintaining the direct API call architecture

#### Identity Provider
- **Start with built-in authentication** (username/password stored in SQLite database)
- **Future-proof for external providers** (Microsoft Entra ID, Google, etc.) using ASP.NET Core Identity's external login providers

### Addressing Specific Questions

1. **NuxtAuth?** → Use **@sidebase/nuxt-auth** instead (modern, actively maintained for Nuxt 3/4)

2. **ASP.NET Identity?** → **Yes, absolutely**. It's mature, well-tested, and integrates perfectly with your stack

3. **External providers?** → **Later**. Start with built-in, add external providers when needed using ASP.NET Core's external login system

4. **Authorization?** → Use **ASP.NET Core's policy-based authorization** with custom claims for account access. Store user-to-account mappings in your database

5. **Examples?** → The combination is common. Key is using JWT tokens as the bridge between frontend and backend auth

### Why This Approach Works Well

✅ **Security**: ASP.NET Core Identity is battle-tested with regular security updates  
✅ **Familiar**: You already know ASP.NET Core Identity well  
✅ **Stateless**: JWT tokens work perfectly with your direct API call architecture  
✅ **Extensible**: Easy to add external providers later  
✅ **Single Database**: User data stays in your SQLite database with your business data  
✅ **Authorization Ready**: Built-in support for roles and custom claims for account-level permissions  

## Consequences

### What becomes easier:

- **Security**: ASP.NET Core Identity is battle-tested with regular security updates
- **Familiarity**: Leverages existing ASP.NET Core Identity knowledge
- **Stateless Architecture**: JWT tokens work perfectly with direct API call architecture
- **Extensibility**: Easy to add external providers later
- **Single Database**: User data stays in SQLite database with business data
- **Authorization**: Built-in support for roles and custom claims for account-level permissions

### What becomes more complex:

- **Initial Setup**: Requires configuration of both frontend and backend auth systems
- **Token Management**: Need to handle JWT token lifecycle and refresh
- **Custom Claims**: Account-level authorization requires custom implementation
- **Testing**: Authentication flows need to be tested across both frontend and backend

### Future Considerations:

- External identity providers can be added incrementally
- Session management handled by @sidebase/nuxt-auth
- Authorization policies can be extended for more granular permissions

## Implementation

Detailed implementation guidance can be found in [IDENTITY-DESIGN.md](../IDENTITY-DESIGN.md).

## Architecture Conflict

Looking at the conflict between [AADR 0006 (Production Infrastructure)](./0006-production-infrastructure.md) and ADR 0008 (identity), there's a fundamental incompatibility:

- **ADR 0006** assumes **static site generation** for the frontend
- **ADR 0008** requires **server-side rendering** for the authentication system

**ADR 0006 Infrastructure Decision:**
```
Frontend: Azure Static Web Apps
- Nuxt static generation (nuxt generate)
```

**ADR 0008 Identity Requirement:**
```typescript
// This needs a server to run
export default NuxtAuthHandler({...})
```

Static sites can't run server-side code, so `@sidebase/nuxt-auth` won't work.

### My current plan

I will build the identity backend out *as if* we were going to use `@sidebase/nuxt-auth`. This allows me to defer this decision.

### Recommended Resolution Options

#### Option 1: Update ADR 0006 (Recommended)

**Change the frontend infrastructure to support SSR:**

````typescript
// Update ADR 0006
Frontend: Azure Container Apps or Azure App Service
- Nuxt SSR mode (not static generation)
- Can run server-side authentication
- Still supports custom domain + HTTPS
````

**Why this is better:**
- ✅ Keeps the sophisticated identity design from ADR 0008
- ✅ Financial apps benefit from server-side rendering anyway (SEO, security)
- ✅ User-specific content is inherently dynamic
- ✅ Claims-based authorization works as designed

**Updated Infrastructure:**

```yaml
Frontend: Azure Container Apps (Nuxt SSR)
- Consumption plan scales to zero when not used
- Can run @sidebase/nuxt-auth server handlers
- Custom domain + HTTPS supported
- Better for dynamic, authenticated content

Backend: Azure App Service (unchanged)
- API remains the same
- JWT validation works as designed
```

#### Option 2: Simplify ADR 0008

**Change to client-side only authentication:**

````typescript
// Simplified auth without @sidebase/nuxt-auth
// Direct API calls to ASP.NET Core backend
// Manual token management in Nuxt
````

**Consequences:**
- ❌ Lose sophisticated session management
- ❌ More custom auth code to maintain
- ❌ Less secure (tokens stored client-side only)
- ✅ Keep static site generation
- ✅ Lower hosting costs

#### Option 3: Hybrid Approach

**Use different rendering for different page types:**

````typescript
// nuxt.config.ts
export default defineNuxtConfig({
  nitro: {
    routeRules: {
      // Static marketing pages
      '/': { prerender: true },
      '/about': { prerender: true },
      '/pricing': { prerender: true },
      
      // SSR for authenticated areas
      '/login': { ssr: true },
      '/dashboard/**': { ssr: true },
      '/accounts/**': { ssr: true },
    }
  }
})
````

**Infrastructure:**
- Static marketing pages → Azure Static Web Apps
- Authenticated app → Azure Container Apps
- Backend API → Azure App Service

### My Recommendation: Option 1

**Update ADR 0006 to use Azure Container Apps for the frontend.**

#### Why This Makes Sense

1. **Cost is comparable** - ACA consumption plan can scale to zero
2. **Better fit for the app** - Financial apps are inherently user-specific and dynamic
3. **Security** - Server-side auth is more secure for financial data
4. **Future-proof** - Easier to add features like real-time updates, background jobs, etc.

### Updating ADR 0006...

````markdown
#### Decision

**Frontend**: Azure Container Apps (Consumption Plan)
- Nuxt SSR mode (`ssr: true`)
- Server-side authentication handlers
- Scales to zero when not used
- Custom domain + HTTPS supported
- Supports @sidebase/nuxt-auth

**Backend**: Azure App Service (Basic B1 tier) - unchanged
- Single container deployment
- Persistent storage for SQLite database
- JWT token validation

#### Why the Change

Initial decision for static generation was made before identity 
requirements were fully understood. Financial applications with 
user authentication benefit more from server-side rendering for:
- Security (server-side session management)
- Performance (user-specific data)
- Functionality (real-time features)
````

#### Action Items

1. **Update ADR 0006** to reflect the infrastructure change
2. **Mark the conflict as resolved** in ADR 0008
3. **Update deployment configuration** to use SSR instead of static generation
4. **Adjust cost projections** (ACA consumption vs Static Web Apps)

This resolution maintains the security and functionality benefits of your identity design while using appropriate Azure services for a dynamic, authenticated application.