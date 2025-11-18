#
# Build the Docker CI containers locally
#
# Useful for debugging CI build issues locally, and running functional tests locally.
#

try {
    $env:SOLUTION_VERSION = & ./scripts/Get-Version.ps1
    docker compose -f ./docker/docker-compose-ci.yml build
    Write-Output "Built Docker CI containers with solution version $env:SOLUTION_VERSION"
}
finally {
    Remove-Item env:SOLUTION_VERSION -ErrorAction SilentlyContinue
}
