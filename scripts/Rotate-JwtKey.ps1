<#
.SYNOPSIS
Rotates the JWT signing key for a deployed YoFi.V3 application.

.DESCRIPTION
This script generates a new cryptographically secure JWT signing key and updates
the Azure App Service application setting. This operation will immediately invalidate
all existing JWT tokens, requiring users to re-authenticate.

.PARAMETER ResourceGroup
The name of the Azure Resource Group containing the App Service. This parameter is required.

.PARAMETER AppServiceName
The name of the Azure App Service (backend) to update. This parameter is required.

.PARAMETER Confirm
When specified, prompts for confirmation before rotating the key.
Use -Confirm:$false to skip confirmation (useful for automation).

.EXAMPLE
.\Rotate-JwtKey.ps1 -ResourceGroup "yofi-rg" -AppServiceName "web-abc123"
Generates a new JWT key and updates the App Service after confirmation.

.EXAMPLE
.\Rotate-JwtKey.ps1 -ResourceGroup "yofi-rg" -AppServiceName "web-abc123" -Confirm:$false
Rotates the JWT key without confirmation prompt.

.NOTES
Prerequisites:
- Azure CLI must be installed and authenticated
- User must have permissions to update App Service settings
- WARNING: This operation invalidates all existing JWT tokens immediately

.LINK
https://learn.microsoft.com/en-us/cli/azure/webapp/config/appsettings
#>

[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [Parameter(Mandatory=$true)]
    [string]
    $ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]
    $AppServiceName
)

$ErrorActionPreference = "Stop"

try {
    Write-Host "JWT Key Rotation for App Service: $AppServiceName" -ForegroundColor Cyan
    Write-Host ""

    # Verify App Service exists
    Write-Verbose "Verifying App Service exists..."
    $appServiceCheck = az webapp show --name $AppServiceName --resource-group $ResourceGroup 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "App Service '$AppServiceName' not found in resource group '$ResourceGroup'. Please verify the names and try again."
    }

    Write-Host "Current App Service: $AppServiceName" -ForegroundColor Green
    Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Green
    Write-Host ""

    # Retrieve current JWT settings to display
    Write-Verbose "Retrieving current JWT configuration..."
    $currentSettings = az webapp config appsettings list --name $AppServiceName --resource-group $ResourceGroup | ConvertFrom-Json
    $currentIssuer = ($currentSettings | Where-Object { $_.name -eq 'Jwt__Issuer' }).value
    $currentAudience = ($currentSettings | Where-Object { $_.name -eq 'Jwt__Audience' }).value
    $currentLifespan = ($currentSettings | Where-Object { $_.name -eq 'Jwt__Lifespan' }).value

    if ($currentIssuer) {
        Write-Host "Current JWT Configuration:" -ForegroundColor Cyan
        Write-Output "  Issuer: $currentIssuer"
        Write-Output "  Audience: $currentAudience"
        Write-Output "  Lifespan: $currentLifespan"
        Write-Output "  Key: [REDACTED - will be rotated]"
        Write-Host ""
    }
    else {
        Write-Host "WARNING: No existing JWT configuration found. This may be a new deployment." -ForegroundColor Yellow
        Write-Host ""
    }

    # Generate new JWT key
    Write-Host "Generating new JWT signing key..." -ForegroundColor Cyan
    $jwtKeyBytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($jwtKeyBytes)
    $newJwtKey = [Convert]::ToBase64String($jwtKeyBytes)
    Write-Verbose "Generated new JWT key: $newJwtKey"
    Write-Host "OK New key generated (256-bit cryptographically secure)" -ForegroundColor Green
    Write-Host ""

    # Confirm action
    Write-Host "WARNING: This operation will:" -ForegroundColor Yellow
    Write-Host "  - Replace the current JWT signing key" -ForegroundColor Yellow
    Write-Host "  - Invalidate ALL existing JWT tokens immediately" -ForegroundColor Yellow
    Write-Host "  - Require all users to re-authenticate" -ForegroundColor Yellow
    Write-Host ""

    if ($PSCmdlet.ShouldProcess($AppServiceName, "Rotate JWT signing key")) {
        Write-Host "Updating App Service application settings..." -ForegroundColor Cyan

        $updateResult = az webapp config appsettings set `
            --name $AppServiceName `
            --resource-group $ResourceGroup `
            --settings "Jwt__Key=$newJwtKey" `
            --output none

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update App Service settings with exit code $LASTEXITCODE"
        }

        Write-Host "OK JWT key updated successfully" -ForegroundColor Green
        Write-Host ""
        Write-Host "New JWT Configuration:" -ForegroundColor Cyan
        Write-Output "  Issuer: $currentIssuer"
        Write-Output "  Audience: $currentAudience"
        Write-Output "  Lifespan: $currentLifespan"
        Write-Output "  Key: $newJwtKey"
        Write-Host ""
        Write-Host "IMPORTANT: Store the new JWT key securely for backup/recovery purposes." -ForegroundColor Yellow
        Write-Host "All existing user sessions have been invalidated and users must re-authenticate." -ForegroundColor Yellow
    }
    else {
        Write-Host "Key rotation cancelled by user." -ForegroundColor Yellow
        exit 0
    }
}
catch {
    Write-Error "Failed to rotate JWT key: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
