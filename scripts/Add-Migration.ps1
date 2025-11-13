#
# Add a new migration to the specified database provider.
#

param(
    [Parameter(Mandatory=$true)]
    [string]
    $Name,
    [Parameter()]
    [string]
    $Provider = "Sqlite"
)

$ErrorActionPreference = "Stop"

$Top = "$PSScriptRoot/.."

Push-Location $Top
dotnet build
dotnet ef migrations add $Name -o .\Migrations\ -n "YoFi.V3.Data.$Provider.Migrations" --project ".\src\Data\$Provider\" --startup-project ".\src\Data\$Provider.MigrationHost\" --context ApplicationDbContext
Pop-Location