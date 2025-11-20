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

$ErrorActionPreference = "Stop"

try {
    $env:SOLUTION_VERSION = & ./scripts/Get-Version.ps1
    Write-Output "Building and starting docker services with solution version $env:SOLUTION_VERSION..."
    docker compose -f ./docker/docker-compose-ci.yml up --build -d --wait

    Push-Location ./tests/Functional
    Write-Output "Running tests..."
    dotnet test .\YoFi.V3.Tests.Functional.csproj -s .\docker.runsettings
    Pop-Location
}
finally {
    Write-Output "Stopping docker services..."
    docker compose -f ./docker/docker-compose-ci.yml down
    Remove-Item env:SOLUTION_VERSION -ErrorAction SilentlyContinue
}
