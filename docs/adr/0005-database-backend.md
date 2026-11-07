# 0005. Database backend

Date: 2025-11-09

## Status

Accepted (updated November 2026 to adopt PostgreSQL in all environments).

## Context

Need a database backend to store persistent app data.

### Requirements

1. Low-cost in production for low volumes initially
2. Easy to use during development
3. Can run in Azure Pipelines
4. Easy to use from Azure App Services
5. Scale-ready for higher volumes

### Previous solution

In my last project, I used Azure SQL Server for production ($5/month) and Postgres in containers for development and CI/CD. This worked but required maintaining two separate data implementations and configuration to select the right data layer.

### Options Considered

#### 1. SQLite (Previous approach, not selected)

**Pros:**

- Single implementation - works everywhere (dev, CI/CD, production)
- Zero cost - file-based, no server needed
- Trivial in Azure Pipelines - no container needed
- EF Core support - same migrations/LINQ as SQL Server
- Perfect for low volumes

**Cons:**

- No concurrent writes at scale
- Limited for multi-server scenarios
- Requires persistent storage configuration in Azure App Service
- [Migration limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations#migrations-limitations)

#### 2. PostgreSQL Everywhere (Selected)

**Pros:**

- Single implementation
- Azure Database for PostgreSQL Flexible Server available
- Container support via Aspire orchestration
- More features than SQL Server for similar cost

**Cons:**

- Requires containers for local development
- Slightly more complex setup

#### 3. Azure SQL Server (Not selected)

**Pros:**

- Known quantity from previous projects
- Proven to work in Azure App Services
- $5/month basic tier

**Cons:**

- Would still need separate solution for CI/CD
- Multiple data layer implementations

## Decision

1. **Primary approach**: Use PostgreSQL in all environments through [`Data.PostgreSQL`](../../src/Data/PostgreSQL/), with Entity Framework Core and Npgsql. Identity and business data share one database.
2. **Development and container testing**: Aspire manages PostgreSQL for local development; Docker Compose provides PostgreSQL for the application container environment.
3. **Integration testing**: Data, Application, and Controller integration tests use PostgreSQL Testcontainers with isolated databases and real EF migrations. Docker must be running.
4. **Production deployment**: Use Azure Database for PostgreSQL Flexible Server, with system-assigned managed-identity authentication from App Service. Provision database permissions and network connectivity before deployment.
5. **Migration lifecycle**: Non-production backend startup applies EF migrations before Identity seeding. Production migrations must be applied by deployment tooling before backend startup.
6. **Provider boundary**: Keep PostgreSQL-specific configuration, SQL, and migrations in Data. Application Features depend on Entities data-access abstractions, not Npgsql or EF Core. The legacy provider is retained only temporarily pending removal, not as a supported runtime alternative.

See [ENVIRONMENTS.md](../ENVIRONMENTS.md) for connection-string configuration and
[ADR 0006](0006-production-infrastructure.md) for production infrastructure requirements.

## Consequences

### Positive

- One database provider across development, container testing, integration tests, and production
- Real-database tests exercise PostgreSQL constraints, timestamp precision, queries, and migrations
- Database persistence is independent of the backend filesystem and deployments
- Managed-identity authentication avoids a static production database password

### Negative

- Docker is required for local database orchestration and integration tests
- PostgreSQL adds production provisioning, networking, permissions, and ongoing service costs
- Production schema migrations must be coordinated with application deployments
- Provider-specific behavior still requires explicit validation, including enforced lengths, numeric precision, and UTC timestamps

### Mitigation

- Check Docker availability before integration test runs
- Use isolated PostgreSQL test databases with real migrations to catch persistence differences early
- Configure managed-identity access and network connectivity, and verify migrations before production deployment
- Monitor database capacity and concurrent workload as usage grows

## Related Decisions

- [0006. Production Infrastructure](0006-production-infrastructure.md) - App Service backend with PostgreSQL Flexible Server
- [0011. Clean Architecture](0011-clean-architecture.md) - Database-specific infrastructure behind Entities abstractions
