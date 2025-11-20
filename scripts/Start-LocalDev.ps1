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

$Top = "$PSScriptRoot/.."
dotnet watch --project "$Top/src/AppHost"