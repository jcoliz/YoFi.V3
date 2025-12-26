<#
.SYNOPSIS
Generates the TypeScript API client for the frontend from the backend API specification.

.DESCRIPTION
This script builds the WireApiHost project, which triggers NSwag to generate the
TypeScript API client (apiclient.ts) for the Nuxt frontend.

The script ensures the frontend API client stays synchronized with backend API changes.

.PARAMETER Configuration
The build configuration to use. Valid values: Debug, Release. Default is Debug.

.EXAMPLE
.\Generate-ApiClient.ps1
Generates the TypeScript API client using Debug configuration.

.EXAMPLE
.\Generate-ApiClient.ps1 -Configuration Release
Generates the TypeScript API client using Release configuration.

.NOTES
Generated file: src/FrontEnd.Nuxt/app/utils/apiclient.ts

NEVER edit this file manually - it will be overwritten on regeneration.

NSwag configuration: src/WireApiHost/nswag.json
Build trigger: WireApiHost project PostBuildEvent

.LINK
https://github.com/RicoSuter/NSwag
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]
    $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

try {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $wireApiHostPath = "$repoRoot/src/WireApiHost"
    $typeScriptClient = "$repoRoot/src/FrontEnd.Nuxt/app/utils/apiclient.ts"

    Push-Location $wireApiHostPath

    Write-Host "Generating TypeScript API client from backend specification..." -ForegroundColor Cyan
    Write-Host "  Configuration: $Configuration" -ForegroundColor Cyan
    Write-Host ""

    # Build the WireApiHost project, which triggers NSwag generation via PostBuildEvent
    dotnet build --configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    # Verify output file was generated
    Write-Host ""
    Write-Host "Verifying generated file..." -ForegroundColor Cyan

    if (Test-Path $typeScriptClient) {
        $tsSize = (Get-Item $typeScriptClient).Length
        Write-Host "  ✓ TypeScript client: $([math]::Round($tsSize / 1KB, 2)) KB" -ForegroundColor Green
        Write-Host "    $typeScriptClient" -ForegroundColor Gray
    }
    else {
        Write-Host "  ✗ TypeScript client not found at: $typeScriptClient" -ForegroundColor Red
        throw "TypeScript API client file was not generated"
    }

    Write-Host ""
    Write-Host "OK TypeScript API client generated successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to generate TypeScript API client: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}
