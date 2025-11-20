<#
.SYNOPSIS
Starts the Docker CI containers locally.

.DESCRIPTION
This script starts the Docker containers defined in docker-compose-ci.yml in detached mode
and waits for them to be ready. Once the containers are running, it automatically opens
the application in a browser at http://localhost:5000. This is useful for debugging CI
build issues locally and running functional tests locally.

.EXAMPLE
.\Start-Container.ps1
Starts the Docker containers and opens the application in a browser.

.NOTES
The containers run in detached mode (-d) and the script waits (--wait) for them to be ready.
Use Stop-Container.ps1 to stop the containers when finished.

.LINK
https://docs.docker.com/compose/
#>


$ErrorActionPreference = "Stop"
docker compose -f ./docker/docker-compose-ci.yml up -d --wait

Write-Host "Containers are up and running."

Start-Process "http://localhost:5000"
