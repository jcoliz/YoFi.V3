# Deployment Guide

## Overview

This application uses **Azure Pipelines** for Continuous Deployment (CD) to Azure. The deployment process is fully automated and deploys both the frontend (Vue.js/Nuxt) and backend (.NET API) to Azure services.

## Architecture

- **Frontend**: Deployed to Azure Static Web Apps
- **Backend**: Deployed to Azure App Service
- **Infrastructure**: Provisioned using Azure Bicep templates

## Pipeline Configuration

The main pipeline configuration is located in **`.azure/pipelines/ci.yaml`**.

### Pipeline Stages

1. **Build, Test & Publish** - Compiles code, runs tests, creates artifacts
2. **Containerize & Functional Tests** - Creates Docker containers and runs end-to-end tests
3. **Deploy to Azure** - Deploys artifacts to Azure Static Web Apps and App Service

## Setting Up Azure Pipeline

### Prerequisites

1. **Azure Resources**: Provision Azure resources first using the [provisioning guide](PROVISION-RESOURCES.md)
2. **Azure DevOps Project**: Create or use existing Azure DevOps project
3. **Service Connections**: Set up Azure service connections

### Required Pipeline Variables

These must be configured as pipeline variables in Azure DevOps:

| Variable | Description | Example | Source |
|----------|-------------|---------|--------|
| `azureAppServiceName` | Name of Azure App Service for backend | `web-abc123` | Provisioning script output |
| `azureStaticAppApiToken` | API token for Static Web Apps | `xxxx-xxxx-xxxx` | Provisioning script output |
| `azureServiceConnectionName` | Azure service connection name | `azure-prod-connection` | Created in Azure DevOps |
| `backendBaseUrl` | Backend API base URL | `https://web-abc123.azurewebsites.net` | Provisioning script output |
| `dockerRegistryEndpoint` | Docker registry service connection | `docker-hub-connection` | Created in Azure DevOps |
| `dockerUserName` | Docker registry username | `your-username` | Your Docker Hub account |

### Step 1: Create Azure Service Connection

1. Go to **Project Settings** → **Service connections**
2. Click **New service connection** → **Azure Resource Manager**
3. Choose **Service principal (automatic)**
4. Select your **Subscription** and **Resource Group**
5. Name it (e.g., `azure-prod-connection`)
6. Save the connection name as `azureServiceConnectionName` pipeline variable

### Step 2: Create Pipeline

1. Go to **Pipelines** → **Create Pipeline**
2. Choose **Azure Repos Git** (or your source)
3. Select your repository
4. Choose **Existing Azure Pipelines YAML file**
5. Select **`.azure/pipelines/ci.yaml`**
6. Click **Continue** → **Save**

### Step 3: Configure Pipeline Variables

1. Go to your pipeline → **Edit** → **Variables**
2. Add each required variable from the table above
3. Mark sensitive variables (like `azureStaticAppApiToken`) as **secret**
4. **Save** the pipeline

### Step 4: Set up Triggers

The pipeline is configured to trigger on:
- **Commits to main branch** - Automatic deployment
- **Pull requests** - Disabled (manual testing only)

To modify triggers, edit the trigger section in `ci.yaml`:

```yaml
trigger:
  branches:
    include:
    - main

pr: none  # Change to 'pr: [main]' to enable PR builds
```

## Pipeline Workflow Details

### Build & Test Stage
- Checks out source code
- Installs .NET SDK 10.x
- Builds solution and runs unit tests
- Builds Nuxt frontend application
- Creates deployment artifacts

### Containerization & Functional Tests
- Builds Docker containers for testing
- Runs end-to-end functional tests using Playwright
- Validates application functionality before deployment

### Deployment Stage
- **Frontend Deployment**: Deploys Nuxt build to Azure Static Web Apps
- **Backend Deployment**: Deploys .NET application to Azure App Service
- Deployments run in parallel for faster completion

## Monitoring Deployments

### Pipeline Status
- Monitor pipeline runs in **Azure DevOps** → **Pipelines**
- View logs for each stage and job
- Check test results and artifacts

### Application Monitoring
- **Application Insights**: Monitor application performance and errors
- **Log Analytics**: Query structured logs and telemetry
- **Azure Portal**: Check resource health and metrics

## Troubleshooting

### Common Issues

**Pipeline fails with "Service connection not found"**
- Verify service connection exists and name matches `azureServiceConnectionName` variable
- Check service connection has proper permissions on resource group

**Static Web App deployment fails**
- Verify `azureStaticAppApiToken` is correct and not expired
- Check Static Web App exists in Azure portal

**App Service deployment fails**
- Verify `azureAppServiceName` matches actual App Service name
- Check service connection has Contributor access to App Service

**Functional tests fail**
- Check if containers are starting properly in pipeline logs
- Verify Docker service connection is configured correctly

### Getting Help

1. **Pipeline Logs**: Check detailed logs in Azure DevOps pipeline runs
2. **Azure Portal**: Check Application Insights for runtime errors
3. **Local Testing**: Run functional tests locally using Docker Compose

## Manual Deployment (Alternative)

For manual deployments or troubleshooting:

### Frontend (Static Web App)
```bash
# Install Azure Static Web Apps CLI
npm install -g @azure/static-web-apps-cli

# Deploy manually
swa deploy ./dist --deployment-token <your-token>
```

### Backend (App Service)
```bash
# Using Azure CLI
az webapp deploy --resource-group <rg> --name <app-name> --src-path <zip-file>
```

## Security Considerations

- **Pipeline variables** marked as secret are encrypted
- **Service connections** use managed identities when possible
- **API tokens** have minimal required permissions
- **Deployment artifacts** are automatically cleaned up

## Next Steps

- Set up **branch policies** for main branch protection
- Configure **approval gates** for production deployments
- Add **monitoring alerts** for deployment failures
- Consider **blue-green deployments** for zero-downtime updates

