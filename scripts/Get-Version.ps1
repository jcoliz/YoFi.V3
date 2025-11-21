<#
.SYNOPSIS
Gets the current version string for the solution build.

.DESCRIPTION
This script generates a version string used by the build process to inject into
assembly resources. It checks for the SOLUTION_VERSION environment variable first,
and if not found, generates a version from the git commit hash, username, and timestamp.

.PARAMETER Stable
When specified, uses a less frequently changing time format (year-week) instead of
the default month-day-hour-minute format.

.EXAMPLE
.\Get-Version.ps1
Returns a version string like "a1b2c3d-username-11201630" based on git and user info.

.EXAMPLE
.\Get-Version.ps1 -Stable
Returns a version string like "a1b2c3d-username-25W47" using year-week format.

.EXAMPLE
$env:SOLUTION_VERSION = "1.0.0"
.\Get-Version.ps1
Returns "1.0.0" from the environment variable.

.OUTPUTS
System.String
Returns a version string to be used in the build process.

.NOTES
The generated version format when SOLUTION_VERSION is not set:
- Default: {GitCommitHash}-{Username}-{MonthDayHourMinute}
- Stable:  {GitCommitHash}-{Username}-{YearWeek}

For example:
- Default: a1b2c3d-jsmith-11201630
- Stable:  a1b2c3d-jsmith-25W47

.LINK
https://git-scm.com/docs/git-describe
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Stable
)

$ErrorActionPreference = "Stop"

try {
    if (Test-Path env:SOLUTION_VERSION) {
        $Version = "$env:SOLUTION_VERSION"
        Write-Verbose "Using SOLUTION_VERSION from environment: $Version"
    }
    else {
        $User = $env:USERNAME
        if (-not $User) {
            throw "USERNAME environment variable not set"
        }

        $Commit = git describe --always 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to get git commit information. Is this a git repository?"
        }

        if ($Stable) {
            $Time = Get-Date -UFormat "%yW%V"
            Write-Verbose "Using stable time format: $Time"
        }
        else {
            $Time = Get-Date -Format "MMddHHmm"
            Write-Verbose "Using default time format: $Time"
        }

        $Version = "$Commit-$User-$Time"
        Write-Verbose "Generated version: $Version"
    }

    Write-Output $Version
}
catch {
    Write-Error "Failed to get version: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
