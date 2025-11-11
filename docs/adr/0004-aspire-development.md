# 0004. Use of .NET Aspire for Development Orchestration

Date: 2025-11-09

## Status

Accepted

## Context

Given the previous architectural decisions (SPA frontend with Nuxt, .NET backend with separate controllers), we need to choose a methodology to orchestrate the multiple components of this application during development.

### Previous Pain Points

In past projects, I've found other orchestration methods cumbersome:

* **Docker Compose** - Requires maintaining separate Dockerfiles and compose configurations, slower feedback loop, complexity in debugging across containers
* **Separate dev servers** - Running frontend dev server independently and proxying backend API calls is error-prone, requires manual coordination, inconsistent environment setup

### Requirements

Our development orchestration needs to:
- Run both .NET backend and Node.js frontend concurrently
- Provide easy service discovery between components
- Enable debugging across distributed components
- Support rapid iteration with hot reload
- Provide observability (logs, traces, metrics) during development

## Decision

We will use **.NET Aspire** for development orchestration. It is well-suited to managing the complexities of distributed applications and provides excellent developer experience for .NET-centric polyglot applications.

### Scope Limitation

**Important:** This decision applies **only to development orchestration**, not production deployment.

For production, we're leaning toward:
- Frontend: Static Web App (Azure Static Web Apps or CDN)
- Backend: Azure App Service or single-container Azure Container Apps

This separation allows us to:
- Reduce costs of running compute for the front-end container
- Optimize each component for its deployment target
- Keep deployment options flexible

## Consequences

### What Becomes Easier

1. **Unified Development Experience**
   - Single command (`dotnet run` from AppHost) starts entire application stack
   - Aspire Dashboard provides unified view of all services, logs, and telemetry
   - Consistent environment setup across team members

2. **Service Discovery**
   - Automatic service-to-service communication without hardcoded URLs
   - Frontend automatically knows backend URL in development
   - No need for manual proxy configuration

3. **Observability**
   - Built-in structured logging aggregation
   - Distributed tracing across frontend/backend boundaries
   - Health checks and metrics out of the box
   - OpenTelemetry integration

4. **Resource Management**
   - Can easily add dependencies (Redis, SQL Server, etc.) later
   - Resource lifecycle managed automatically
   - Clean shutdown of all services

5. **Debugging**
   - Attach debuggers to any service from Visual Studio/VS Code
   - Clear visualization of request flows
   - Easy access to logs and traces for troubleshooting

6. **Onboarding**
   - New developers can start the entire stack with minimal setup
   - Self-documenting through AppHost configuration
   - Reduces "works on my machine" issues

### What Becomes More Difficult

1. **Learning Curve**
   - Future contributors needs to understand Aspire concepts and dashboard
   - Additional mental model beyond just "run backend, run frontend"

2. **Tooling Requirements**
   - Requires .NET 8+ SDK even for developers primarily working on frontend
   - Visual Studio 2022 17.9+ or VS Code with C# Dev Kit recommended

3. **Configuration Complexity**
   - AppHost project adds another layer to maintain
   - Need to keep Aspire orchestration in sync with actual deployment architecture

4. **Deployment Divergence**
   - Development environment differs from production
   - Must ensure features work in both Aspire-orchestrated and standalone modes
   - Testing deployment configuration requires separate process

5. **Limited to .NET Ecosystem**
   - If we later add non-.NET services, Aspire support may be limited
   - Heavier dependency on Microsoft tooling

### Migration Path

If we decide to change this later:
- AppHost is separate project, can be removed without affecting application code
- Each service should still be runnable independently
- Can migrate back to Docker Compose or other orchestration if needed

## Additional Considerations

### Not Addressed in This Decision

1. **Integration Testing**
   - Should we use Aspire for integration test orchestration?
   - Consider separate ADR if integration tests need distributed service setup

2. **CI/CD Impact**
   - GitHub Actions workflows currently build/test without Aspire
   - Evaluate if CI should use Aspire for more realistic testing

3. **Production Aspire Hosting**
   - Keep monitoring Aspire hosting maturity
   - Revisit deployment decision in 6 months
   - Consider Azure Container Apps + Aspire manifest deployment

4. **Local Development Alternatives**
   - Document fallback for developers who can't/won't use Aspire
   - Ensure services can run standalone with environment variables

### Related Decisions

- [0001. Single Page Web App](0001-spa-web-app.md) - Established need for frontend/backend separation
- [0002. Vue.js](0002-vue-js.md) - Frontend framework choice
- [0003. Nuxt](0003-nuxt.md) - Frontend meta-framework choice
- [0006. Production Infrastructure](0006-production-infrastructure.md) - Production deployment differs from Aspire dev orchestration

### References

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Aspire AppHost Concepts](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview)
