# Environments

The application is built to run in three distinct environments:

* Local: For local development
* Container: Primarily for quick build/execution of functional tests in CI pipeline
* Production: Running in Azure

## Local

As described in [ADR 0004](./adr/0004-aspire-development.md), local development is done using .NET Aspire.

**Architecture:**
- Frontend runs in Node.js dev server (via npm) with hot module replacement
- Backend runs as .NET API service
- Orchestrated by Aspire AppHost

**Frontend-to-Backend Communication:**
- Nuxt proxy configured in `nuxt.config.ts` routes `/api/**` to backend
- Service discovery handled by Aspire
- Frontend makes relative API calls (e.g., `/api/Weather`)

**How to run:**
```powershell
cd src/AppHost
dotnet run
```

Then open the Aspire Dashboard URL shown in the console.

## Container

The application can be packaged into containers and orchestrated with a [Docker Compose project](../docker/docker-compose-ci.yml).

**Use cases:**
- Run functional tests in CI pipeline
- Run functional tests locally with ease
- Distribute the entire application via DockerHub for evaluation

**Architecture:**
- Frontend generated as static site using `nuxt generate`
- Frontend served by nginx
- Backend runs as containerized .NET API
- Orchestrated via [docker compose](../docker/docker-compose-ci.yml)

**Frontend-to-Backend Communication:**
- Static site built with `NUXT_PUBLIC_API_BASE_URL` baked in at build time
- Configured via Docker build arg to point to backend container
- Direct API calls from browser JavaScript

**How to run:**
```powershell
.\scripts\Build-Container.ps1
.\scripts\Start-Container.ps1
```

Frontend available at http://localhost:5000, Backend at http://localhost:5001.

Run functional tests with `.\scripts\Run-FunctionalTestsVsContainer.ps1`.

## Production

As described in [ADR 0006](./adr/0006-production-infrastructure.md) and [ADR 0007](./adr/0007-backend-proxy-or-direct.md), the production application is hosted on Azure services.

**Architecture:**
- Frontend: Azure Static Web Apps (static site from `nuxt generate`)
- Backend: Azure App Service (.NET Web API)
- Deployed via [Azure Pipelines](../.azure/pipelines/ci.yaml). Note this is the **ONLY** supported methodology to deploy bits.

**Frontend-to-Backend Communication:**
- Static site built with `NUXT_PUBLIC_API_BASE_URL` set to production backend URL
- Direct API calls from browser JavaScript
- CORS configured on backend to allow Static Web App origin

**Environment Variables:**
- `NUXT_PUBLIC_API_BASE_URL`: Set during build to production backend URL (e.g., `https://yofi-backend.azurewebsites.net`)

## Summary Table

| Aspect | Local | Container | Production |
|--------|-------|-----------|------------|
| Frontend host | Node dev server | nginx (static) | Azure Static Web Apps |
| Backend host | .NET process | Docker container | Azure App Service |
| Frontend build | `npm run dev` | `npm run generate` | `npm run generate` |
| API calls | Proxied via Nuxt | Direct (baked URL) | Direct (baked URL) |
| Orchestration | .NET Aspire | Docker Compose | Azure |
| Service discovery | Aspire | Docker network | DNS |
