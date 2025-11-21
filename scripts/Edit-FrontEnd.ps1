<#
.SYNOPSIS
    Launches VS Code to edit the front-end application.

.DESCRIPTION
    Opens the Nuxt front-end directory in VS Code for development and editing.

.EXAMPLE
    .\Edit-FrontEnd.ps1
    Opens VS Code with the front-end project.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

try {
    $Top = "$PSScriptRoot/.."
    $FrontEndPath = Join-Path $Top "src" "FrontEnd.Nuxt"

    # Validate that the front-end directory exists
    if (-not (Test-Path $FrontEndPath)) {
        Write-Error "Front-end directory not found: $FrontEndPath"
        exit 1
    }

    Write-Verbose "Opening $FrontEndPath in VS Code"

    # Launch VS Code
    code "$FrontEndPath"

    exit 0
}
catch {
    Write-Error "Failed to launch VS Code: $_"
    exit 1
}
