<#
.SYNOPSIS
Generates the frontend API client and test control OpenAPI specification.

.DESCRIPTION
This script builds the WireApiHost project, whose build targets generate:
1. TypeScript API client (apiclient.ts) for the Nuxt frontend
2. Test control OpenAPI specification (openapi-test-control.json) for functional tests

The test control C# client is generated separately by the functional test build.

.PARAMETER Configuration
The build configuration to use. Valid values: Debug, Release. Default is Debug.

.EXAMPLE
.\Generate-ApiClient.ps1
Generates the frontend client and test control specification using Debug configuration.

.EXAMPLE
.\Generate-ApiClient.ps1 -Configuration Release
Generates the frontend client and test control specification using Release configuration.

.NOTES
Generated files:
- src/FrontEnd.Nuxt/app/utils/apiclient.ts (TypeScript for frontend)
- tests/Functional/Infrastructure/TestControl/openapi-test-control.json (OpenAPI for tests)

NEVER edit these files manually - they will be overwritten on regeneration.

NSwag configurations:
- tools/WireApiHost/nswag.json (TypeScript client)
- tools/WireApiHost/nswag-test-control.json (test control specification)

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
    $wireApiHostPath = Join-Path $repoRoot "tools\WireApiHost"
    $typeScriptClient = Join-Path $repoRoot "src\FrontEnd.Nuxt\app\utils\apiclient.ts"
    $testControlSpec = Join-Path $repoRoot "tests\Functional\Infrastructure\TestControl\openapi-test-control.json"

    Write-Host "Generating frontend API client and test control specification..." -ForegroundColor Cyan
    Write-Host "  Configuration: $Configuration" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Building WireApiHost..." -ForegroundColor Cyan
    Push-Location $wireApiHostPath
    try {
        dotnet build --configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "WireApiHost build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    # Verify output files were generated
    Write-Host ""
    Write-Host "Verifying generated files..." -ForegroundColor Cyan

    $allGood = $true

    if (Test-Path $typeScriptClient) {
        $tsSize = (Get-Item $typeScriptClient).Length
        Write-Host "  ✓ TypeScript client: $([math]::Round($tsSize / 1KB, 2)) KB" -ForegroundColor Green
        Write-Host "    $typeScriptClient" -ForegroundColor Gray
    }
    else {
        Write-Host "  ✗ TypeScript client not found at: $typeScriptClient" -ForegroundColor Red
        $allGood = $false
    }

    if (Test-Path $testControlSpec) {
        $specSize = (Get-Item $testControlSpec).Length
        Write-Host "  ✓ Test control OpenAPI specification: $([math]::Round($specSize / 1KB, 2)) KB" -ForegroundColor Green
        Write-Host "    $testControlSpec" -ForegroundColor Gray
    }
    else {
        Write-Host "  ✗ Test control OpenAPI specification not found at: $testControlSpec" -ForegroundColor Red
        $allGood = $false
    }

    if (-not $allGood) {
        throw "One or more generated files were not found"
    }

    Write-Host ""
    Write-Host "OK Frontend API client and test control specification generated successfully" -ForegroundColor Green
    Write-Host "The functional test build generates the test control C# client." -ForegroundColor Gray
}
catch {
    Write-Error "Failed to generate frontend API client and test control specification: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
