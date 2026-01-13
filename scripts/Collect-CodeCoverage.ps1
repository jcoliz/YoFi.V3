<#
.SYNOPSIS
Runs unit and application integration tests and collects code coverage metrics.

.DESCRIPTION
This script executes ONLY unit tests and application integration tests with code coverage
collection. It generates both a combined HTML report and individual reports per test layer.

Coverage is intentionally limited to Unit + Application Integration tests to drive adoption
of these layers as the primary testing approach. Controller, Data, and Functional tests are
excluded from coverage metrics.

.EXAMPLE
.\Collect-CodeCoverage.ps1
Runs unit and application integration tests and generates code coverage reports.

.NOTES
Requires ReportGenerator to be installed globally:
    dotnet tool install -g dotnet-reportgenerator-globaltool

Reports are generated in:
- .\bin\coverage\combined\ - Combined coverage from Unit + Application Integration
- .\bin\coverage\unit\ - Coverage from unit tests only
- .\bin\coverage\application\ - Coverage from application integration tests only

The combined report opens automatically in your browser.

.LINK
https://github.com/danielpalme/ReportGenerator
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Run-TestsWithCoverage {
    param(
        [string]$TestProjectPath,
        [string]$TestName,
        [string]$SettingsPath
    )

    Write-Host "Running $TestName with code coverage..." -ForegroundColor Cyan
    Push-Location $TestProjectPath
    try {
        Remove-Item TestResults -Recurse -Force -ErrorAction SilentlyContinue
        dotnet clean --nologo --verbosity quiet
        dotnet test --collect:"XPlat Code Coverage" --settings:$SettingsPath
        if ($LASTEXITCODE -ne 0) {
            throw "$TestName execution failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

try {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $unitTestPath = "$repoRoot/tests/Unit"
    $appTestPath = "$repoRoot/tests/Integration.Application"
    $coverletSettingsPath = "$repoRoot/tests/Unit/coverlet.runsettings"
    $outputDir = "$repoRoot/bin/coverage"

    # Verify test directories exist
    if (-not (Test-Path $unitTestPath)) {
        throw "Unit test directory not found: $unitTestPath"
    }
    if (-not (Test-Path $appTestPath)) {
        throw "Application integration test directory not found: $appTestPath"
    }
    if (-not (Test-Path $coverletSettingsPath)) {
        throw "Coverlet settings file not found: $coverletSettingsPath"
    }

    Write-Host "Collecting code coverage from Unit + Application Integration tests only" -ForegroundColor Cyan
    Write-Host "(Controller, Data, and Functional tests excluded from coverage)" -ForegroundColor Gray
    Write-Host ""

    Write-Host "Cleaning up previous coverage results..." -ForegroundColor Cyan
    Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue

    # Run unit tests
    Run-TestsWithCoverage -TestProjectPath $unitTestPath -TestName "unit tests" -SettingsPath $coverletSettingsPath

    # Run application integration tests (PRIMARY coverage layer)
    Run-TestsWithCoverage -TestProjectPath $appTestPath -TestName "application integration tests" -SettingsPath $coverletSettingsPath

    # Generate individual reports per test layer
    Write-Host "`nGenerating per-layer coverage reports..." -ForegroundColor Cyan

    Write-Host "  - Unit test coverage..." -ForegroundColor Gray
    reportgenerator `
        -reports:"$unitTestPath/TestResults/*/coverage.cobertura.xml" `
        -targetdir:"$outputDir/unit" `
        -reporttypes:"Html" | Out-Null

    Write-Host "  - Application integration test coverage..." -ForegroundColor Gray
    reportgenerator `
        -reports:"$appTestPath/TestResults/*/coverage.cobertura.xml" `
        -targetdir:"$outputDir/application" `
        -reporttypes:"Html" | Out-Null

    # Generate combined coverage report (Unit + Application Integration only)
    Write-Host "`nGenerating combined coverage report (Unit + Application Integration)..." -ForegroundColor Cyan
    reportgenerator `
        -reports:"$unitTestPath/TestResults/*/coverage.cobertura.xml;$appTestPath/TestResults/*/coverage.cobertura.xml" `
        -targetdir:"$outputDir/combined"
    if ($LASTEXITCODE -ne 0) {
        throw "Report generation failed with exit code $LASTEXITCODE"
    }

    Write-Host "`nOK Coverage reports generated successfully" -ForegroundColor Green
    Write-Host "`nCoverage reports available at:" -ForegroundColor Cyan
    Write-Host "  Combined (Unit + App):  $outputDir/combined/index.html" -ForegroundColor White
    Write-Host "  Unit only:              $outputDir/unit/index.html" -ForegroundColor White
    Write-Host "  Application only:       $outputDir/application/index.html" -ForegroundColor White
    Write-Host "`nOpening combined report in browser..." -ForegroundColor Cyan
    Start-Process "$outputDir/combined/index.html"
}
catch {
    Write-Error "Failed to collect code coverage: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
