#
# Start the Docker CI containers locally
#
# Useful for debugging CI build issues locally, and running functional tests locally.
#


$ErrorActionPreference = "Stop"
docker compose -f ./docker/docker-compose-ci.yml up -d --wait

Write-Host "Containers are up and running."

Start-Process "http://localhost:5000"
