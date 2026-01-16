<#
.SYNOPSIS
Runs all unit and integration tests and collects combined code coverage metrics.

.DESCRIPTION
This script executes unit tests and all three integration test projects (Application,
Controller, Data) with code coverage collection across the entire application surface area.
It generates a single combined HTML report showing coverage from all test layers.

Coverage includes all backend code: Application, Entities, Data.Sqlite, Controllers, and BackEnd.
Functional tests are excluded as they test through the browser.

.EXAMPLE
.\Collect-CodeCoverage.ps1
Runs all unit and integration tests and generates a combined code coverage report.

.NOTES
Requires ReportGenerator to be installed globally:
    dotnet tool install -g dotnet-reportgenerator-globaltool

The combined report is generated in .\bin\coverage\ and opens automatically in your browser.

Coverage configuration is defined in tests\coverlet.runsettings.

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
    $testsDir = "$repoRoot/tests"
    $unitTestPath = "$testsDir/Unit"
    $appTestPath = "$testsDir/Integration.Application"
    $controllerTestPath = "$testsDir/Integration.Controller"
    $dataTestPath = "$testsDir/Integration.Data"
    $coverletSettingsPath = "$testsDir/coverlet.runsettings"
    $outputDir = "$repoRoot/bin/coverage"

    # Verify test directories exist
    $testPaths = @{
        "Unit" = $unitTestPath
        "Integration.Application" = $appTestPath
        "Integration.Controller" = $controllerTestPath
        "Integration.Data" = $dataTestPath
    }

    foreach ($testInfo in $testPaths.GetEnumerator()) {
        if (-not (Test-Path $testInfo.Value)) {
            throw "$($testInfo.Key) test directory not found: $($testInfo.Value)"
        }
    }

    if (-not (Test-Path $coverletSettingsPath)) {
        throw "Coverlet settings file not found: $coverletSettingsPath"
    }

    Write-Host "Collecting combined code coverage from all test layers" -ForegroundColor Cyan
    Write-Host "(Unit + Integration.Application + Integration.Controller + Integration.Data)" -ForegroundColor Gray
    Write-Host ""

    Write-Host "Cleaning up previous coverage results..." -ForegroundColor Cyan
    Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue

    # Run all test projects with coverage
    Run-TestsWithCoverage -TestProjectPath $unitTestPath -TestName "unit tests" -SettingsPath $coverletSettingsPath
    Run-TestsWithCoverage -TestProjectPath $appTestPath -TestName "application integration tests" -SettingsPath $coverletSettingsPath
    Run-TestsWithCoverage -TestProjectPath $controllerTestPath -TestName "controller integration tests" -SettingsPath $coverletSettingsPath
    Run-TestsWithCoverage -TestProjectPath $dataTestPath -TestName "data integration tests" -SettingsPath $coverletSettingsPath

    # Generate combined coverage report from all test layers
    Write-Host "`nGenerating combined coverage report from all test layers..." -ForegroundColor Cyan
    reportgenerator `
        -reports:"$unitTestPath/TestResults/*/coverage.cobertura.xml;$appTestPath/TestResults/*/coverage.cobertura.xml;$controllerTestPath/TestResults/*/coverage.cobertura.xml;$dataTestPath/TestResults/*/coverage.cobertura.xml" `
        -targetdir:"$outputDir"
    if ($LASTEXITCODE -ne 0) {
        throw "Report generation failed with exit code $LASTEXITCODE"
    }

    Write-Host "`nOK Combined coverage report generated successfully" -ForegroundColor Green
    Write-Host "`nCoverage report available at:" -ForegroundColor Cyan
    Write-Host "  $outputDir/index.html" -ForegroundColor White
    Write-Host "`nOpening report in browser..." -ForegroundColor Cyan
    Start-Process "$outputDir/index.html"
}
catch {
    Write-Error "Failed to collect code coverage: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
