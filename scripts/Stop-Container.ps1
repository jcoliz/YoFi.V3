<#
.SYNOPSIS
Stops the Docker CI containers locally.

.DESCRIPTION
This script stops and removes the Docker containers defined in docker-compose-ci.yml.
It cleans up all containers, networks, and volumes created by docker-compose up.
Use this script to stop containers started with Start-Container.ps1.

.EXAMPLE
.\Stop-Container.ps1
Stops and removes all Docker CI containers.

.NOTES
This command removes containers and networks but preserves volumes unless explicitly removed.

.LINK
https://docs.docker.com/compose/
#>

docker compose -f ./docker/docker-compose-ci.yml down
