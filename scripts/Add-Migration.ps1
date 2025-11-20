<#
.SYNOPSIS
Adds a new Entity Framework migration to the specified database provider.

.DESCRIPTION
This script creates a new Entity Framework Core migration for the YoFi.V3 application.
It builds the solution and then uses dotnet ef to add a migration to the specified
database provider (default: Sqlite).

.PARAMETER Name
The name of the migration to create. This is required and should describe the database
schema changes being made.

.PARAMETER Provider
The database provider to create the migration for. Default value is "Sqlite".

.EXAMPLE
.\Add-Migration.ps1 -Name "AddUserTable"
Creates a new migration named "AddUserTable" for the Sqlite provider.

.EXAMPLE
.\Add-Migration.ps1 -Name "AddIndexes" -Provider "Sqlite"
Creates a new migration named "AddIndexes" for the Sqlite provider.

.NOTES
The migration files will be created in the .\Migrations\ directory of the provider project.
See the Migration README at ../src/Data/Sqlite.MigrationHost/README.md for more information.

.LINK
https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
#>

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