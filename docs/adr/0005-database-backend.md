# 0005. Database backend

Date: 2025-11-09

## Status

Accepted

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

#### 1. SQLite (Local development, initial production testing)
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

#### 2. PostgreSQL Everywhere (Scale-up plan)
**Pros:**
- Single implementation
- Azure Database for PostgreSQL Flexible Server available
- Container support via Aspire orchestration
- More features than SQL Server for similar cost

**Cons:**
- Requires containers for local development
- Slightly more complex setup

#### 3. Azure SQL Server (Fallback)
**Pros:**
- Known quantity from previous projects
- Proven to work in Azure App Services
- $5/month basic tier

**Cons:**
- Would still need separate solution for CI/CD
- Multiple data layer implementations

## Decision

1. **Primary approach**: Use SQLite for local development & CI.
2. **Production deployment**: Test SQLite Azure App Service using Azure Storage mounts during intial pre-releases stages. Determine performance limits of this system.
3. **Scale-up plan**: As scale grows beyond capability of SQlite in production, prepare to migrate to PostgreSQL Everywhere. If this presents too many hurdles,  fall back to proven capability of Azure SQL Server
4. **Migration strategy**: Accept that we may need multiple providers as the application scales, as this promotes proper layer decoupling.
5. **Container Matching**: Whatever backend we land on in production, migrate the containerized environment (including CI) to that technology, to catch issues early.

## Consequences

### Positive
- Single codebase for all environments
- Simplified development setup - no containers required locally
- Zero database costs in production initially
- Easy CI/CD with no external dependencies

### Negative
- May need to migrate database provider as application scales
- Requires careful management of SQLite limitations (migrations, concurrent access)
- Need to explore and configure persistent storage mounting in Azure App Service

### Mitigation
- Monitor application performance and concurrent usage patterns
- Plan for database provider migration if scaling requirements change
- Document Azure Storage mount configuration for production deployment

## Related Decisions

- [0006. Production Infrastructure](0006-production-infrastructure.md) - Azure App Service provides persistent storage for SQLite database
