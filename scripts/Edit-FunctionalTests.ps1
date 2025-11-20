<#
.SYNOPSIS
    Launches VS Code to edit functional tests with optional Playwright debugging.

.DESCRIPTION
    Opens the functional tests directory in VS Code with configurable Playwright
    debugging environment. Sets the PWDEBUG environment variable to enable or
    disable Playwright Inspector.

.PARAMETER PwDebug
    Enable Playwright debugging mode. When specified, sets PWDEBUG=1 to launch
    Playwright Inspector for step-by-step test debugging.

.EXAMPLE
    .\Edit-FunctionalTests.ps1 -PwDebug
    Opens VS Code with Playwright debugging enabled.

.EXAMPLE
    .\Edit-FunctionalTests.ps1
    Opens VS Code with normal test execution mode.
#>

[CmdletBinding()]
param(
    [Parameter(HelpMessage="Enable Playwright debugging mode")]
    [switch]
    $PwDebug
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

try {
    $Top = "$PSScriptRoot/.."
    $TestsPath = Join-Path $Top "tests" "Functional"
    
    # Validate that the tests directory exists
    if (-not (Test-Path $TestsPath)) {
        Write-Error "Functional tests directory not found: $TestsPath"
        exit 1
    }
    
    # Set Playwright debug environment variable
    $env:PWDEBUG = if ($PwDebug) { "1" } else { "0" }
    
    Write-Verbose "Opening $TestsPath in VS Code with PWDEBUG=$($env:PWDEBUG)"
    
    # Launch VS Code
    code $TestsPath
    
    exit 0
}
catch {
    Write-Error "Failed to launch VS Code: $_"
    exit 1
}