# Containerizing this app

YoFi.V3 can be built and run in containers. This document will explain why and how.

## Why?

Running in containers supports these developer use cases.

As a developer I can...
- Run the app quickly in a near-production environment
- Run all functional tests quickly, with assurance the same code will work as well in production
- Run the functional tests in the CI pipeline, protecting the codebase from every commit
- Lower the barrier to evaluation by colleagues

### Near-production environment locally

The container build generates the frontend as a static site. It runs the backend in the `Container` environment, using PostgreSQL connection-string authentication and automatic EF migrations rather than Production managed-identity authentication. Configuration is handled by environment variables, including CORS settings, in a production-like manner.

### Fast functional tests

If it takes too long to set up and run functional tests, or they're unreliable, we won't run them locally. This leads to the potential for
decay in the functional tests or last-minute surprises when CI builds fail.

Instead, with one script, we can quickly build the application into containers and run the entire suite of functional tests against it.

```powershell
.\scripts\Start-Container.ps1 -Build -NoBrowser
.\scripts\Run-Tests.ps1 -Functional
```

### Functional tests in CI build

Rather than using additional cloud resources for a testing environment, and waiting for a deployment, we can build and run the entire functional
tests in the same container on the CI machine. This gives us high confidence that any new changes aren't going to break anything in production.

### Peer evaluation

When I want to send the app to a colleague for review, the barrier of entry is much lower to run in a docker container. Rather than having to
set up all the dependencies, they can simply run the docker compose
project, and they have a full version of the app running locally.

## How to run locally

The simplest way to get started locally is to run the convenience scripts to build, and to run the compose project.

To run `docker compose build`:

```
./scripts/Build-Container.ps1
```

To run `docker compose up --wait -d`, and bring up browser window:

```
./scripts/Start-Container.ps1
```

Leave application containers running after tests. Disposable integration-test
containers are managed separately by Testcontainers.

## How it's built

The [docker-compose-ci](../docker/docker-compose-ci.yml) project contains the orchestration needed to build, run, and (in the future) push and publish the containers. This project sets up the same build-time evironment variables that are used by the CD pipeline to deploy the production app. It runs them using a production-capable runtime configuration using environment variables.

Using a docker compose project to manage building, running, pushing, and publishing centralizes configuration in a single file, clarifying how application containerizing works across all phases.

> [!NOTE] In final production, secrets will come from an Azure Key Vault, so there is a little variance from the final production state, in that way.

The [Backend.dockerfile](../docker/BackEnd.dockerfile) and the Frontend.Nuxt [Dockerfile](../src/FrontEnd.Nuxt/docker/Dockerfile) specify the needed build and run details.

The database runs in the Compose `db` service using PostgreSQL. Its data directory
is mounted as `tmpfs`, so data is lost when that database container stops. This is
intentional for testing. Durable storage requires replacing the `tmpfs` mount with
a PostgreSQL data volume, not mounting a database file in the backend container.

Note that containers are not intended to replace a robust local development setup. To make this easy, we even have a script which checks all local development prerequisites so you can catch issues right away.

```powershell
./scripts/Start-LocalDev.ps1
```

In the future, I plan to take advantage of coming capability in docker compose to package the entire project in a single OCI unit. This will
enable an evaluator to simply run one command to pull both containers locally and run the full project.

```bash
docker run jcoliz/yofi-v3:latest
```

## Observability

### Aspire Dashboard

The docker-compose configuration includes the Aspire Dashboard for comprehensive observability. When containers are running, the dashboard is available at **http://localhost:18788**.

**Dashboard Features:**
- **Structured Logs** - Filter, search, and correlate logs with TraceIds (better than `docker logs`)
- **Distributed Traces** - Visualize request flows and timing breakdowns
- **Metrics** - Monitor ASP.NET Core, EF Core, and runtime performance
- **Resources** - View service health and configuration

**Automatic Access:**
- [`Start-Container.ps1`](../scripts/Start-Container.ps1) - Opens dashboard automatically
- [`Run-FunctionalTestsVsContainer.ps1`](../scripts/Run-FunctionalTestsVsContainer.ps1) - Displays dashboard URL

See [`docs/wip/DOCKER-COMPOSE-ASPIRE-DASHBOARD.md`](wip/DOCKER-COMPOSE-ASPIRE-DASHBOARD.md) for complete details on using the dashboard.

## Troubleshooting

If functional tests fail against the container, you have several options:

### 1. View Telemetry in Aspire Dashboard (Recommended)

Open http://localhost:18788 and check:
- **Structured Logs tab** - Filter by log level, search by content, view structured properties
- **Traces tab** - See request flows with timing information
- **Metrics tab** - Check for performance issues

This provides much richer diagnostics than `docker logs` alone and makes it easy to correlate logs with specific requests using TraceIds.

### 2. Re-run Failed Tests in Playwright Debug Mode

Change the `PWDEBUG` setting in [docker.runsettings](../tests/Functional/docker.runsettings) to `1` and re-run one test at a time. This usually shows the problem.

### 3. Check Docker Logs

The backend logs useful information to stdout:
```powershell
docker logs yofi-v3-backend-1
```

### 4. Increase Log Levels

If you need more detailed logs, set `YoFi.V3` logging level to `Debug` in the docker-compose environment variables. The logs will appear in both `docker logs` and the Aspire Dashboard.

### 5. Check Browser Console

For frontend issues, check the browser console logs. Occasionally useful notes are dropped there.

## Benchmarking performance

Both dockerfiles use intelligent layer caching. While the very first run can take 5+ minutes, it's much faster in subsequent iterations. In my experience, it takes about 30 seconds total to rebuild both containers in the worst case rebuilding all the backend projects and regenerating the frontend. This compares with about 5 minutes on the CI machine to clone, build, publish and deploy a new version. On larger apps, I have seen an even greater savings.
