<#
.SYNOPSIS
    Build and start containers, run functional tests, and stop containers.

.DESCRIPTION
    Uses Start-Container.ps1 -Build -NoBrowser, Run-Tests.ps1 -Functional, and
    Stop-Container.ps1. Always attempts container cleanup and restores the
    original working directory, including when startup or tests fail.
    Test settings come from the functional test project's default runsettings.
    This script does not set a solution version.

.PARAMETER KeepAlive
    Skip the Stop-Container.ps1 step, leaving the containers running.

.EXAMPLE
    .\Run-FunctionalTestsVsContainer.ps1

.EXAMPLE
    .\Run-FunctionalTestsVsContainer.ps1 -KeepAlive
#>

[CmdletBinding()]
param(
    [switch]$KeepAlive
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

try {
    & (Join-Path $PSScriptRoot "Start-Container.ps1") -Build -NoBrowser
    if ($LASTEXITCODE -ne 0) {
        throw "Start-Container.ps1 failed with exit code $LASTEXITCODE"
    }

    & (Join-Path $PSScriptRoot "Run-Tests.ps1") -Functional
    if ($LASTEXITCODE -ne 0) {
        throw "Run-Tests.ps1 failed with exit code $LASTEXITCODE"
    }
}
finally {
    try {
        if (-not $KeepAlive) {
            & (Join-Path $PSScriptRoot "Stop-Container.ps1")
            if ($LASTEXITCODE -ne 0) {
                throw "Stop-Container.ps1 failed with exit code $LASTEXITCODE"
            }
        }
        else {
            Write-Warning "KeepAlive was specified. Containers are still running."
            Write-Host "To stop them, run: .\scripts\Stop-Container.ps1" -ForegroundColor Yellow
        }
    }
    finally {
        Pop-Location
    }
}
