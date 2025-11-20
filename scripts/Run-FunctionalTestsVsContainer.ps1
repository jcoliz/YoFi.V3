<#
.SYNOPSIS
Runs functional tests against the CI Docker containers.

.DESCRIPTION
This script builds and starts the Docker containers defined in docker-compose-ci.yml,
then runs the functional tests against them. The containers are automatically stopped
and cleaned up after the tests complete. This is useful for debugging CI build issues
locally and running functional tests in a containerized environment.

.EXAMPLE
.\Run-FunctionalTestsVsContainer.ps1
Builds containers, runs functional tests, and cleans up.

.NOTES
The script uses docker.runsettings for test configuration when running against containers.
The solution version is automatically generated using Get-Version.ps1.
Containers are always stopped after test execution, even if tests fail.

.LINK
https://docs.docker.com/compose/
#>

[CmdletBinding()]
param()

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

    $env:SOLUTION_VERSION = & ./scripts/Get-Version.ps1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to get version with exit code $LASTEXITCODE"
    }

    Write-Host "Building and starting docker services with solution version $env:SOLUTION_VERSION..." -ForegroundColor Cyan
    docker compose -f ./docker/docker-compose-ci.yml up --build -d --wait
    if ($LASTEXITCODE -ne 0) {
        throw "Docker compose up failed with exit code $LASTEXITCODE"
    }

    Push-Location ./tests/Functional

    Write-Host "Running functional tests..." -ForegroundColor Cyan
    dotnet test .\YoFi.V3.Tests.Functional.csproj -s .\docker.runsettings
    $testExitCode = $LASTEXITCODE

    Pop-Location

    if ($testExitCode -ne 0) {
        throw "Tests failed with exit code $testExitCode"
    }

    Write-Host "Functional tests completed successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to run functional tests: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Write-Host "Stopping docker services..." -ForegroundColor Cyan
    docker compose -f ./docker/docker-compose-ci.yml down
    Remove-Item env:SOLUTION_VERSION -ErrorAction SilentlyContinue
}
