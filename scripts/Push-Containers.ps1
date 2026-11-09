<#
.SYNOPSIS
Builds and pushes the Docker CI containers to Docker Hub.

.DESCRIPTION
Ensures Docker is running and logged in, builds the frontend and backend using
docker-compose-ci.yml, and pushes both versioned and latest tags to Docker Hub.
Clears TESTPWORD while running and restores it afterward.

.PARAMETER DockerHubNamespace
Optional Docker Hub username or organization that owns the image repositories.
When omitted, REGISTRY_PREFIX from the environment or docker/.env is used.

.PARAMETER Version
Optional version tag to publish. When omitted, SOLUTION_VERSION must be provided
by the environment or Compose environment file; the script rejects the local fallback.

.PARAMETER PublishProject
Also publish the Compose project itself to Docker Hub as an OCI artifact named
<namespace>/yofi-v3, tagged with the version and latest. Image references are
pinned to digests so the project runs without REGISTRY_PREFIX or SOLUTION_VERSION.
Users only need to set the TESTPWORD environment variable, then run:
docker compose -f oci://<namespace>/yofi-v3:<tag> up

.PARAMETER SkipBuild
Push existing images without rebuilding. Use after testing the images in CI.

.PARAMETER SkipLogin
Use the existing Docker login, such as an Azure Pipelines service connection.

.PARAMETER Yes
Accept all Compose publishing confirmation prompts. Only use with reviewed inputs.

.EXAMPLE
.\Push-Containers.ps1

.EXAMPLE
.\Push-Containers.ps1 -DockerHubNamespace myorganization -Version 1.2.3

.EXAMPLE
.\Push-Containers.ps1 -PublishProject

.EXAMPLE
.\Push-Containers.ps1 -SkipBuild -SkipLogin -PublishProject -Yes
#>

[CmdletBinding()]
param(
    [string]$DockerHubNamespace,
    [string]$Version,
    [switch]$PublishProject,
    [switch]$SkipBuild,
    [switch]$SkipLogin,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$projectName = "yofi-v3"

Import-Module (Join-Path $PSScriptRoot "DockerUtilities.psm1") -Force

$originalRegistryPrefix = $env:REGISTRY_PREFIX
$originalSolutionVersion = $env:SOLUTION_VERSION
$originalTestPassword = $env:TESTPWORD

try {
    $env:TESTPWORD = ''

    if (-not (Test-DockerRunning)) {
        throw "Docker is not running. Please start Docker Desktop and try again."
    }

    if (-not $SkipLogin) {
        Write-Host "Checking Docker Hub login..." -ForegroundColor Cyan
        docker login
        if ($LASTEXITCODE -ne 0) {
            throw "Docker Hub login failed with exit code $LASTEXITCODE"
        }
    }

    if ($DockerHubNamespace) {
        if ($DockerHubNamespace -notmatch '^[a-z0-9]+(?:[._-][a-z0-9]+)*$') {
            throw "Docker Hub namespace '$DockerHubNamespace' is invalid. Use a lowercase username or organization name."
        }
        $env:REGISTRY_PREFIX = "$DockerHubNamespace/"
    }

    if ($Version) {
        $env:SOLUTION_VERSION = $Version
    }

    $composeFile = Join-Path $PSScriptRoot "../docker/docker-compose-ci.yml"
    $services = @("frontend", "backend")

    $images = docker compose -f $composeFile config --images @services
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose config failed with exit code $LASTEXITCODE"
    }
    if ($images | Where-Object { $_ -match ":local$" }) {
        throw "SOLUTION_VERSION is not set. Set it in the environment or docker/.env, or pass -Version."
    }
    if ($images | Where-Object { $_ -match "^$projectName-" }) {
        throw "No registry namespace is set. Set REGISTRY_PREFIX in docker/.env (for example 'myorganization/') or pass -DockerHubNamespace."
    }

    if (-not $SkipBuild) {
        if ($Version) {
            Write-Host "Building Docker images with version tag '$Version'..." -ForegroundColor Cyan
        }
        elseif (Test-Path env:SOLUTION_VERSION) {
            Write-Host "Building Docker images with SOLUTION_VERSION '$env:SOLUTION_VERSION'..." -ForegroundColor Cyan
        }
        else {
            Write-Host "Building Docker images with the version tag resolved by Docker Compose..." -ForegroundColor Cyan
        }
        docker compose -f $composeFile build @services
        if ($LASTEXITCODE -ne 0) {
            throw "Docker Compose build failed with exit code $LASTEXITCODE"
        }
    }

    Write-Host "Pushing Docker images to Docker Hub..." -ForegroundColor Cyan
    docker compose -f $composeFile push @services
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose push failed with exit code $LASTEXITCODE"
    }

    Write-Host "Pushed Docker images to Docker Hub with versioned and latest tags." -ForegroundColor Green

    if ($PublishProject) {
        # Derive namespace and version tag from the resolved backend image
        $backendImage = $images | Where-Object { $_ -match "/$projectName-backend:" } | Select-Object -First 1
        if ($backendImage -notmatch "^(?<repo>.+)/$projectName-backend:(?<tag>[^:/]+)$") {
            throw "Could not determine project repository from backend image '$backendImage'."
        }
        $projectRepository = "$($Matches.repo)/$projectName"
        $publishOptions = @("--resolve-image-digests", "--with-env")
        if ($Yes) {
            $publishOptions += "--yes"
        }

        foreach ($tag in @($Matches.tag, "latest") | Select-Object -Unique) {
            Write-Host "Publishing Compose project to $projectRepository`:$tag..." -ForegroundColor Cyan
            # --with-env only uploads files referenced by services' env_file (none here), not docker/.env.
            docker compose -f $composeFile publish @publishOptions "$projectRepository`:$tag"
            if ($LASTEXITCODE -ne 0) {
                throw "Docker Compose publish failed with exit code $LASTEXITCODE"
            }
        }

        Write-Host "Published Compose project to $projectRepository." -ForegroundColor Green
    }
}
catch {
    Write-Error "Failed to push containers: $_"
    exit 1
}
finally {
    $env:REGISTRY_PREFIX = $originalRegistryPrefix
    $env:SOLUTION_VERSION = $originalSolutionVersion
    $env:TESTPWORD = $originalTestPassword
}
