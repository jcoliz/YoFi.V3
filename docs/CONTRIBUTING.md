# Contributing to YoFi.V3

## Development Setup

1. **Prerequisites**
   - .NET 10.0 SDK
   - Node.js 20+ with pnpm
   - Visual Studio 2022 or VS Code with recommended extensions

2. **Clone and Setup**
   ```powershell
   git clone https://github.com/jcoliz/YoFi.V3.git
   cd YoFi.V3
   dotnet restore
   cd src/FrontEnd.Nuxt
   pnpm install
   ```

3. **Running Locally**
   ```powershell
   dotnet watch --project src/AppHost
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