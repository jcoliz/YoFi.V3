<#
.SYNOPSIS
    Run selected unit, integration, functional, and frontend component tests.

.DESCRIPTION
    Runs unit and frontend component tests by default. Use -Unit, -Integration, -Functional, and -FrontEnd in any
    combination to select test suites. Each selected .NET test project is built by
    dotnet test; the solution is not built.

    Test projects included:
    - tests/Unit - Unit tests for Application layer
    - tests/Integration.Application - Integration tests for Application layer (PRIMARY)
    - tests/Integration.Data - Integration tests for Data layer
    - tests/Integration.Controller - Integration tests for Controller layer
    - tests/Functional - Functional tests against running containers
    - src/FrontEnd.Nuxt/tests - Frontend component tests via npm run test:components

    Functional tests require Docker and the application containers to be running.
    Use Run-FunctionalTestsVsContainer.ps1 to manage containers automatically.

.PARAMETER Unit
    Run unit tests.

.PARAMETER Integration
    Run all three integration test projects.

.PARAMETER Functional
    Check Docker availability and run functional tests.

.PARAMETER Filter
    Pass a dotnet test filter expression to every selected .NET test project.
    Frontend component tests always run in full.

.PARAMETER FrontEnd
    Run frontend component tests. Requires Node and installed frontend dependencies.

.EXAMPLE
    .\Run-Tests.ps1 -FrontEnd

    Runs frontend component tests without running .NET tests.

.EXAMPLE
    .\Run-Tests.ps1

    Runs unit and frontend component tests.

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
    [switch]$FrontEnd,
    [string]$Filter
)

$ErrorActionPreference = "Stop"

if (-not $Unit -and -not $Integration -and -not $Functional -and -not $FrontEnd) {
    $Unit = $true
    $FrontEnd = $true
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
    if ($FrontEnd) {
        $selectedSuites += @{ Name = "FrontEnd"; Path = "src\FrontEnd.Nuxt" }
    }

    # Strip ANSI escape sequences from a string for reliable regex matching.
    function Remove-AnsiEscapes {
        param([string]$Text)
        return [System.Text.RegularExpressions.Regex]::Replace($Text, '\x1B\[[0-9;]*[mGKHF]', '')
    }

    # Parse test counts and durations from output
    function Parse-TestResults {
        param([string]$Output)

        # Strip ANSI codes so regex patterns work regardless of terminal color output.
        $plain = Remove-AnsiEscapes $Output

        # Vitest format: "Tests  71 passed (71)" or "Tests  1 failed | 70 passed (71)"
        # Duration: "Duration  1.65s (...)"
        if ($plain -match 'Tests\s+(?:\d+ failed \| )?(\d+) passed') {
            $count = [int]$Matches[1]
            $duration = "N/A"
            if ($plain -match 'Duration\s+([\d.]+[smh]+)') {
                $duration = $Matches[1]
            }
            return @{ Count = $count; Duration = $duration }
        }

        # dotnet test format: "Passed: N ... Duration: X"
        # Capture the full duration up to the assembly suffix, including values like "1 m 27 s".
        if ($plain -match 'Passed:\s+(\d+).*?Duration:\s+([^\r\n]+?)(?:\s+-\s+|[\r\n]|$)') {
            return @{
                Count = [int]$Matches[1]
                Duration = $Matches[2].Trim()
            }
        }

        # node:test format: "pass N"
        if ($plain -match '(?m)^[^\r\n]*?\bpass\s+(\d+)\s*$') {
            $count = [int]$Matches[1]
            $duration = "N/A"
            if ($plain -match '(?m)^[^\r\n]*?\bduration_ms\s+([\d.]+)\s*$') {
                $duration = "$($Matches[1]) ms"
            }
            return @{ Count = $count; Duration = $duration }
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
        if ($suite.Name -eq "FrontEnd") {
            Push-Location $suite.Path
            try {
                & npm run test:components 2>&1 | Tee-Object -Variable capturedOutput | ForEach-Object { Write-Host $_ }
                $suite.ExitCode = $LASTEXITCODE
            } finally {
                Pop-Location
            }
            $suite.Stats = Parse-TestResults -Output ($capturedOutput -join "`n")
            continue
        }

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
