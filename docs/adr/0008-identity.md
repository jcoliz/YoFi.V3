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