# 0005. Database backend

Date: 2025-11-09

## Status

In progress

## Context

Need a database backend to store persistent app data.

### Requirements

1. Low-cost in production for low volumes
2. Easy to use during development
3. Can run in Azure Pipelines
4. Easy to use from Azure App Services

### Previous solution

In my last project, here's what I did.

For production, I used Azure SQL Server. The lowest possible cost tier is $5/month, which is totally reasonable.

The big problem is that this did not work well for functional testing in Azure Pipelines. For that case, I used Postgres in a container. I could easily add a Postgres container to the
docker compose project which is running in Azure Pipelines. This worked great.

For local development, I decided to use the same Postgres setup. It's easy enough to run a container locally, and will be even easier with Aspire.

The problem with this approach is I've got two separate data implementations, which requires configuration to select the right data layer at the right time. And it also requires maintaining two data layers.

## Additional Options to Consider

I asked Claude, "Please review this in-progress ADR. Do you have any other suggestions to consider for my database backend which would meet my requirements?"

GitHub Copilot: Great ADR! Let me suggest some additional options to consider:

### 1. **SQLite (Recommended for your use case)**

**Pros:**
- ✅ **Single implementation** - Works everywhere (dev, CI/CD, production)
- ✅ **Zero cost** - File-based, no server needed
- ✅ **Trivial in Azure Pipelines** - No container needed, just runs
- ✅ **Easy in App Services** - Just a file on disk
- ✅ **EF Core support** - Same migrations/LINQ as SQL Server
- ✅ **Perfect for low volumes** - Handles millions of rows fine
- ✅ **Aspire-ready** - No orchestration needed

**Cons:**
- ❌ No concurrent writes at scale (but fine for single-user/low traffic)
- ❌ Limited for multi-server scenarios (but Azure App Service can use shared storage)
- ❌ Fewer advanced SQL features

**For your scenario:** This seems ideal - one codebase, works everywhere, perfect for low-volume personal finance app.

### 2. **PostgreSQL Everywhere (Simplify your previous approach)**

**Pros:**
- ✅ **Single implementation**
- ✅ **Free tier in Azure** - Azure Database for PostgreSQL Flexible Server has a burstable tier (~$12/month, sometimes free credits)
- ✅ **Container in dev/CI** - Aspire can orchestrate this
- ✅ **More features than SQL Server** for same/lower cost

**Cons:**
- ❌ Still need a container locally
- ❌ Slightly more expensive than Azure SQL Basic tier

**Modern approach with Aspire:**
```csharp
// In AppHost.cs
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("yofidb");

var apiService = builder.AddProject<Projects.YoFi_V3_BackEnd>("backend")
    .WithReference(postgres);
```

### 3. **Azure Cosmos DB (NoSQL option)**

**Pros:**
- ✅ **Free tier** - 1000 RU/s and 25GB storage free forever
- ✅ **Serverless option** - Pay per request (could be nearly free at low volumes)
- ✅ **Local emulator** - Great for dev/testing
- ✅ **Works in CI** - Emulator in container

**Cons:**
- ❌ Different data model (document-based)
- ❌ May be overkill for traditional finance data
- ❌ Different query language (SQL-like but not SQL)

### 4. **SQL Server LocalDB/Express + Azure SQL**

**Pros:**
- ✅ **LocalDB for dev** - Embedded, zero config
- ✅ **SQL Server Express in container** for CI
- ✅ **Azure SQL** for production - You already know this works
- ✅ **Single EF Core provider**

**Cons:**
- ❌ Still multiple configurations
- ❌ Container needed for CI

### 5. **Turso/libSQL (Modern SQLite)**

**Pros:**
- ✅ SQLite-compatible with edge replication
- ✅ Free tier available
- ✅ Distributed SQLite
- ✅ Works everywhere SQLite works

**Cons:**
- ❌ Newer/less mature
- ❌ May need custom provider

## My Recommendation

Given your requirements, I'd suggest **one of two paths**:

### **Option A: SQLite (Best fit)**
- **Local dev**: SQLite file in bin or `App_Data/`
- **CI/CD**: SQLite in-memory or file
- **Production**: SQLite on App Service persistent storage or Azure Files

This eliminates your "two implementations" problem entirely. For a personal finance app with low volume, SQLite is totally viable in production.

### **Option B: PostgreSQL Everywhere (If you need more power)**
- **Local dev**: Aspire-orchestrated Postgres container
- **CI/CD**: Postgres container in pipeline
- **Production**: Azure Database for PostgreSQL Flexible Server (Burstable tier)

This is your previous approach but **without SQL Server**, so one implementation.

## Recommendation

Use **SQLite** as the single database implementation across all environments.

- **Development**: SQLite file (`yofi.db`) in local App_Data folder
- **CI/CD**: SQLite in-memory database (`:memory:`)
- **Production**: SQLite file on Azure App Service persistent storage

Use Entity Framework Core with the SQLite provider for a single, consistent data access layer.

## Consequences

**Positive:**
- Single data implementation to maintain
- No configuration switching between environments
- Zero cost for database infrastructure
- Trivial setup in all environments
- Fast integration tests (in-memory mode)
- Easy backup (just copy the .db file)
- Works great for Aspire development

**Negative:**
- Limited to single-server scenarios (acceptable for current scale)
- May need migration path if app grows significantly
- Fewer advanced SQL features (but EF Core abstracts most of this)

**Mitigation:**
- EF Core abstractions mean switching to PostgreSQL/SQL Server later is straightforward
- Can use Azure Files for SQLite file to enable some multi-instance scenarios if needed

What do you think? Does SQLite meet your needs, or do you need the scalability of PostgreSQL/SQL Server?

## Decision

What is the change that we're proposing and/or doing?

## Consequences

What becomes easier or more difficult to do because of this change?
