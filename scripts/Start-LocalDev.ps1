<#
.SYNOPSIS
Starts the local development environment using .NET Aspire.

.DESCRIPTION
This script starts the local development environment by running the AppHost project
with dotnet watch. This enables hot reload during development and launches the
.NET Aspire dashboard for monitoring and managing the application services.

.EXAMPLE
.\Start-LocalDev.ps1
Starts the AppHost with hot reload enabled.

.NOTES
This script uses dotnet watch to enable automatic recompilation when files change.
The Aspire dashboard will open automatically and provide access to all services.

.LINK
https://learn.microsoft.com/en-us/dotnet/aspire/
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

try {
    $Top = "$PSScriptRoot/.."
    $ProjectPath = "$Top/src/AppHost"
    
    if (-not (Test-Path $ProjectPath)) {
        throw "AppHost project not found: $ProjectPath"
    }
    
    Write-Host "Starting local development environment..." -ForegroundColor Cyan
    Write-Host "Using dotnet watch for hot reload" -ForegroundColor Cyan
    
    dotnet watch --project $ProjectPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet watch failed with exit code $LASTEXITCODE"
    }
}
catch {
    Write-Error "Failed to start local development: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}