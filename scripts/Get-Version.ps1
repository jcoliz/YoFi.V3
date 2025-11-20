<#
.SYNOPSIS
Gets the current version string for the solution build.

.DESCRIPTION
This script generates a version string used by the build process to inject into
assembly resources. It checks for the SOLUTION_VERSION environment variable first,
and if not found, generates a version from the git commit hash, username, and timestamp.

.EXAMPLE
.\Get-Version.ps1
Returns a version string like "a1b2c3d-username-11201630" based on git and user info.

.EXAMPLE
$env:SOLUTION_VERSION = "1.0.0"
.\Get-Version.ps1
Returns "1.0.0" from the environment variable.

.OUTPUTS
System.String
Returns a version string to be used in the build process.

.NOTES
The generated version format when SOLUTION_VERSION is not set:
{GitCommitHash}-{Username}-{MonthDayHourMinute}

For example: a1b2c3d-jsmith-11201630

.LINK
https://git-scm.com/docs/git-describe
#>

if (Test-Path env:SOLUTION_VERSION) 
{
    $Version = "$env:SOLUTION_VERSION"
}
else 
{   
    $User = $env:USERNAME
    $Commit = git describe --always
    $Time = $(Get-Date -Format "MMddHHmm")

    $Version = "$Commit-$User-$Time"
}

Write-Output $Version
