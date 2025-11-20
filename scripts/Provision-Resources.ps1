<#
.SYNOPSIS
Provisions Azure resources for the YoFi.V3 application.

.DESCRIPTION
This script creates an Azure Resource Group and deploys all required Azure resources
using Bicep templates. It provisions a Static Web App, Web App, Log Analytics Workspace,
and Application Insights, then outputs the deployment information needed for CI/CD pipelines.

.PARAMETER ResourceGroup
The name of the Azure Resource Group to create or use. This parameter is required.

.PARAMETER Location
The Azure region where the Resource Group will be created (e.g., "eastus", "westus2").
This parameter is required.

.PARAMETER StaticWebAppLocation
The Azure region where the Static Web App will be created. This may differ from the
Resource Group location. This parameter is required.

.EXAMPLE
.\Provision-Resources.ps1 -ResourceGroup "yofi-rg" -Location "eastus" -StaticWebAppLocation "eastus2"
Creates a resource group in East US and provisions all resources with the Static Web App in East US 2.

.OUTPUTS
The script outputs deployment information including:
- Resource names (Static Web App, Web App, Log Analytics, App Insights)
- Endpoints (Frontend URL, Backend API URL)
- Deployment pipeline inputs (tokens and connection strings)

.NOTES
Requires Azure CLI to be installed and authenticated.
The script uses the Bicep template located at ../infra/main.bicep.

.LINK
https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/overview
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]
    $ResourceGroup,
    [Parameter(Mandatory=$true)]
    [string]
    $Location,
    [Parameter(Mandatory=$true)]
    [string]
    $StaticWebAppLocation
)

$ErrorActionPreference = "Stop"

try {
    Write-Host "Creating Resource Group $ResourceGroup in $Location..." -ForegroundColor Cyan
    az group create --name $ResourceGroup --location $Location
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create resource group with exit code $LASTEXITCODE"
    }
    
    Write-Host "Deploying to Resource Group $ResourceGroup..." -ForegroundColor Cyan
    $Top = "$PSScriptRoot/.."
    $TemplatePath = "$Top/infra/main.bicep"
    
    if (-not (Test-Path $TemplatePath)) {
        throw "Bicep template not found: $TemplatePath"
    }
    
    $DeploymentName = "Deploy-$(Get-Random)"
    Write-Verbose "Deployment name: $DeploymentName"
    
    $result = az deployment group create --name $DeploymentName --resource-group $ResourceGroup --template-file $TemplatePath --parameter staticWebAppLocation=$StaticWebAppLocation | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw "Deployment failed with exit code $LASTEXITCODE"
    }
    
    $staticWebAppName = $result.properties.outputs.staticWebAppName.value
    $webAppName = $result.properties.outputs.webAppName.value
    $webAppDefaultHostName = $result.properties.outputs.webAppDefaultHostName.value
    $appInsightsName = $result.properties.outputs.appInsightsName.value
    
    Write-Verbose "Retrieving deployment token for Static Web App..."
    $deploymentToken = az staticwebapp secrets list --name $staticWebAppName --resource-group $ResourceGroup --query "properties.apiKey" -o tsv
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to retrieve Static Web App token with exit code $LASTEXITCODE"
    }
    
    Write-Verbose "Retrieving Application Insights connection string..."
    $insightsConnectionString = az monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup --query connectionString --output tsv
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to retrieve App Insights connection string with exit code $LASTEXITCODE"
    }
    
    Write-Host ""
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Resources provisioned:" -ForegroundColor Cyan
    Write-Output "  Static Web App: $staticWebAppName"
    Write-Output "  Web App: $webAppName"
    Write-Output "  Log Analytics Workspace: $($result.properties.outputs.logAnalyticsName.value)"
    Write-Output "  App Insights: $appInsightsName"
    Write-Host ""
    Write-Host "Endpoints:" -ForegroundColor Cyan
    Write-Output "  Frontend: https://$($result.properties.outputs.staticWebHostName.value)"
    Write-Output "  Backend API: https://$webAppDefaultHostName"
    Write-Host ""
    Write-Host "Deployment Pipeline Inputs:" -ForegroundColor Cyan
    Write-Output "  azureStaticAppApiToken: $deploymentToken"
    Write-Output "  azureAppServiceName: $webAppName"
    Write-Output "  backendBaseUrl: https://$webAppDefaultHostName"
    Write-Output "  appInsightsConnectionString: $insightsConnectionString"
}
catch {
    Write-Error "Failed to provision resources: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}