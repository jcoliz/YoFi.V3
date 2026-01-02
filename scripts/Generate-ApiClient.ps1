<#
.SYNOPSIS
Generates API clients for the frontend (TypeScript) and functional tests (C#) from the backend API specification.

.DESCRIPTION
This script builds the WireApiHost project and runs NSwag to generate:
1. TypeScript API client (apiclient.ts) for the Nuxt frontend
2. C# API client (ApiClient.cs) for functional tests

The script ensures API clients stay synchronized with backend API changes.

.PARAMETER Configuration
The build configuration to use. Valid values: Debug, Release. Default is Debug.

.EXAMPLE
.\Generate-ApiClient.ps1
Generates both API clients using Debug configuration.

.EXAMPLE
.\Generate-ApiClient.ps1 -Configuration Release
Generates both API clients using Release configuration.

.NOTES
Generated files:
- src/FrontEnd.Nuxt/app/utils/apiclient.ts (TypeScript for frontend)
- tests/Functional/Api/Generated/ApiClient.cs (C# for tests)

NEVER edit these files manually - they will be overwritten on regeneration.

NSwag configurations:
- src/WireApiHost/nswag.json (TypeScript client)
- tests/Functional/Api/nswag.json (C# client)

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
    $functionalTestsPath = "$repoRoot/tests/Functional"
    $typeScriptClient = "$repoRoot/src/FrontEnd.Nuxt/app/utils/apiclient.ts"
    $csharpClient = "$functionalTestsPath/Api/Generated/ApiClient.cs"

    Write-Host "Generating API clients from backend specification..." -ForegroundColor Cyan
    Write-Host "  Configuration: $Configuration" -ForegroundColor Cyan
    Write-Host ""

    # Build the WireApiHost project first
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

    # Generate TypeScript client for frontend
    Write-Host ""
    Write-Host "Generating TypeScript client..." -ForegroundColor Cyan
    Push-Location $wireApiHostPath
    try {
        $nswagExe = (Get-ChildItem "$env:USERPROFILE/.nuget/packages/nswag.msbuild/*/tools/Net100/dotnet-nswag.dll" | Select-Object -First 1).FullName
        if (-not $nswagExe) {
            throw "NSwag executable not found in NuGet packages"
        }

        $msbuildOutput = "bin/$Configuration/net10.0/"
        $variables = "Configuration=$Configuration,MSBuildOutputPath=$msbuildOutput,MSBuildProjectFile=YoFi.V3.WireApiHost.csproj,OutputFile=../FrontEnd.Nuxt/app/utils/apiclient.ts"
        dotnet $nswagExe run nswag.json /variables:$variables

        if ($LASTEXITCODE -ne 0) {
            throw "TypeScript client generation failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    # Generate C# client for functional tests
    Write-Host ""
    Write-Host "Generating C# client for tests..." -ForegroundColor Cyan
    Push-Location $functionalTestsPath
    try {
        # Ensure output directory exists
        $outputDir = Split-Path $csharpClient -Parent
        if (-not (Test-Path $outputDir)) {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        }

        $variables = "OutputFile=$csharpClient,Configuration=$Configuration"
        dotnet $nswagExe run Api/nswag.json /variables:$variables

        if ($LASTEXITCODE -ne 0) {
            throw "C# client generation failed with exit code $LASTEXITCODE"
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

    if (Test-Path $csharpClient) {
        $csSize = (Get-Item $csharpClient).Length
        Write-Host "  ✓ C# client: $([math]::Round($csSize / 1KB, 2)) KB" -ForegroundColor Green
        Write-Host "    $csharpClient" -ForegroundColor Gray
    }
    else {
        Write-Host "  ✗ C# client not found at: $csharpClient" -ForegroundColor Red
        $allGood = $false
    }

    if (-not $allGood) {
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
