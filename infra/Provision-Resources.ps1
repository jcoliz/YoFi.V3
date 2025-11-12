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
#$result =
az deployment group create --name "Deploy-$(Get-Random)" --resource-group $ResourceGroup --template-file "$PSScriptRoot/main.bicep" --parameter staticWebAppLocation=$StaticWebAppLocation
#| ConvertFrom-Json

Write-Output "OK"
Write-Output ""

#Write-Output "Copy these values to config.toml:"
#Write-Output ""

#$dcrImmutableId = $result.properties.outputs.dcrImmutableId.value
#$endpointUri = $result.properties.outputs.endpointUri.value
#$stream = $result.properties.outputs.stream.value

#Write-Output "[LogIngestion]"
#Write-Output "EndpointUri = ""$endpointUri"""
#Write-Output "DcrImmutableId = ""$dcrImmutableId"""
#Write-Output "Stream = ""$stream"""

# TODO: Make sure everything works and output useful info

#$sentinelWorkspaceName = $result.properties.outputs.sentinelWorkspaceName.value
#$appFqdn = $result.properties.outputs.appFqdn.value
#$rgName = $result.properties.outputs.rgName.value

#Write-Output "Deployed sentinel workspace $sentinelWorkspaceName"
#Write-Output "Synthetic endpoints available at https://$appFqdn/"
#Write-Output ""

#Write-Output "When finished, run:"
#Write-Output "az group delete --name $rgName"
