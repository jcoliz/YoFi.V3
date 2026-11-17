<#
.SYNOPSIS
Rebuilds and starts the Docker CI containers without opening a browser.

.EXAMPLE
.\Refresh-Container.ps1
#>

& (Join-Path $PSScriptRoot "Start-Container.ps1") -Build -NoBrowser
if (-not $?) {
    exit 1
}
