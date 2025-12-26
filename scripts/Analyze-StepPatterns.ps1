<#
.SYNOPSIS
Analyzes step definition patterns for duplicates.

.DESCRIPTION
This script extracts all step attribute patterns from functional test step definition
files and checks for duplicate patterns that could cause conflicts.

.EXAMPLE
.\Analyze-StepPatterns.ps1
Analyzes all step patterns and reports duplicates.

.NOTES
Used to verify step attribute conversion has no pattern conflicts.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

try {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $stepsPath = Join-Path $repoRoot "tests\Functional\Steps"

    Write-Host "Analyzing step patterns in: $stepsPath" -ForegroundColor Cyan

    # Find all .cs files in Steps directory
    $stepFiles = Get-ChildItem -Path $stepsPath -Filter "*.cs" -Recurse

    # Extract all step patterns
    $patterns = @()
    $regex = '\[(Given|When|Then)\("([^"]+)"\)\]'

    foreach ($file in $stepFiles) {
        $content = Get-Content $file.FullName -Raw
        $matches = [regex]::Matches($content, $regex)

        foreach ($match in $matches) {
            $keyword = $match.Groups[1].Value
            $pattern = $match.Groups[2].Value
            $relativePath = $file.FullName.Substring($repoRoot.Length + 1)

            $patterns += [PSCustomObject]@{
                Keyword = $keyword
                Pattern = $pattern
                File = $relativePath
                FullPattern = "$keyword(`"$pattern`")"
            }
        }
    }

    Write-Host "Found $($patterns.Count) step patterns across $($stepFiles.Count) files" -ForegroundColor Cyan
    Write-Host ""

    # Group by full pattern (keyword + pattern text)
    $grouped = $patterns | Group-Object -Property FullPattern | Where-Object { $_.Count -gt 1 }

    if ($grouped.Count -eq 0) {
        Write-Host "OK No duplicate patterns found - all patterns are unique!" -ForegroundColor Green
    }
    else {
        Write-Host "WARNING Found $($grouped.Count) duplicate patterns:" -ForegroundColor Yellow
        Write-Host ""

        foreach ($group in $grouped) {
            Write-Host "Duplicate: $($group.Name)" -ForegroundColor Yellow
            foreach ($item in $group.Group) {
                Write-Host "  - $($item.File)" -ForegroundColor Yellow
            }
            Write-Host ""
        }

        throw "Found $($grouped.Count) duplicate step patterns"
    }

    # Show pattern statistics
    Write-Host ""
    Write-Host "Pattern Statistics:" -ForegroundColor Cyan
    $byKeyword = $patterns | Group-Object -Property Keyword | Sort-Object Name
    foreach ($group in $byKeyword) {
        Write-Host "  $($group.Name): $($group.Count) patterns" -ForegroundColor Cyan
    }
}
catch {
    Write-Error "Failed to analyze step patterns: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
