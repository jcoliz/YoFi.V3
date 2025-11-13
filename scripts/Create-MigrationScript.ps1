#
# Create a script to migrate the database schema to the latest migration.
#
# WARNING: Not supported for Sqlite. See https://go.microsoft.com/fwlink/?LinkId=723262
#

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