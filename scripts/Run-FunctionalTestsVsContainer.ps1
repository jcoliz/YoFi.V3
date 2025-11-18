#
# Run functional tests in the CI Docker containers
#
# Useful for debugging CI build issues locally, and running functional tests easily.
#

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
