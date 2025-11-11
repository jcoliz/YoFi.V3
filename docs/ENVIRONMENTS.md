# Environments

The application is built to run in three distinct environments:

* Local: For local development
* Container: Primarily for quick build/execution of functional tests in CI pipeline
* Production: Running in Azure

## Local

As described in [ADR 0004](./adr/0004-aspire-development.md), local development is done using .NET Aspire.
In this case, the code is built locally, and running with a local Aspire Dashboard. This enables us to debug
the backend or frontend directly. The frontend is running in an Node.JS dev server run by NPM.

## Container

To make functional tests easy to run both in Azure Pipelines, or at our desks, the app can be containerized.
This uses a [docker compose project](../docker/docker-compose-ci.yml) to orchestrate the needed containers.
When built into a container, the frontend is generated using `nuxt generate`.

## Production

As described in [ADR 0006](./adr/0006-production-infrastructure.md), the application is deployed to Azure as a
pair of Azure resources: The frontend is deployed as an Azure Static Web app, while the backend is deployed
as an Azure App Service. These are built and deployed using Azure Pipelines, which will be defined soon.
