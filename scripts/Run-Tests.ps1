<#
.SYNOPSIS
    Run selected unit, integration, and functional tests.

.DESCRIPTION
    Runs unit tests by default. Use -Unit, -Integration, and -Functional in any
    combination to select test suites. Each selected test project is built by
    dotnet test; the solution is not built.

    Test projects included:
    - tests/Unit - Unit tests for Application layer
    - tests/Integration.Application - Integration tests for Application layer (PRIMARY)
    - tests/Integration.Data - Integration tests for Data layer
    - tests/Integration.Controller - Integration tests for Controller layer
    - tests/Functional - Functional tests against running containers

    Functional tests require Docker and the application containers to be running.
    Use Run-FunctionalTestsVsContainer.ps1 to manage containers automatically.

.PARAMETER Unit
    Run unit tests.

.PARAMETER Integration
    Run all three integration test projects.

.PARAMETER Functional
    Check Docker availability and run functional tests.

.PARAMETER Filter
    Pass a dotnet test filter expression to every selected test project.

.EXAMPLE
    .\Run-Tests.ps1

    Runs unit tests.

.EXAMPLE
    .\Run-Tests.ps1 -Unit -Integration -Filter "FullyQualifiedName~Transaction"

    Runs matching unit and integration tests.

.EXAMPLE
    .\Run-Tests.ps1 -Functional

    Checks Docker and runs functional tests against running containers.
#>

[CmdletBinding()]
param(
    [switch]$Unit,
    [switch]$Integration,
    [switch]$Functional,
    [string]$Filter
)

$ErrorActionPreference = "Stop"

if (-not $Unit -and -not $Integration -and -not $Functional) {
    $Unit = $true
}

if ($Functional) {
    Import-Module (Join-Path $PSScriptRoot "DockerUtilities.psm1") -Force
}

# Ensure we're in the repository root
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

try {
    $selectedSuites = @()
    if ($Unit) {
        $selectedSuites += @{ Name = "Unit"; Path = "tests\Unit" }
    }
    if ($Integration) {
        $selectedSuites += @(
            @{ Name = "Application Integration"; Path = "tests\Integration.Application" }
            @{ Name = "Data Integration"; Path = "tests\Integration.Data" }
            @{ Name = "Controller Integration"; Path = "tests\Integration.Controller" }
        )
    }
    if ($Functional) {
        $selectedSuites += @{ Name = "Functional"; Path = "tests\Functional" }
    }

    # Parse test counts and durations from output
    function Parse-TestResults {
        param([string]$Output)

        # Match "Passed! - Failed: 0, Passed: 249, Skipped: 0, Total: 249, Duration: 1 s" or "Duration: 676 ms"
        if ($Output -match 'Passed:\s+(\d+).*?Duration:\s+([\d\.]+ (?:ms|s))') {
            return @{
                Count = [int]$Matches[1]
                Duration = $Matches[2]
            }
        }
        return @{ Count = 0; Duration = "N/A" }
    }

    Write-Host "Running selected tests..." -ForegroundColor Cyan
    foreach ($suite in $selectedSuites) {
        if ($suite.Name -eq "Functional") {
            Write-Host "`nChecking Docker availability..." -ForegroundColor Cyan
            if (-not (Test-DockerRunning)) {
                Write-Host "ERROR Docker is not running or not available. Start Docker Desktop or the Docker daemon and try again." -ForegroundColor Red
                exit 1
            }
        }

        Write-Host "`nRunning $($suite.Name) tests..." -ForegroundColor Cyan
        $testArgs = @('test', $suite.Path)
        if (-not [string]::IsNullOrWhiteSpace($Filter)) {
            $testArgs += @('--filter', $Filter)
        }

        & dotnet @testArgs 2>&1 | Tee-Object -Variable capturedOutput | ForEach-Object { Write-Host $_ }
        $suite.ExitCode = $LASTEXITCODE
        $suite.Stats = Parse-TestResults -Output ($capturedOutput -join "`n")
    }

    # Summary
    Write-Host "`n==================== TEST SUMMARY ====================" -ForegroundColor Cyan
    $totalTests = 0
    $hasFailures = $false
    foreach ($suite in $selectedSuites) {
        $totalTests += $suite.Stats.Count
        Write-Host "$($suite.Name.PadRight(24)) $($suite.Stats.Count.ToString().PadLeft(4)) tests in $($suite.Stats.Duration)" -ForegroundColor White
        if ($suite.ExitCode -ne 0) {
            Write-Host "WARNING $($suite.Name) tests failed" -ForegroundColor Yellow
            $hasFailures = $true
        }
    }
    Write-Host "======================================================" -ForegroundColor Cyan
    Write-Host "TOTAL:                    $($totalTests.ToString().PadLeft(4)) tests" -ForegroundColor Cyan
    Write-Host "======================================================`n" -ForegroundColor Cyan

    if ($hasFailures) {
        exit 1
    }

    Write-Host "OK All selected tests passed" -ForegroundColor Green
    exit 0

} finally {
    Pop-Location
}
