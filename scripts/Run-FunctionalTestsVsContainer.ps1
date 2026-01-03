<#
.SYNOPSIS
Runs functional tests against the CI Docker containers.

.DESCRIPTION
This script builds and starts the Docker containers defined in docker-compose-ci.yml,
then runs the functional tests against them. By default, containers are automatically
stopped and cleaned up after the tests complete. Use the -KeepRunning switch to leave
containers running for investigation via the Aspire Dashboard.

.PARAMETER KeepRunning
When specified, leaves containers running after tests complete. Use this to examine
telemetry in the Aspire Dashboard (http://localhost:18888). Remember to run
Stop-Container.ps1 when finished.

.EXAMPLE
.\Run-FunctionalTestsVsContainer.ps1
Builds containers, runs functional tests, and cleans up automatically.

.EXAMPLE
.\Run-FunctionalTestsVsContainer.ps1 -KeepRunning
Runs tests but leaves containers running for dashboard investigation.
Use .\scripts\Stop-Container.ps1 to clean up when finished.

.NOTES
The script uses container.runsettings for test configuration when running against containers.
The solution version is automatically generated using Get-Version.ps1.
The Aspire Dashboard is available at http://localhost:18888 while containers are running.

.LINK
https://docs.docker.com/compose/
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]
    $KeepRunning
)

$ErrorActionPreference = "Stop"

function Test-DockerRunning {
    try {
        $null = docker info 2>&1
        return ($LASTEXITCODE -eq 0)
    }
    catch {
        return $false
    }
}

try {
    if (-not (Test-DockerRunning)) {
        Write-Error "Docker is not running. Please start Docker Desktop and try again."
        exit 1
    }

    $env:SOLUTION_VERSION = & "$PSScriptRoot/Get-Version.ps1" -Stable
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to get version with exit code $LASTEXITCODE"
    }

    Write-Host "Building and starting docker services with solution version $env:SOLUTION_VERSION..." -ForegroundColor Cyan
    docker compose -f "$PSScriptRoot/../docker/docker-compose-ci.yml" up --build -d --wait
    if ($LASTEXITCODE -ne 0) {
        throw "Docker compose up failed with exit code $LASTEXITCODE"
    }

    Write-Host ""
    Write-Host "Aspire Dashboard available at http://localhost:18888" -ForegroundColor Cyan
    Write-Host ""

    Push-Location "$PSScriptRoot/../tests/Functional"
    try {
        Write-Host "Running functional tests..." -ForegroundColor Cyan
        dotnet test .\YoFi.V3.Tests.Functional.csproj -s .\runsettings\container.runsettings
        $testExitCode = $LASTEXITCODE

        if ($testExitCode -ne 0) {
            throw "Tests failed with exit code $testExitCode"
        }

        Write-Host "OK Functional tests completed successfully" -ForegroundColor Green

        if ($KeepRunning) {
            Write-Host ""
            Write-Host "Containers are still running (per -KeepRunning switch)" -ForegroundColor Yellow
            Write-Host "Aspire Dashboard: http://localhost:18888" -ForegroundColor Cyan
            Write-Host "Application: http://localhost:5000" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "Run '.\scripts\Stop-Container.ps1' to stop containers when finished." -ForegroundColor Yellow
        }
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Error "Failed to run functional tests: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    if (-not $KeepRunning) {
        Write-Host "Stopping docker services..." -ForegroundColor Cyan
        docker compose -f "$PSScriptRoot/../docker/docker-compose-ci.yml" down
    }
    Remove-Item env:SOLUTION_VERSION -ErrorAction SilentlyContinue
}
