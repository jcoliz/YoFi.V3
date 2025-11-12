param(
    [Parameter(Mandatory=$true)]
    [string]
    $Location
)

Write-Output "Deploying environment $EnvironmentName"
az deployment sub create --location $Location --template-file "$PSScriptRoot/main.bicep" --parameters location=$Location --parameters "$PSScriptRoot/main.bicepparam" --what-if

Write-Output "OK"
Write-Output ""

# TODO: Make sure everything works and output useful info

#$sentinelWorkspaceName = $result.properties.outputs.sentinelWorkspaceName.value
#$appFqdn = $result.properties.outputs.appFqdn.value
#$rgName = $result.properties.outputs.rgName.value

#Write-Output "Deployed sentinel workspace $sentinelWorkspaceName"
#Write-Output "Synthetic endpoints available at https://$appFqdn/"
#Write-Output ""

#Write-Output "When finished, run:"
#Write-Output "az group delete --name $rgName"