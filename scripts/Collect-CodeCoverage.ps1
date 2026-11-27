<#
.SYNOPSIS
Runs unit tests and collects Application and Entities code coverage metrics.

.DESCRIPTION
This script executes the Unit test project with code coverage collection focused on
Application and Entities assemblies. Functional and integration test projects are not
included.

.EXAMPLE
.\Collect-CodeCoverage.ps1
Runs unit tests and generates an Application and Entities code coverage report.

.NOTES
Requires ReportGenerator to be installed globally:
    dotnet tool install -g dotnet-reportgenerator-globaltool

The unit coverage report is generated in .\bin\coverage\ and opens automatically in your browser.

Coverage configuration is defined in tests\coverlet.runsettings.

.LINK
https://github.com/danielpalme/ReportGenerator
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Write-LowCoverageClasses {
    param(
        [string]$ReportPath
    )

    $html = Get-Content -LiteralPath $ReportPath -Raw

    $coverageInfoMatch = [regex]::Match(
        $html,
        '<coverage-info>(?<content>.*?)</coverage-info>',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    if (-not $coverageInfoMatch.Success) {
        Write-Warning "Could not find <coverage-info> in $ReportPath"
        return
    }

    $classRowPattern = '<tr>\s*<td><a[^>]*>(?<name>.*?)</a></td>\s*<td[^>]*>(?<covered>\d+)</td>\s*<td[^>]*>(?<uncovered>\d+)</td>\s*<td[^>]*>\d+</td>\s*<td[^>]*>\d+</td>\s*<td[^>]*>(?<percentage>[\d.]+)%</td>'
    $classRows = [regex]::Matches(
        $coverageInfoMatch.Groups['content'].Value,
        $classRowPattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    $lowCoverageClasses = foreach ($row in $classRows) {
        $uncovered = [int]$row.Groups['uncovered'].Value
        $percentage = [double]$row.Groups['percentage'].Value

        if ($uncovered -ge 10 -and $percentage -lt 90) {
            [PSCustomObject]@{
                Name = [System.Net.WebUtility]::HtmlDecode($row.Groups['name'].Value)
                Uncovered = $uncovered
                Percentage = $percentage
            }
        }
    }

    if ($lowCoverageClasses) {
        Write-Host "`nClasses below coverage standard (>=10 uncovered lines and <90% line coverage):" -ForegroundColor Yellow
        foreach ($class in $lowCoverageClasses) {
            Write-Host "  $($class.Name): $($class.Uncovered) uncovered lines, $($class.Percentage)% line coverage" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "`nNo classes are below the coverage standard" -ForegroundColor Green
    }
}

function Write-RiskHotspots {
    param(
        [string]$ReportPath
    )

    $html = Get-Content -LiteralPath $ReportPath -Raw

    $riskHotspotsMatch = [regex]::Match(
        $html,
        '<risk-hotspots>(?<content>.*?)</risk-hotspots>',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    if (-not $riskHotspotsMatch.Success) {
        Write-Warning "Could not find <risk-hotspots> in $ReportPath"
        return
    }

    $rowPattern = '<tr>(?<row>.*?)</tr>'
    $rows = [regex]::Matches(
        $riskHotspotsMatch.Groups['content'].Value,
        $rowPattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    $riskHotspots = foreach ($rowMatch in $rows) {
        $cells = [regex]::Matches(
            $rowMatch.Groups['row'].Value,
            '<td(?<attributes>[^>]*)>(?<value>.*?)</td>',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline
        )

        if ($cells.Count -eq 5) {
            $methodCell = $cells[2].Groups['value'].Value
            $titleMatch = [regex]::Match(
                $cells[2].Groups['attributes'].Value,
                'title="(?<title>[^"]*)"',
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
            )

            $classHtml = $cells[1].Groups['value'].Value
            $methodHtml = if ($titleMatch.Success) { $titleMatch.Groups['title'].Value } else { $methodCell }

            [PSCustomObject]@{
                Assembly = [System.Net.WebUtility]::HtmlDecode(([regex]::Replace($cells[0].Groups['value'].Value, '<.*?>', '')).Trim())
                Class = [System.Net.WebUtility]::HtmlDecode(([regex]::Replace($classHtml, '<.*?>', '')).Trim())
                Method = [System.Net.WebUtility]::HtmlDecode(([regex]::Replace($methodHtml, '<.*?>', '')).Trim())
                'Crap Score' = [System.Net.WebUtility]::HtmlDecode(([regex]::Replace($cells[3].Groups['value'].Value, '<.*?>', '')).Trim())
                'Cyclomatic complexity' = [System.Net.WebUtility]::HtmlDecode(([regex]::Replace($cells[4].Groups['value'].Value, '<.*?>', '')).Trim())
            }
        }
    }

    if ($riskHotspots) {
        Write-Host "`nRisk hotspots (all entries require remediation):" -ForegroundColor Yellow
        $riskHotspots | Format-Table -AutoSize
    }
    else {
        Write-Host "`nNo risk hotspots" -ForegroundColor Green
    }
}

function Run-TestsWithCoverage {
    param(
        [string]$TestProjectPath,
        [string]$TestName,
        [string]$SettingsPath
    )

    Write-Host "Running $TestName with code coverage..." -ForegroundColor Cyan
    Push-Location $TestProjectPath
    try {
        Remove-Item TestResults -Recurse -Force -ErrorAction SilentlyContinue
        dotnet test --no-build --collect:"XPlat Code Coverage" --settings:$SettingsPath
        if ($LASTEXITCODE -ne 0) {
            throw "$TestName execution failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

try {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $testsDir = "$repoRoot/tests"
    $unitTestPath = "$testsDir/Unit"
    $coverletSettingsPath = "$testsDir/coverlet.runsettings"
    $outputDir = "$repoRoot/bin/coverage"

    # Verify the unit test project exists
    if (-not (Test-Path $unitTestPath)) {
        throw "Unit test directory not found: $unitTestPath"
    }

    if (-not (Test-Path $coverletSettingsPath)) {
        throw "Coverlet settings file not found: $coverletSettingsPath"
    }

    Write-Host "Collecting unit code coverage for Application and Entities" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Cleaning up previous coverage results..." -ForegroundColor Cyan
    Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue

    # Clean and build the unit test project
    Write-Host "Cleaning solution..." -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        dotnet clean --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw "Clean failed with exit code $LASTEXITCODE"
        }

        Write-Host "Building solution..." -ForegroundColor Cyan
        dotnet build $unitTestPath --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    # Run unit tests with coverage (without rebuilding)
    Run-TestsWithCoverage -TestProjectPath $unitTestPath -TestName "unit tests" -SettingsPath $coverletSettingsPath

    # Generate a report only from unit-test coverage output
    Write-Host "`nGenerating unit code coverage report..." -ForegroundColor Cyan
    reportgenerator `
        -reports:"$unitTestPath/TestResults/*/coverage.cobertura.xml" `
        -targetdir:"$outputDir"
    if ($LASTEXITCODE -ne 0) {
        throw "Report generation failed with exit code $LASTEXITCODE"
    }

    Write-LowCoverageClasses -ReportPath "$outputDir/index.html"
    Write-RiskHotspots -ReportPath "$outputDir/index.html"

    Write-Host "`nOK Unit code coverage report generated successfully" -ForegroundColor Green
    Write-Host "`nCoverage report available at:" -ForegroundColor Cyan
    Write-Host "  $outputDir/index.html" -ForegroundColor White
    Write-Host "`nOpening report in browser..." -ForegroundColor Cyan
    Start-Process "$outputDir/index.html"
}
catch {
    Write-Error "Failed to collect code coverage: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
