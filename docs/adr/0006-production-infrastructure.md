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
- Compatible with client-side authentication using `@sidebase/nuxt-auth` local provider

**Backend**: Azure App Service (Basic B1 tier)
- Single container deployment
- Persistent storage for SQLite database (contemplated in [ADR 0005](./0005-database-backend.md))
- Cost-effective for low, steady traffic (~$13/month)
- Provides authentication endpoints for frontend

### Alternative Considered: Azure Container Apps

ACA was considered but rejected for cost reasons. At low volumes, the consumption plan still incurs minimum daily charges (~$12/month minimum). App Service B1 provides better value with predictable pricing for this use case.

## Consequences

**Easier**:
- Predictable, low monthly costs
- SQLite works naturally with App Service persistent storage
- Native HTTPS on custom domain for both frontend and backend
- Less configuration complexity than ACA
- `.PublishAsDockerFile()` in `AppHost.cs` already supports this
- Static site generation works with client-side authentication

**More Difficult**:
- Less auto-scaling capability (must manually scale App Service)
- Cannot easily scale to zero like ACA
- May need to migrate to ACA later if traffic patterns become highly variable
- Authentication state managed entirely client-side

## Authentication Compatibility

This infrastructure decision is compatible with the identity system outlined in [ADR 0008](./0008-identity.md). The `@sidebase/nuxt-auth` local provider works perfectly with static site generation by:

- Making direct HTTP calls to the ASP.NET Core backend authentication endpoints
- Handling JWT token storage and management client-side
- Providing secure session management without requiring server-side session storage
- Maintaining all the benefits of a battle-tested authentication library

The static frontend will authenticate against the App Service backend using standard REST API calls, keeping the architecture simple and cost-effective.

## Related Decisions

- [0004. Aspire Development](0004-aspire-development.md) - Development orchestration differs from production deployment
- [0005. Database Backend](0005-database-backend.md) - SQLite database stored on App Service persistent storage
- [0007. Proxy to backend or make direct calls?](0007-backend-proxy-or-direct.md) - Frontend-to-backend communication approach
- [0008. Identity](0008-identity.md) - Client-side authentication using local provider
