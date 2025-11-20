# Contributing to YoFi.V3

## Development Setup

### Prerequisites

- .NET 10.0 SDK
- Node.js 24+ with pnpm
- Visual Studio 2022 or VS Code with recommended extensions
- Docker Desktop (optional, for container workflows)

### Quick Start (Recommended)

For first-time setup, use the automated development setup script:

```powershell
git clone --recurse-submodules https://github.com/jcoliz/YoFi.V3.git
cd YoFi.V3
.\scripts\Setup-Development.ps1
```

This script will:
- Verify all required tools are installed (.NET 10 SDK, Node.js 24+, pnpm, Docker)
- Restore .NET dependencies
- Install frontend npm packages
- Build the solution
- Run tests to verify your setup is working correctly

After setup completes, start the development environment:

```powershell
.\scripts\Start-LocalDev.ps1
```

## Architecture

See [Architecture Decision Records](adr/README.md) for key design decisions.

This project follows Clean Architecture with clear separation:
- **Entities** - Pure data models (no logic)
- **Application** - Business logic organized as Features
- **Controllers** - Thin HTTP layer (pass-through to Application)
- **BackEnd** - Host project for the API
- **FrontEnd.Nuxt** - Vue/Nuxt SPA

## Coding Standards

### C# (.NET)

- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Prefer modern C# features (records, pattern matching, etc.)
- Keep controllers thin - business logic goes in Application Features

### TypeScript/Vue

- Use TypeScript for all new code
- Follow Vue 3 Composition API patterns
- Use Nuxt auto-imports (avoid manual imports)
- Keep components small and focused

## Testing

- Write unit tests for all Application Features
- Tests live in `tests/Unit/`
- Run tests: `dotnet test`
- Aim for high coverage of business logic

## Pull Request Process

1. Create a feature branch from `main`
2. Make your changes with clear, atomic commits
3. Ensure all tests pass
4. Update documentation if needed
5. Create PR with clear description
6. Wait for CI checks to pass

## Code Review Guidelines

- Be respectful and constructive
- Focus on code quality, not personal preferences
- Explain the "why" behind suggestions
- Approve when satisfied, or request changes with specific feedback