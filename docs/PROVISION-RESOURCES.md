# Provisioning Resource Infrastructure for Production

This document describes how to provision the necessary Azure resources to deploy this application into.

## What Gets Provisioned

This script creates the following Azure resources:

| Resource Type | Purpose | Estimated Cost |
|---------------|---------|----------------|
| **Static Web App** (Free tier) | Hosts Vue.js frontend | Free |
| **App Service** (B1 Basic) | Hosts .NET API backend | ~$13/month |
| **App Service Plan** (B1) | Compute for App Service | Included above |
| **Application Insights** | Monitoring and telemetry | ~$0-5/month |
| **Log Analytics Workspace** | Log storage | ~$0-5/month |

**Total estimated cost: ~$15-20/month**

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed and updated
- PowerShell 5.1+ or PowerShell Core 7+
- An Azure subscription with Contributor access
- Git (for submodule operations)

## Provisioning Steps

1. Ensure this repository has been cloned with submodules, or if not, initialize the submodules now
    ```
    git submodule update --init --recursive
    ```

2. Ensure you're logged into Azure using the Azure CLI into the correct subscription where you want the resources provisioned.
    ```
    az login --tenant=<your_tenant_id>
    az account set --subscription <your_subscription_id>
    az account show
    ```

3. Choose resource group and locations. **Examples:**

   ```bash
   # Good location combinations:
   RESOURCE_GROUP="rg-yofi-prod"
   PRIMARY_LOCATION="eastus2"           # Most resources
   STATIC_WEB_APP_LOCATION="eastus2"    # Static Web Apps available here
   
   # Alternative:
   PRIMARY_LOCATION="westus2"
   STATIC_WEB_APP_LOCATION="westus2"    # Check availability at https://aka.ms/staticwebapps/regions
   ```

4. Run the provisioning script, providing these values
    ```
    ./scripts/Provision-Resources.ps1 -ResourceGroup <your_resource_group> -Location <primary_location> -StaticWebAppLocation <static_web_app_location>
    ```

## Troubleshooting

### Common Issues

**"Static Web Apps not available in this region"**
- Use one of these regions: `eastus2`, `westus2`, `centralus`, `eastasia`, `westeurope`
- See [Static Web Apps regions](https://aka.ms/staticwebapps/regions) for full list

**"Insufficient permissions"**
- Ensure your account has `Contributor` role on the subscription
- Check: `az role assignment list --assignee $(az account show --query user.name -o tsv)`

**"Resource group already exists"**
- The script will use the existing resource group if it exists
- Ensure you have access: `az group show --name <resource-group>`

**PowerShell execution policy issues (Windows)**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## After Deployment

### 1. Save Important Values
The script outputs these values

You will need these to deploy using Azure Pipelines CD:

```
Deployment Pipeline Inputs:
  azureStaticAppApiToken: <your_deployment_token>
  azureAppServiceName: web-{suffix}
  backendBaseUrl: https://web-{suffix}.azurewebsites.net
  appInsightsConnectionString: <your_appinsights_connection_string>
```

### 2. Set up CD
- YoFi.V3 includes CD pipeline definitions for Azure Pipelines
- Create a new pipeline
- Add the "deployment pipeline inputs" given above as pipeline variables
- TODO: Add appInsightsConnectionString to CD config

### 3. Set up local development monitoring (TODO)
- Create `./src/AppHost/config.toml`
- Add these lines:
    ```toml
    [Application]
    ApplicationInsights=<your_appinsights_connection_string>
    ```

### 4. Monitoring
Access your application monitoring at:
- **Application Insights**: Search for "insights-{suffix}" in Azure Portal
- **Log Analytics**: Search for "logs-{suffix}" in Azure Portal

## Coming in Future

In the future, this script will be improved to...

* Assign a custom domain to the static web app
* Include storage (when features needing storage get implemented)
