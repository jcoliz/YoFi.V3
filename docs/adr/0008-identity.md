# 0008. Identity system

Date: 2025-11-13

## Status

Accepted

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

Based on the existing architecture (Nuxt 4 + ASP.NET Core + direct API calls) and the infrastructure decision to use static site generation ([ADR 0006](./0006-production-infrastructure.md)), we need a client-side authentication solution that works with static hosting.

## Decision

### Client-Side Authentication with ASP.NET Core Identity + @sidebase/nuxt-auth Local Provider

Given the technology stack and static hosting requirements, implement:

#### Frontend (Nuxt 4 - Static Generated)
- **Use [@sidebase/nuxt-auth](https://auth.sidebase.com/) with local provider** - Client-side authentication
- Configure to work with ASP.NET Core backend authentication endpoints
- JWT token management handled client-side with secure cookie storage

#### Backend (ASP.NET Core)
- **Use ASP.NET Core Identity** with JWT tokens for stateless authentication
- Provide REST API endpoints for login, logout, registration, and profile
- This gives battle-tested auth components while maintaining the direct API call architecture

#### Identity Provider
- **Start with built-in authentication** (username/password stored in SQLite database)
- **Future-proof for external providers** (Microsoft Entra ID, Google, etc.) using ASP.NET Core Identity's external login providers

### Configuration Example

````typescript
// nuxt.config.ts
export default defineNuxtConfig({
  ssr: false, // SPA mode for static generation
  nitro: {
    preset: 'static'
  },
  
  modules: ['@sidebase/nuxt-auth'],
  
  auth: {
    providers: {
      local: {
        type: 'local',
        endpoints: {
          signIn: { path: '/login', method: 'post' },
          signOut: { path: '/logout', method: 'post' },
          signUp: { path: '/register', method: 'post' },
          getSession: { path: '/profile', method: 'get' }
        },
        pages: {
          login: '/login'
        },
        token: {
          signInResponseTokenPointer: '/token',
          type: 'Bearer',
          cookieName: 'auth.token',
          headerName: 'Authorization',
          headerType: 'Bearer',
          cookieMaxAge: 60 * 60 * 24 * 30 // 30 days
        },
        sessionDataType: { id: 'string', email: 'string', name: 'string' }
      }
    }
  },
  
  runtimeConfig: {
    public: {
      authBaseURL: process.env.NUXT_PUBLIC_API_BASE_URL || 'http://localhost:5000/api/auth'
    }
  }
})
````

### Addressing Specific Questions

1. **NuxtAuth?** → Use **@sidebase/nuxt-auth with local provider** (modern, works with static generation)

2. **ASP.NET Identity?** → **Yes, absolutely**. It's mature, well-tested, and provides the API endpoints for the frontend

3. **External providers?** → **Later**. Start with built-in, add external providers when needed using ASP.NET Core's external login system

4. **Authorization?** → Use **ASP.NET Core's policy-based authorization** with custom claims for account access. Store user-to-account mappings in your database using the three-role model (Owner/Editor/Viewer) as defined in ADR 0009.

5. **Examples?** → The local provider pattern is common for SPA + API architectures. JWT tokens bridge frontend and backend auth

### Why This Approach Works Well

✅ **Security**: ASP.NET Core Identity is battle-tested with regular security updates  
✅ **Familiar**: You already know ASP.NET Core Identity well  
✅ **Stateless**: JWT tokens work perfectly with your direct API call architecture  
✅ **Static Hosting Compatible**: No server-side session handling required  
✅ **Cost Effective**: Works with Azure Static Web Apps infrastructure  
✅ **Extensible**: Easy to add external providers later  
✅ **Single Database**: User data stays in your SQLite database with your business data  
✅ **Authorization Ready**: Built-in support for roles and custom claims for account-level permissions  

## Consequences

### What becomes easier:

- **Security**: ASP.NET Core Identity is battle-tested with regular security updates
- **Familiarity**: Leverages existing ASP.NET Core Identity knowledge
- **Stateless Architecture**: JWT tokens work perfectly with direct API call architecture
- **Static Hosting**: Compatible with Azure Static Web Apps cost-effective infrastructure
- **Extensibility**: Easy to add external providers later
- **Single Database**: User data stays in SQLite database with business data
- **Authorization**: Built-in support for roles and custom claims using the AccountView/AccountEdit/AccountOwn policies defined in ADR 0009

### What becomes more complex:

- **Client-Side Token Management**: All authentication state managed in browser
- **Token Refresh**: Need to implement JWT token refresh logic client-side
- **Custom Claims**: Account-level authorization requires custom implementation
- **Security Considerations**: Tokens stored client-side (though mitigated by secure cookies and short expiry)
- **Testing**: Authentication flows need to be tested across both frontend and backend

### Future Considerations:

- External identity providers can be added incrementally
- Session management handled by @sidebase/nuxt-auth local provider
- Authorization policies can be extended for more granular permissions
- Consider token refresh strategies for long-lived sessions

## Implementation

Detailed implementation guidance can be found in IDENTITY-DESIGN.md.

## Architecture Compatibility

This identity design is fully compatible with ADR 0006 (Production Infrastructure):

- ✅ **Static Site Generation**: Local provider works without server-side handlers
- ✅ **Azure Static Web Apps**: Client-side authentication compatible
- ✅ **Cost Effective**: No additional hosting costs for authentication
- ✅ **Direct API Calls**: Maintains the established architecture pattern

The frontend will authenticate against the App Service backend using standard REST API calls, keeping the architecture simple, secure, and cost-effective.

## Related Decisions

- [ADR 0009: Multi-tenancy and Account Model](0009-accounts-and-tenancy.md) - Defines the account structure, roles, and authorization policies that this identity system implements
- [ADR 0006: Production Infrastructure](0006-production-infrastructure.md) - Azure infrastructure decisions
- [ADR 0005: Database Backend](0005-database-backend.md) - SQLite database for storing user and account data
