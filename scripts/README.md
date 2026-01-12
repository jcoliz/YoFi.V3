# Scripts

This folder contains scripts of many different kinds. Often these would be scattered around the project, found wherever they're used. I think it's more useful to have them collected in one place.

## Table of Contents

### Development Setup

| Script | Synopsis |
|--------|----------|
| [`Setup-Development.ps1`](Setup-Development.ps1) | Setup development environment for new developers by verifying tools, restoring dependencies, building solution, and running tests |

### Local Development

| Script | Synopsis |
|--------|----------|
| [`Start-LocalDev.ps1`](Start-LocalDev.ps1) | Starts the local development environment using .NET Aspire with hot reload enabled |

### Testing

| Script | Synopsis |
|--------|----------|
| [`Run-Tests.ps1`](Run-Tests.ps1) | Run unit and integration tests (excludes functional tests) |
| [`Run-FunctionalTestsVsContainer.ps1`](Run-FunctionalTestsVsContainer.ps1) | Runs functional tests against the CI Docker containers |
| [`Collect-CodeCoverage.ps1`](Collect-CodeCoverage.ps1) | Runs unit tests and collects code coverage metrics with HTML report generation |
| [`Analyze-StepPatterns.ps1`](Analyze-StepPatterns.ps1) | Analyzes functional test step definition patterns for duplicates |

### Database Management

| Script | Synopsis |
|--------|----------|
| [`Add-Migration.ps1`](Add-Migration.ps1) | Adds a new Entity Framework migration to the specified database provider |
| [`Create-MigrationScript.ps1`](Create-MigrationScript.ps1) | Creates a SQL script to migrate the database schema to the latest migration |

### Docker Operations

| Script | Synopsis |
|--------|----------|
| [`Build-Container.ps1`](Build-Container.ps1) | Builds the Docker CI containers locally for testing |
| [`Push-Container.ps1`](Push-Container.ps1) | Pushes Docker CI containers to DockerHub |
| [`Start-Container.ps1`](Start-Container.ps1) | Starts the Docker CI containers locally and opens the application in browser |
| [`Stop-Container.ps1`](Stop-Container.ps1) | Stops the Docker CI containers locally |

### Azure Deployment

| Script | Synopsis |
|--------|----------|
| [`Provision-Resources.ps1`](Provision-Resources.ps1) | Provisions Azure resources for the YoFi.V3 application using Bicep templates |
| [`Rotate-JwtKey.ps1`](Rotate-JwtKey.ps1) | Rotates the JWT signing key for a deployed application (invalidates all tokens) |

### Code Generation

| Script | Synopsis |
|--------|----------|
| [`Generate-ApiClient.ps1`](Generate-ApiClient.ps1) | Generates TypeScript API client for frontend from backend API specification using NSwag |

### Build Utilities

| Script | Synopsis |
|--------|----------|
| [`Get-Version.ps1`](Get-Version.ps1) | Gets the current version string for the solution build from environment or git |

## Quick Start

### For New Developers

```powershell
# First time setup
.\Setup-Development.ps1

# Start local development
.\Start-LocalDev.ps1
```

### Running Tests

```powershell
# Run unit and integration tests
.\Run-Tests.ps1

# Run functional tests (requires Docker)
.\Run-FunctionalTestsVsContainer.ps1

# Collect code coverage
.\Collect-CodeCoverage.ps1
```

### Docker Workflows

```powershell
# Build containers
.\Build-Container.ps1

# Push containers to DockerHub (requires docker login)
.\Push-Container.ps1

# Or build and push in one step
.\Push-Container.ps1 -Build

# Push with a custom tag
.\Push-Container.ps1 -Tag "latest"

# Start containers
.\Start-Container.ps1

# Stop containers
.\Stop-Container.ps1
```

### Code Generation

```powershell
# Generate TypeScript API client for frontend
.\Generate-ApiClient.ps1

# Generate with Release configuration
.\Generate-ApiClient.ps1 -Configuration Release
```

### Azure Deployment

```powershell
# Provision Azure resources (includes JWT key generation)
.\Provision-Resources.ps1 -ResourceGroup "yofi-rg" -Location "eastus2" -StaticWebAppLocation "eastus2"

# Rotate JWT key for existing deployment (with confirmation)
.\Rotate-JwtKey.ps1 -ResourceGroup "yofi-rg" -AppServiceName "web-abc123"

# Rotate JWT key without confirmation prompt
.\Rotate-JwtKey.ps1 -ResourceGroup "yofi-rg" -AppServiceName "web-abc123" -Confirm:$false
```

## Prerequisites

- **PowerShell** 5.1 or later (Windows) or PowerShell Core 7+ (cross-platform)
- **.NET SDK** 10.0 or later
- **Node.js** 24+ with pnpm
- **Docker Desktop** (for container-based workflows)
- **Azure CLI** (for Azure deployment scripts)

## Notes

### API Client Generation

The `Generate-ApiClient.ps1` script regenerates the frontend TypeScript client. The TypeScript client is automatically regenerated when you build the `WireApiHost` project, but this script provides an explicit way to trigger regeneration.

The C# API client for functional tests is automatically generated during the test project build (see [`tests/Functional/YoFi.V3.Tests.Functional.csproj`](../tests/Functional/YoFi.V3.Tests.Functional.csproj) for the MSBuild configuration).

## Documentation

For more information about contributing to this project, see:

- [CONTRIBUTING.md](../docs/CONTRIBUTING.md) - Development guidelines
- [ARCHITECTURE.md](../docs/ARCHITECTURE.md) - System architecture
- [DEPLOYMENT.md](../docs/DEPLOYMENT.md) - Deployment processes
