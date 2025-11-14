#
# Run functional tests in the CI Docker containers
#
# Useful for debugging CI build issues locally, and running functional tests easily.
#

$ErrorActionPreference = "Stop"
Write-Output "Building and starting docker services..."
Invoke-Expression "docker compose -f ./docker/docker-compose-ci.yml up --build -d --wait"

Push-Location ./tests/Functional
Write-Output "Running tests..."
dotnet test .\YoFi.V3.Tests.Functional.csproj -s .\docker.runsettings
Pop-Location

Write-Output "Stopping docker services..."
Invoke-Expression "docker compose -f ./docker/docker-compose-ci.yml down"
