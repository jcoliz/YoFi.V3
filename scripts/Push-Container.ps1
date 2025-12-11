<#
.SYNOPSIS
Pushes Docker CI containers to DockerHub.

.DESCRIPTION
This script pushes the Docker containers defined in docker-compose-ci.yml to DockerHub.
It uses the REGISTRY_PREFIX from the .env file and tags containers with the solution version.
The script optionally builds the containers before pushing if -Build is specified.

.PARAMETER Build
When specified, builds the containers before pushing them.

.PARAMETER Tag
Optional custom tag to use instead of the auto-generated version. If not specified,
uses the solution version from Get-Version.ps1.

.EXAMPLE
.\Push-Container.ps1
Pushes the existing Docker CI containers to DockerHub with an auto-generated version tag.

.EXAMPLE
.\Push-Container.ps1 -Build
Builds and then pushes the Docker CI containers to DockerHub.

.EXAMPLE
.\Push-Container.ps1 -Tag "1.2.3"
Pushes the containers with a custom tag "1.2.3".

.EXAMPLE
.\Push-Container.ps1 -Build -Tag "latest"
Builds and pushes the containers with the "latest" tag.

.NOTES
Requires Docker to be running and you must be logged in to DockerHub (docker login).
The REGISTRY_PREFIX is read from docker/.env file (defaults to empty string if not set).

.LINK
https://docs.docker.com/compose/
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]
    $Build,

    [Parameter()]
    [string]
    $Tag
)

$ErrorActionPreference = "Stop"

function Test-DockerRunning {
    try {
        $null = docker info 2>&1
        return ($LASTEXITCODE -eq 0)
    }
    catch {
        return $false
    }
}

function Test-DockerLoggedIn {
    try {
        # Try to get current login status - this works if logged in
        $result = docker system info --format '{{.RegistryConfig.IndexConfigs}}' 2>&1
        return ($LASTEXITCODE -eq 0)
    }
    catch {
        return $false
    }
}

function Get-RegistryPrefix {
    $envFile = "$PSScriptRoot/../docker/.env"
    if (Test-Path $envFile) {
        $content = Get-Content $envFile
        foreach ($line in $content) {
            if ($line -match '^REGISTRY_PREFIX=(.+)$') {
                return $matches[1]
            }
        }
    }
    return ""
}

try {
    if (-not (Test-DockerRunning)) {
        throw "Docker is not running. Please start Docker Desktop and try again."
    }

    if (-not (Test-DockerLoggedIn)) {
        Write-Host "WARNING: You may not be logged in to DockerHub." -ForegroundColor Yellow
        Write-Host "If push fails, run 'docker login' first." -ForegroundColor Yellow
        Write-Host ""
    }

    # Get registry prefix from .env file
    $registryPrefix = Get-RegistryPrefix
    if ([string]::IsNullOrWhiteSpace($registryPrefix)) {
        Write-Host "WARNING: REGISTRY_PREFIX not set in docker/.env file." -ForegroundColor Yellow
        Write-Host "Images will be pushed without a registry prefix." -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "Using registry prefix: $registryPrefix" -ForegroundColor Cyan
    }

    # Determine the version tag
    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $env:SOLUTION_VERSION = & "$PSScriptRoot/Get-Version.ps1" -Stable
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to get version with exit code $LASTEXITCODE"
        }
        $versionTag = $env:SOLUTION_VERSION
        Write-Host "Using auto-generated version tag: $versionTag" -ForegroundColor Cyan
    }
    else {
        $versionTag = $Tag
        $env:SOLUTION_VERSION = $Tag
        Write-Host "Using custom tag: $versionTag" -ForegroundColor Cyan
    }

    # Build containers if requested
    if ($Build) {
        Write-Host "Building containers..." -ForegroundColor Cyan
        docker compose -f "$PSScriptRoot/../docker/docker-compose-ci.yml" build
        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed with exit code $LASTEXITCODE"
        }
        Write-Host "OK Build completed" -ForegroundColor Green
    }

    # Push containers
    Write-Host "Pushing containers to DockerHub..." -ForegroundColor Cyan

    # Set the registry prefix for compose
    $env:REGISTRY_PREFIX = $registryPrefix

    docker compose -f "$PSScriptRoot/../docker/docker-compose-ci.yml" push
    if ($LASTEXITCODE -ne 0) {
        throw "Docker push failed with exit code $LASTEXITCODE"
    }

    Write-Host "OK Successfully pushed containers to DockerHub" -ForegroundColor Green
    Write-Host "  - ${registryPrefix}yofi-v3-frontend:$versionTag" -ForegroundColor Green
    Write-Host "  - ${registryPrefix}yofi-v3-backend:$versionTag" -ForegroundColor Green
}
catch {
    Write-Error "Failed to push containers: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Remove-Item env:SOLUTION_VERSION -ErrorAction SilentlyContinue
    Remove-Item env:REGISTRY_PREFIX -ErrorAction SilentlyContinue
}
