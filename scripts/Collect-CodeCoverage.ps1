<#
.SYNOPSIS
Runs tests and collects code coverage metrics.

.DESCRIPTION
This script executes tests with code coverage collection and generates an HTML report.
It uses the XPlat Code Coverage collector and ReportGenerator to create a detailed
coverage report that opens automatically in your browser.

.PARAMETER Tests
The test category to run. Default value is "Unit". Other possible values include "Functional".

.EXAMPLE
.\Collect-CodeCoverage.ps1
Runs Unit tests and generates a code coverage report.

.EXAMPLE
.\Collect-CodeCoverage.ps1 -Tests "Functional"
Runs Functional tests and generates a code coverage report.

.NOTES
Requires ReportGenerator to be installed globally:
    dotnet tool install -g dotnet-reportgenerator-globaltool

The coverage report will be generated in .\bin\result\index.html and opened automatically.

.LINK
https://github.com/danielpalme/ReportGenerator
#>

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