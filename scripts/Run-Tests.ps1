<#
.SYNOPSIS
    Run unit and integration tests

.DESCRIPTION
    Builds the solution and runs all unit and integration tests.
    Does NOT run functional tests, as those require containers to be running.

    Test projects included:
    - tests/Unit - Unit tests for Application layer
    - tests/Integration.Application - Integration tests for Application layer (PRIMARY)
    - tests/Integration.Data - Integration tests for Data layer
    - tests/Integration.Controller - Integration tests for Controller layer

    Functional tests are excluded because they require running containers.
    To run functional tests, use Run-FunctionalTestsVsContainer.ps1 instead.

.EXAMPLE
    .\Run-Tests.ps1

    Builds the solution and runs all unit and integration tests.

.NOTES
    This script does not run functional tests.
    For functional tests, use: .\Run-FunctionalTestsVsContainer.ps1
#>

[CmdletBinding()]
param()

# Ensure we're in the repository root
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

try {
    Write-Host "Running unit and integration tests..." -ForegroundColor Cyan

    # Build solution
    Write-Host "`nBuilding solution..." -ForegroundColor Cyan
    dotnet build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK Build succeeded" -ForegroundColor Green

    # Run unit tests
    Write-Host "`nRunning unit tests..." -ForegroundColor Cyan
    $unitOutput = dotnet test tests/Unit --no-build 2>&1
    $unitTestResult = $LASTEXITCODE
    Write-Host $unitOutput

    # Run application integration tests
    Write-Host "`nRunning application integration tests..." -ForegroundColor Cyan
    $appOutput = dotnet test tests/Integration.Application --no-build 2>&1
    $appIntegrationTestResult = $LASTEXITCODE
    Write-Host $appOutput

    # Run data integration tests
    Write-Host "`nRunning data integration tests..." -ForegroundColor Cyan
    $dataOutput = dotnet test tests/Integration.Data --no-build 2>&1
    $dataIntegrationTestResult = $LASTEXITCODE
    Write-Host $dataOutput

    # Run controller integration tests
    Write-Host "`nRunning controller integration tests..." -ForegroundColor Cyan
    $controllerOutput = dotnet test tests/Integration.Controller --no-build 2>&1
    $controllerIntegrationTestResult = $LASTEXITCODE
    Write-Host $controllerOutput

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

    $unitStats = Parse-TestResults -Output $unitOutput
    $appStats = Parse-TestResults -Output $appOutput
    $dataStats = Parse-TestResults -Output $dataOutput
    $controllerStats = Parse-TestResults -Output $controllerOutput
    $totalTests = $unitStats.Count + $appStats.Count + $dataStats.Count + $controllerStats.Count

    # Summary
    Write-Host "`n==================== TEST SUMMARY ====================" -ForegroundColor Cyan
    Write-Host "Unit Tests:               $($unitStats.Count.ToString().PadLeft(4)) tests in $($unitStats.Duration)" -ForegroundColor White
    Write-Host "Application Integration:  $($appStats.Count.ToString().PadLeft(4)) tests in $($appStats.Duration)" -ForegroundColor White
    Write-Host "Data Integration:         $($dataStats.Count.ToString().PadLeft(4)) tests in $($dataStats.Duration)" -ForegroundColor White
    Write-Host "Controller Integration:   $($controllerStats.Count.ToString().PadLeft(4)) tests in $($controllerStats.Duration)" -ForegroundColor White
    Write-Host "======================================================" -ForegroundColor Cyan
    Write-Host "TOTAL:                    $($totalTests.ToString().PadLeft(4)) tests" -ForegroundColor Cyan
    Write-Host "======================================================`n" -ForegroundColor Cyan

    if ($unitTestResult -eq 0 -and $appIntegrationTestResult -eq 0 -and $dataIntegrationTestResult -eq 0 -and $controllerIntegrationTestResult -eq 0) {
        Write-Host "OK All tests passed" -ForegroundColor Green
    } else {
        if ($unitTestResult -ne 0) {
            Write-Host "WARNING Unit tests failed" -ForegroundColor Yellow
        }
        if ($appIntegrationTestResult -ne 0) {
            Write-Host "WARNING Application integration tests failed" -ForegroundColor Yellow
        }
        if ($dataIntegrationTestResult -ne 0) {
            Write-Host "WARNING Data integration tests failed" -ForegroundColor Yellow
        }
        if ($controllerIntegrationTestResult -ne 0) {
            Write-Host "WARNING Controller integration tests failed" -ForegroundColor Yellow
        }
        exit 1
    }

    Write-Host "`nNote: Functional tests were not run." -ForegroundColor Cyan
    Write-Host "To run functional tests: .\scripts\Run-FunctionalTestsVsContainer.ps1"

} finally {
    Pop-Location
}
