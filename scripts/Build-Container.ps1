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

try {
    $env:SOLUTION_VERSION = & ./scripts/Get-Version.ps1
    $env:SOLUTION_VERSION = "$env:SOLUTION_VERSION-bcps" # Tag as built by this script
    docker compose -f ./docker/docker-compose-ci.yml build
    Write-Output "Built Docker CI containers with solution version $env:SOLUTION_VERSION"
}
finally {
    Remove-Item env:SOLUTION_VERSION -ErrorAction SilentlyContinue
}
