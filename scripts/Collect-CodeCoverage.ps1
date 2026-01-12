<#
.SYNOPSIS
Runs unit and controller integration tests and collects combined code coverage metrics.

.DESCRIPTION
This script executes unit tests and controller integration tests with code coverage
collection and generates a combined HTML report. It uses the XPlat Code Coverage collector
and ReportGenerator to create a detailed coverage report that opens automatically in your browser.

.EXAMPLE
.\Collect-CodeCoverage.ps1
Runs unit and controller integration tests and generates a combined code coverage report.

.NOTES
Requires ReportGenerator to be installed globally:
    dotnet tool install -g dotnet-reportgenerator-globaltool

The combined coverage report will be generated in .\bin\result\index.html and opened automatically.

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
    $controllerTestPath = "$repoRoot/tests/Integration.Controller"
    $coverletSettingsPath = "$repoRoot/tests/Unit/coverlet.runsettings"
    $outputDir = "$repoRoot/bin/coverage"

    # Verify test directories exist
    if (-not (Test-Path $unitTestPath)) {
        throw "Unit test directory not found: $unitTestPath"
    }
    if (-not (Test-Path $controllerTestPath)) {
        throw "Controller integration test directory not found: $controllerTestPath"
    }
    if (-not (Test-Path $coverletSettingsPath)) {
        throw "Coverlet settings file not found: $coverletSettingsPath"
    }

    Write-Host "Cleaning up previous coverage results..." -ForegroundColor Cyan
    Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue

    # Run unit tests
    Run-TestsWithCoverage -TestProjectPath $unitTestPath -TestName "unit tests" -SettingsPath $coverletSettingsPath

    # Run controller integration tests
    Run-TestsWithCoverage -TestProjectPath $controllerTestPath -TestName "controller integration tests" -SettingsPath $coverletSettingsPath

    Write-Host "Generating combined coverage report..." -ForegroundColor Cyan
    reportgenerator `
        -reports:"$unitTestPath/TestResults/*/coverage.cobertura.xml;$controllerTestPath/TestResults/*/coverage.cobertura.xml" `
        -targetdir:$outputDir
    if ($LASTEXITCODE -ne 0) {
        throw "Report generation failed with exit code $LASTEXITCODE"
    }

    Write-Host "OK Combined coverage report generated successfully" -ForegroundColor Green
    Write-Host "Opening report in browser..." -ForegroundColor Cyan
    Start-Process "$outputDir/index.html"
}
catch {
    Write-Error "Failed to collect code coverage: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
