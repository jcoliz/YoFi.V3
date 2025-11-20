<#
.SYNOPSIS
Builds the Docker CI containers locally.

.DESCRIPTION
This script builds the Docker containers defined in docker-compose-ci.yml for local testing.
It sets the solution version using Get-Version.ps1 and tags the built containers appropriately.
This is useful for debugging CI build issues locally and running functional tests locally.

.EXAMPLE
.\Build-Container.ps1
Builds the Docker CI containers with an auto-generated version tag.

.NOTES
The built containers will be tagged with the solution version followed by "-bcps" suffix
to indicate they were built by this script.

.LINK
https://docs.docker.com/compose/
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

try {
    $env:SOLUTION_VERSION = & ./scripts/Get-Version.ps1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to get version with exit code $LASTEXITCODE"
    }
    
    $env:SOLUTION_VERSION = "$env:SOLUTION_VERSION-bcps"
    Write-Verbose "Building containers with version: $env:SOLUTION_VERSION"
    
    docker compose -f ./docker/docker-compose-ci.yml build
    if ($LASTEXITCODE -ne 0) {
        throw "Docker build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "Built Docker CI containers with solution version $env:SOLUTION_VERSION" -ForegroundColor Green
}
catch {
    Write-Error "Failed to build containers: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Remove-Item env:SOLUTION_VERSION -ErrorAction SilentlyContinue
}
