<#
.SYNOPSIS
Generates API clients for frontend and functional tests from the backend API specification.

.DESCRIPTION
This script builds the WireApiHost project, which triggers NSwag to generate:
1. TypeScript API client (apiclient.ts) for the Nuxt frontend
2. C# API client (ApiClient.cs) for functional tests

The script ensures API clients stay synchronized with backend API changes.

.PARAMETER Configuration
The build configuration to use. Valid values: Debug, Release. Default is Debug.

.EXAMPLE
.\Generate-ApiClient.ps1
Generates API clients using Debug configuration.

.EXAMPLE
.\Generate-ApiClient.ps1 -Configuration Release
Generates API clients using Release configuration.

.NOTES
Generated files:
- Frontend: src/FrontEnd.Nuxt/app/utils/apiclient.ts (TypeScript)
- Tests: tests/Functional/Api/ApiClient.cs (C#)

NEVER edit these files manually - they will be overwritten on regeneration.

NSwag configuration: src/WireApiHost/nswag.json
Build trigger: WireApiHost project PostBuildEvent

NOTE: The current generation approach has known technical debt (generated files
checked into source control, cross-project dependencies). See the analysis at:
docs/wip/functional-tests/API-CLIENT-GENERATION-IMPROVEMENT.md

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
    $csharpClient = "$repoRoot/tests/Functional/Api/ApiClient.cs"

    Push-Location $wireApiHostPath

    Write-Host "Generating API clients from backend specification..." -ForegroundColor Cyan
    Write-Host "  Configuration: $Configuration" -ForegroundColor Cyan
    Write-Host ""

    # Build the WireApiHost project, which triggers NSwag generation via PostBuildEvent
    dotnet build --configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    # Verify both output files were generated
    $allGenerated = $true

    Write-Host ""
    Write-Host "Verifying generated files..." -ForegroundColor Cyan

    if (Test-Path $typeScriptClient) {
        $tsSize = (Get-Item $typeScriptClient).Length
        Write-Host "  ✓ TypeScript client: $([math]::Round($tsSize / 1KB, 2)) KB" -ForegroundColor Green
        Write-Host "    $typeScriptClient" -ForegroundColor Gray
    }
    else {
        Write-Host "  ✗ TypeScript client not found at: $typeScriptClient" -ForegroundColor Red
        $allGenerated = $false
    }

    if (Test-Path $csharpClient) {
        $csSize = (Get-Item $csharpClient).Length
        Write-Host "  ✓ C# client: $([math]::Round($csSize / 1KB, 2)) KB" -ForegroundColor Green
        Write-Host "    $csharpClient" -ForegroundColor Gray
    }
    else {
        Write-Host "  ✗ C# client not found at: $csharpClient" -ForegroundColor Red
        $allGenerated = $false
    }

    if (-not $allGenerated) {
        throw "One or more API client files were not generated"
    }

    Write-Host ""
    Write-Host "OK API clients generated successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to generate API clients: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}
