# YoFi.V3 Backend Docker Image

Docker container image for the YoFi.V3 backend API, a modern .NET 10 REST API for personal finance management.

## What is YoFi.V3?

YoFi.V3 is a web-based personal finance application that helps you track and manage your financial transactions. The backend API provides:

- **Transaction Management** - Record, categorize, and query financial transactions
- **Multi-Tenant Architecture** - Secure isolation for multiple users and accounts
- **RESTful API** - Modern HTTP API with OpenAPI/Swagger documentation
- **Authentication & Authorization** - JWT-based security with user registration and login
- **Data Persistence** - SQLite database for reliable transaction storage
- **Health Monitoring** - Built-in health checks and comprehensive logging

This container provides the backend API service. The frontend is a separate Vue.js/Nuxt application that connects to this API.

**Technology Stack:**
- .NET 10.0 with ASP.NET Core
- Entity Framework Core ORM
- SQLite database
- JWT authentication
- OpenAPI/Swagger documentation

## Quick Start

Pull and run the backend container:

```bash
docker run -p 5001:8080 yofi-v3-backend
```

The API will be available at http://localhost:5001

## Image Details

ASP.NET Core 10 REST API providing backend services for personal finance management.

**Features:**
- RESTful API with OpenAPI/Swagger documentation
- Entity Framework Core with SQLite database
- JWT-based authentication
- Health check endpoints
- Multi-tenant support
- Comprehensive logging with structured output

**Base Image:** `mcr.microsoft.com/dotnet/aspnet:10.0.0-rc.2`

**Exposed Ports:**
- `8080` - HTTP API endpoint

**Health Check:** `http://localhost:8080/health`

## Building from Source

### Prerequisites

- Docker Desktop or Docker Engine
- Source code from [YoFi.V3 repository](https://github.com/jcoliz/YoFi.V3)

### Build the Image

From the repository root:

```bash
docker build -f docker/BackEnd.dockerfile -t yofi-v3-backend .
```

Or use the convenience script:

```powershell
.\scripts\Build-Container.ps1
```

### Build Arguments

| Argument | Description | Default |
|----------|-------------|---------|
| `SOLUTION_VERSION` | Version tag for the build | `docker-local` |

Example with version:
```bash
docker build -f docker/BackEnd.dockerfile \
  --build-arg SOLUTION_VERSION=1.0.0 \
  -t yofi-v3-backend:1.0.0 .
```

## Configuration

### Environment Variables

Configure the container using these environment variables:

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `APPLICATION__ENVIRONMENT` | Runtime environment (Container/Development/Production) | `Container` | No |
| `APPLICATION__ALLOWEDCORSORIGINS__0` | CORS allowed origin | _(none)_ | Yes |
| `JWT__ISSUER` | JWT token issuer URL | _(none)_ | Yes |
| `JWT__AUDIENCE` | JWT token audience URL | _(none)_ | Yes |
| `JWT__KEY` | JWT signing key (base64) | _(none)_ | Yes |
| `JWT__LIFESPAN` | JWT token lifespan (HH:MM:SS) | `00:10:00` | No |
| `APPLICATION_INSIGHTS_CONNECTION_STRING` | Azure Application Insights connection | _(optional)_ | No |

⚠️ **Security Warning:** Always use a secure, randomly generated JWT key in production. Never commit keys to source control.

### Example Configuration

Run with environment variables:

```bash
docker run -p 5001:8080 \
  -e APPLICATION__ENVIRONMENT=Container \
  -e APPLICATION__ALLOWEDCORSORIGINS__0=http://localhost:5000 \
  -e JWT__ISSUER=http://localhost:5001 \
  -e JWT__AUDIENCE=http://localhost:5001 \
  -e JWT__KEY=YourSecureBase64EncodedKeyHere \
  yofi-v3-backend
```

### Using Docker Compose

The repository includes a [`docker-compose-ci.yml`](docker-compose-ci.yml) configuration for testing and CI purposes:

```bash
docker compose -f docker/docker-compose-ci.yml up backend
```

⚠️ **Note:** The compose file includes development/testing defaults for JWT configuration. These are NOT suitable for production use.

## Usage

### Run the Container

Start in foreground with logs:
```bash
docker run -p 5001:8080 --env-file .env yofi-v3-backend
```

Start in background (detached):
```bash
docker run -d -p 5001:8080 --env-file .env --name yofi-backend yofi-v3-backend
```

### View Logs

```bash
docker logs -f yofi-backend
```

### Stop the Container

```bash
docker stop yofi-backend
docker rm yofi-backend
```

### Execute Commands

```bash
docker exec -it yofi-backend /bin/bash
```

## API Endpoints

Once running, access the API at:

- **Health Check:** http://localhost:5001/health
- **Swagger UI:** http://localhost:5001/swagger
- **OpenAPI Spec:** http://localhost:5001/swagger/v1/swagger.json

Key endpoints:
- `GET /api/weather` - Weather forecast (sample)
- `GET /api/version` - Application version information
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - User authentication
- `GET /api/transactions` - Transaction management (requires authentication)

## Data Persistence

By default, the SQLite database runs ephemerally inside the container. Data is lost when the container is removed.

### Persist Data with Volumes

Mount a volume to persist the database:

```bash
docker run -p 5001:8080 \
  -v $(pwd)/data:/app/data \
  --env-file .env \
  yofi-v3-backend
```

The database file location is controlled by the connection string in configuration.

## Health Checks

The image includes a built-in health check that monitors:
- Application startup state
- ASP.NET Core health endpoints

Docker automatically performs health checks every 5 seconds with a 5-second timeout.

Check health status manually:
```bash
curl http://localhost:5001/health
```

View health status in Docker:
```bash
docker ps
# Look for "healthy" in STATUS column
```

## Observability with Aspire Dashboard

The docker-compose configuration includes the Aspire Dashboard for comprehensive observability during development and testing.

### Accessing the Dashboard

When running containers via [`docker-compose-ci.yml`](docker-compose-ci.yml), the Aspire Dashboard is automatically available at:

**http://localhost:18888**

The dashboard provides real-time visibility into:
- **Structured Logs** - Filter, search, and correlate logs with TraceIds
- **Distributed Traces** - View request flows and timing breakdowns
- **Metrics** - Monitor ASP.NET Core, EF Core, and runtime performance
- **Resources** - See service health and configuration

### Dashboard Features

#### Structured Logs Tab
- Filter by log level (Debug, Info, Warning, Error)
- Search by message content or TraceId
- View structured log properties
- Correlate logs with specific requests

#### Traces Tab
- Visualize distributed traces through the application
- See timing breakdown of each operation
- Filter by status (success, error)
- View detailed span information

#### Metrics Tab
- ASP.NET Core metrics (request rate, duration, active requests)
- HTTP client metrics (outbound request stats)
- Runtime metrics (GC, thread pool, exceptions)
- Entity Framework Core metrics (query duration, connection pool)

### Using the Dashboard for Debugging

**During functional tests:**
```powershell
.\scripts\Run-FunctionalTestsVsContainer.ps1
# Dashboard available at http://localhost:18888
```

**When troubleshooting container issues:**
```powershell
.\scripts\Start-Container.ps1
# Opens both the application and dashboard automatically
```

The dashboard provides much richer diagnostics than `docker logs` alone, making it easier to:
- Debug functional test failures
- Troubleshoot issues specific to the generated frontend
- Analyze performance problems
- Correlate logs with specific HTTP requests

## Troubleshooting

### View Telemetry in Aspire Dashboard

For most troubleshooting scenarios, the Aspire Dashboard (http://localhost:18888) provides the best visibility. Check the Structured Logs and Traces tabs to see detailed diagnostic information with correlation IDs.

### Container Exits Immediately

Check logs for configuration errors:
```bash
docker logs yofi-backend
```

Common issues:
- Missing required environment variables (JWT configuration)
- Invalid CORS origin format
- Port already in use

### Connection Refused

Verify the container is running and healthy:
```bash
docker ps
curl http://localhost:5001/health
```

Check if port 5001 is already in use:
```bash
netstat -an | findstr :5001  # Windows
lsof -i :5001                # Linux/Mac
```

### Database Errors

Remove the container and volume, then restart:
```bash
docker rm -f yofi-backend
docker volume rm yofi-data
docker run -v yofi-data:/app/data ...
```

### Enable Debug Logging

Set logging level to Debug in environment:
```bash
docker run -p 5001:8080 \
  -e Logging__LogLevel__YoFi.V3=Debug \
  --env-file .env \
  yofi-v3-backend
```

## Testing

Run functional tests against the containerized backend:

```powershell
.\scripts\Run-FunctionalTestsVsContainer.ps1
```

This script:
1. Builds the backend and dashboard containers
2. Starts them with test configuration
3. Displays the Aspire Dashboard URL for real-time monitoring
4. Runs the complete Playwright test suite
5. Tears down the environment

**Tip:** Open the Aspire Dashboard (http://localhost:18888) while tests are running to see real-time logs, traces, and metrics.

## Performance

**Build Times:**
- First build: ~3-5 minutes
- Incremental builds: ~30-60 seconds (with layer caching)

**Resource Usage:**
- Memory: ~150 MB RAM
- Storage: ~250 MB disk space

**Layer Caching:** The Dockerfile uses intelligent layer caching to optimize rebuild performance.

## Development vs Production

This container is designed to run in multiple environments:

- **Development/Testing:** Use with default or test JWT keys, SQLite database
- **Production:** Requires secure JWT configuration, consider external database (SQL Server, PostgreSQL)

See [Container Environment Guide](../docs/CONTAINER-ENVIRONMENT.md) for detailed information about running in different environments.

## Related Documentation

- [Container Environment Guide](../docs/CONTAINER-ENVIRONMENT.md) - Detailed containerization documentation
- [Aspire Dashboard Setup](../docs/wip/DOCKER-COMPOSE-ASPIRE-DASHBOARD.md) - Complete Aspire Dashboard integration guide
- [Deployment Guide](../docs/DEPLOYMENT.md) - Production deployment to Azure
- [Architecture Overview](../docs/ARCHITECTURE.md) - Application architecture
- [Contributing Guide](../docs/CONTRIBUTING.md) - Development setup and guidelines

## Security Considerations

- Always use secure, randomly generated JWT keys in production
- Store sensitive configuration in environment variables or secrets management
- Use HTTPS in production (not included in this image - configure at reverse proxy)
- Regularly update base images for security patches
- Enable Application Insights or other monitoring in production

## Support

For issues, questions, or contributions:
- **GitHub Repository:** https://github.com/jcoliz/YoFi.V3
- **Issues:** https://github.com/jcoliz/YoFi.V3/issues

## License

This project is open source. See the LICENSE file in the repository for details.
