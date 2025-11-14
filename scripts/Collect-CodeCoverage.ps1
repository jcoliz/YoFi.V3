#
# Run tests and collect code coverage
#
# Requires: [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
#
# To install: dotnet tool install -g dotnet-reportgenerator-globaltool
#

param(
    [Parameter()]
    [string]
    $Tests="Unit"
)

Push-Location "$PSScriptRoot/../tests/$Tests"
$ErrorActionPreference = "Ignore"
del TestResults -Recurse
del bin\results -Recurse
$ErrorActionPreference = "Stop"
dotnet test --collect:"XPlat Code Coverage" --settings:coverlet.runsettings
reportgenerator -reports:.\TestResults\*\coverage.cobertura.xml -targetdir:.\bin\result
start .\bin\result\index.html
Pop-Location