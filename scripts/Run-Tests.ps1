<#
.SYNOPSIS
    Run unit and integration tests

.DESCRIPTION
    Builds the solution and runs all unit and integration tests.
    Does NOT run functional tests, as those require containers to be running.
    
    Test projects included:
    - tests/Unit - Unit tests for Application layer
    - tests/Integration.Data - Integration tests for Data layer
    
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
    dotnet test tests/Unit --no-build
    $unitTestResult = $LASTEXITCODE

    # Run integration tests
    Write-Host "`nRunning integration tests..." -ForegroundColor Cyan
    dotnet test tests/Integration.Data --no-build
    $integrationTestResult = $LASTEXITCODE

    # Summary
    Write-Host "`n" -NoNewline
    if ($unitTestResult -eq 0 -and $integrationTestResult -eq 0) {
        Write-Host "OK All tests passed" -ForegroundColor Green
    } else {
        if ($unitTestResult -ne 0) {
            Write-Host "WARNING Unit tests failed" -ForegroundColor Yellow
        }
        if ($integrationTestResult -ne 0) {
            Write-Host "WARNING Integration tests failed" -ForegroundColor Yellow
        }
        exit 1
    }

    Write-Host "`nNote: Functional tests were not run." -ForegroundColor Cyan
    Write-Host "To run functional tests: .\scripts\Run-FunctionalTestsVsContainer.ps1"

} finally {
    Pop-Location
}