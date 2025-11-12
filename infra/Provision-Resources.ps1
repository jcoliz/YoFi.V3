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

Write-Output "Creating Resource Group $ResourceGroup in $Location"
az group create --name $ResourceGroup --location $Location

Write-Output "Deploying to Resource Group $ResourceGroup"
$result = az deployment group create --name "Deploy-$(Get-Random)" --resource-group $ResourceGroup --template-file "$PSScriptRoot/main.bicep" --parameter staticWebAppLocation=$StaticWebAppLocation | ConvertFrom-Json

Write-Output ""
Write-Output "Deployment completed successfully!"
Write-Output ""
Write-Output "Frontend: https://$($result.properties.outputs.staticWebHostName.value)"
Write-Output "Backend API: https://$($result.properties.outputs.webAppDefaultHostName.value)"
Write-Output "App Insights: $($result.properties.outputs.appInsightsName.value)"
