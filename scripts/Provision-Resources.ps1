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

Write-Output "Creating Resource Group $ResourceGroup in $Location"
az group create --name $ResourceGroup --location $Location

Write-Output "Deploying to Resource Group $ResourceGroup"
$Top = "$PSScriptRoot/.."
$result = az deployment group create --name "Deploy-$(Get-Random)" --resource-group $ResourceGroup --template-file "$Top/infra/main.bicep" --parameter staticWebAppLocation=$StaticWebAppLocation | ConvertFrom-Json

$staticWebAppName = $result.properties.outputs.staticWebAppName.value
$webAppName = $result.properties.outputs.webAppName.value
$webAppDefaultHostName = $result.properties.outputs.webAppDefaultHostName.value
$appInsightsName = $result.properties.outputs.appInsightsName.value

# Get the deployment token for your static web app
$deploymentToken = az staticwebapp secrets list --name $staticWebAppName --resource-group $ResourceGroup --query "properties.apiKey" -o tsv

# Get connection string for a the Application Insights resource
$insightsConnectionString = az monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup --query connectionString --output tsv

Write-Output ""
Write-Output "Deployment completed successfully!"
Write-Output ""
Write-Output "Resources provisioned:"
Write-Output "  Static Web App: $staticWebAppName"
Write-Output "  Web App: $webAppName"
Write-Output "  Log Analytics Workspace: $($result.properties.outputs.logAnalyticsName.value)"
Write-Output "  App Insights: $appInsightsName"
Write-Output ""
Write-Output "Endpoints:"
Write-Output "  Frontend: https://$($result.properties.outputs.staticWebHostName.value)"
Write-Output "  Backend API: https://$webAppDefaultHostName"
Write-Output ""
Write-Output "Deployment Pipeline Inputs:"
Write-Output "  azureStaticAppApiToken: $deploymentToken"
Write-Output "  azureAppServiceName: $webAppName"
Write-Output "  backendBaseUrl: https://$webAppDefaultHostName"
Write-Output "  appInsightsConnectionString: $insightsConnectionString"