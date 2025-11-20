<#
.SYNOPSIS
Creates a SQL script to migrate the database schema to the latest migration.

.DESCRIPTION
This script generates an idempotent SQL migration script using Entity Framework Core.
The generated script can be used to update a database schema to match the latest migration.

WARNING: This is not supported for Sqlite databases.
See https://go.microsoft.com/fwlink/?LinkId=723262 for more information.

.PARAMETER Provider
The database provider to generate the migration script for (e.g., "SqlServer", "PostgreSQL").
This parameter is optional.

.EXAMPLE
.\Create-MigrationScript.ps1 -Provider "SqlServer"
Generates a SQL migration script for SQL Server in the out/ directory.

.NOTES
The generated script will be idempotent (-i flag) and saved to out/{Provider}-Migration.sql.
This script does not work with Sqlite databases.

.LINK
https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#sql-scripts
#>

param(
    [Parameter()]
    [string]
    $Provider
)

$ErrorActionPreference = "Stop"

$Top = "$PSScriptRoot/.."

Push-Location $Top
dotnet build
dotnet ef migrations script --project "./src/Data/$Provider/" --startup-project "./src/Data/$Provider.MigrationHost/" --context ApplicationDbContext -i -o "out/$Provider-Migration.sql"
Pop-Location