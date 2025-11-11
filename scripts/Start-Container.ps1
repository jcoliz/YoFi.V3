$ErrorActionPreference = "Stop"
docker compose -f ./docker/docker-compose-ci.yml up -d --wait

Write-Host "Containers are up and running."

Start-Process "http://localhost:5000"
