<#
.SYNOPSIS
Runs unit tests and collects Application and Entities code coverage metrics.

.DESCRIPTION
This script executes the Unit test project with code coverage collection focused on
Application and Entities assemblies. Functional and integration test projects are not
included.

.EXAMPLE
.\Collect-CodeCoverage.ps1
Runs unit tests and generates an Application and Entities code coverage report.

.NOTES
Requires ReportGenerator to be installed globally:
    dotnet tool install -g dotnet-reportgenerator-globaltool

The unit coverage report is generated in .\bin\coverage\ and opens automatically in your browser.

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
        dotnet test --no-build --collect:"XPlat Code Coverage" --settings:$SettingsPath
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
    $coverletSettingsPath = "$testsDir/coverlet.runsettings"
    $outputDir = "$repoRoot/bin/coverage"

    # Verify the unit test project exists
    if (-not (Test-Path $unitTestPath)) {
        throw "Unit test directory not found: $unitTestPath"
    }

    if (-not (Test-Path $coverletSettingsPath)) {
        throw "Coverlet settings file not found: $coverletSettingsPath"
    }

    Write-Host "Collecting unit code coverage for Application and Entities" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Cleaning up previous coverage results..." -ForegroundColor Cyan
    Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue

    # Clean and build the unit test project
    Write-Host "Cleaning solution..." -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        dotnet clean --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw "Clean failed with exit code $LASTEXITCODE"
        }

        Write-Host "Building solution..." -ForegroundColor Cyan
        dotnet build $unitTestPath --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    # Run unit tests with coverage (without rebuilding)
    Run-TestsWithCoverage -TestProjectPath $unitTestPath -TestName "unit tests" -SettingsPath $coverletSettingsPath

    # Generate a report only from unit-test coverage output
    Write-Host "`nGenerating unit code coverage report..." -ForegroundColor Cyan
    reportgenerator `
        -reports:"$unitTestPath/TestResults/*/coverage.cobertura.xml" `
        -targetdir:"$outputDir"
    if ($LASTEXITCODE -ne 0) {
        throw "Report generation failed with exit code $LASTEXITCODE"
    }

    Write-Host "`nOK Unit code coverage report generated successfully" -ForegroundColor Green
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
