# 0006. Production infrastructure

Date: 2025-11-09

## Status

Approved

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
