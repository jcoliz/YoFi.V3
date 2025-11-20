<#
.SYNOPSIS
    Setup development environment for new developers

.DESCRIPTION
    Prepares a freshly cloned YoFi.V3 repository for development by:
    - Verifying required tools are installed (.NET SDK, Node.js, pnpm)
    - Restoring .NET dependencies
    - Installing frontend npm packages
    - Building the solution
    - Running tests to verify setup

.EXAMPLE
    .\Setup-Development.ps1
    
    Runs the complete development environment setup process.

.NOTES
    Run this script after cloning the repository for the first time.
    Requires: .NET 10 SDK, Node.js 20+, npm (for pnpm installation)
#>

[CmdletBinding()]
param()

# Ensure we're in the repository root
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

try {
    Write-Host "Setting up YoFi.V3 development environment..." -ForegroundColor Cyan

    # Check prerequisites
    Write-Host "`nChecking prerequisites..." -ForegroundColor Cyan

    # Check .NET SDK
    $requiredDotNetVersion = [System.Version]"10.0.100"
    try {
        $dotnetVersionString = dotnet --version
        $dotnetVersion = [System.Version]$dotnetVersionString
        if ($dotnetVersion -lt $requiredDotNetVersion) {
            Write-Host "WARNING .NET SDK version $dotnetVersionString is older than required $requiredDotNetVersion" -ForegroundColor Yellow
            Write-Host "        Download from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
        }
        Write-Host "OK .NET SDK: $dotnetVersionString" -ForegroundColor Green
    } catch {
        Write-Host "ERROR .NET SDK not found. Please install .NET 10 SDK" -ForegroundColor Red
        Write-Host "      Download from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
        exit 1
    }

    # Check Node.js
    $requiredNodeMajorVersion = 24
    try {
        $nodeVersion = node --version
        $nodeMajorVersion = [int]($nodeVersion -replace 'v(\d+)\..*', '$1')
        if ($nodeMajorVersion -lt $requiredNodeMajorVersion) {
            Write-Host "WARNING Node.js version $nodeVersion is older than required v$requiredNodeMajorVersion" -ForegroundColor Yellow
            Write-Host "        Download from: https://nodejs.org/" -ForegroundColor Yellow
        }
        Write-Host "OK Node.js: $nodeVersion" -ForegroundColor Green
    } catch {
        Write-Host "ERROR Node.js not found. Please install Node.js 20+" -ForegroundColor Red
        Write-Host "      Download from: https://nodejs.org/" -ForegroundColor Yellow
        exit 1
    }

    # Check pnpm
    try {
        $pnpmVersion = pnpm --version
        Write-Host "OK pnpm: $pnpmVersion" -ForegroundColor Green
    } catch {
        Write-Host "WARNING pnpm not found. Installing..." -ForegroundColor Yellow
        npm install -g pnpm
    }

    # Check Docker (optional but recommended)
    try {
        docker --version | Out-Null
        Write-Host "OK Docker: $(docker --version)" -ForegroundColor Green
    } catch {
        Write-Host "WARNING Docker not found or not running" -ForegroundColor Yellow
        Write-Host "        Docker is recommended but not required for local development" -ForegroundColor Yellow
        Write-Host "        Download from: https://www.docker.com/products/docker-desktop" -ForegroundColor Yellow
    }

    # Restore .NET dependencies
    Write-Host "`nRestoring .NET dependencies..." -ForegroundColor Cyan
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR Failed to restore .NET dependencies" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK .NET dependencies restored" -ForegroundColor Green

    # Install frontend dependencies
    Write-Host "`nInstalling frontend dependencies..." -ForegroundColor Cyan
    Push-Location src/FrontEnd.Nuxt
    try {
        pnpm install
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR Failed to install frontend dependencies" -ForegroundColor Red
            exit 1
        }
        Write-Host "OK Frontend dependencies installed" -ForegroundColor Green
    } finally {
        Pop-Location
    }

    # Build solution to verify everything works
    Write-Host "`nBuilding solution..." -ForegroundColor Cyan
    dotnet build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK Solution built successfully" -ForegroundColor Green

    # Run tests to verify setup
    Write-Host "`nRunning tests..." -ForegroundColor Cyan
    & "$PSScriptRoot\Run-Tests.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING Some tests failed. Review test output above." -ForegroundColor Yellow
    }

    # Success message
    Write-Host "`nDevelopment environment setup complete!" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "  1. Run the application:"
    Write-Host "     dotnet watch --project src/AppHost"
    Write-Host "`n  2. Or run in containers:"
    Write-Host "     ./scripts/Build-Container.ps1"
    Write-Host "     ./scripts/Start-Container.ps1"
    Write-Host "`n  3. See docs/CONTRIBUTING.md for more information"

} finally {
    Pop-Location
}