# Commit Conventions

This project follows a structured commit message format to maintain a clear and readable git history. Following these conventions helps with automated changelog generation, easier code reviews, and better collaboration.

## Format

All commit messages should follow this structure:

```
<type>(<scope>): <subject>

[optional body]

[optional footer]
```

## Types

Use one of the following types to categorize your commit:

- **feat**: A new feature for the user
- **fix**: A bug fix in production code
- **docs**: Documentation changes only
- **style**: Code style changes (formatting, missing semicolons, etc.) with no logic changes
- **refactor**: Code changes that neither fix a bug nor add a feature
- **perf**: Performance improvements
- **test**: Adding, updating, fixing, or refactoring tests (use this for all test-related changes)
- **build**: Changes to build system, dependencies, or project configuration (e.g., NuGet packages, npm dependencies, .csproj files)
- **ci**: Changes to CI/CD configuration files and scripts
- **revert**: Reverts a previous commit

**Note**: Use `test` type for all test changes, including new tests, fixing broken tests, and refactoring test code. The scope (unit/functional/integration) indicates which type of test.

## Scopes

Use these project-specific scopes to identify the area of change:

### Architecture Layer Scopes

Use when changes are isolated to a single layer:

- **frontend**: FrontEnd.Nuxt (Vue/Nuxt SPA)
- **backend**: BackEnd API service
- **app**: Application layer (business logic Features)
- **controllers**: Controller layer
- **entities**: Entity/model definitions
- **data**: Data layer (EF Core, migrations, database context)
- **infra**: Infrastructure as code (Bicep, Azure resources)
- **unit**: Unit tests
- **functional**: Functional/end-to-end tests
- **integration**: Integration tests
- **docs**: Documentation files
- **scripts**: PowerShell automation scripts
- **aspire**: AppHost/ServiceDefaults (.NET Aspire orchestration)
- **ci**: CI/CD pipeline build changes
- **deps**: Dependency build changes

### Feature-Based Scopes

Use when changes span multiple layers for a single feature:

**Generic Feature Scopes** (for general work):
- **auth**: Authentication and authorization
- **tenancy**: Managing tenants, user role assignments, invitations
- **transactions**: Transaction management features (general)
- **reports**: Reporting functionality (general)
- **budgets**: Budget tracking features (general)
- **payees**: Payee management
- **categories**: Category management
- **imports**: Data import functionality (general)

**PRD-Specific Feature Scopes** (use when implementing a specific PRD):
- **transaction-record**: Transaction CRUD operations (PRD-TRANSACTION-RECORD.md)
- **transaction-splits**: Transaction split functionality (PRD-TRANSACTION-SPLITS.md)
- **transaction-filtering**: Transaction filtering and search (PRD-TRANSACTION-FILTERING.md)
- **transaction-attachments**: Transaction attachments (PRD-TRANSACTION-ATTACHMENTS.md)
- **payee-rules**: Payee rules and categorization (PRD-PAYEE-RULES.md)
- **bank-import**: Bank statement import (PRD-BANK-IMPORT.md)
- **tenant-data-admin**: Tenant data administration (PRD-TENANT-DATA-ADMIN.md)
- **export-api**: Data export API (PRD-EXPORT-API.md)

**Note**: Feature scopes are preferred when implementing or modifying functionality that cuts across multiple architectural layers (e.g., entities, app layer, controllers, frontend). **When implementing a specific PRD, always use the PRD-specific feature slug** (e.g., `feat(transaction-record): add create endpoint`) rather than the generic scope (e.g., `feat(transactions): ...`). This makes commit history much more meaningful and easier to scan for specific feature work.

### Choosing the Right Scope

- **Use layer scopes** for isolated changes: `fix(data): correct migration rollback logic`
- **Use feature scopes** for cross-cutting work: `feat(auth): implement multi-factor authentication`
- **Scope is optional** but strongly recommended for clarity

Feel free to create new feature-based scopes as you implement major features in the application.

## Subject Line

The subject line should:

- Use imperative mood ("add" not "added" or "adds")
- Not capitalize the first letter (makes grep easier)
- Not end with a period
- Be limited to 72 characters
- Be concise but descriptive

### Examples

✅ **Good**:
```
feat(frontend): add weather forecast display component
fix(backend): resolve null reference in weather controller
docs(contributing): move prerequisites to separate section
feat(transaction-record): add create transaction endpoint
feat(transaction-splits): implement split allocation logic
refactor(payee-rules): simplify rule matching across all layers
```

✅ **Also Good** (generic scopes for non-PRD work):
```
feat(transactions): add transaction export utility
fix(budgets): correct budget calculation edge case
```

❌ **Bad**:
```
Added new feature.
Fixed bug
Update files
```

## Body (Optional)

Include a body when the commit needs additional explanation:

- Separate from subject with a blank line
- Wrap lines at 72 characters
- Explain **what** and **why**, not **how**
- Use bullet points for multiple items

### Example

```
refactor(data): simplify database context configuration

- Extract connection string logic to separate method
- Remove unused DbSet properties
- Add XML documentation for public members

This improves testability and makes the context easier to maintain.
```

## Footer (Optional)

Use the footer for:

### Breaking Changes

Prefix with `BREAKING CHANGE:` followed by a description:

```
feat(app)!: redesign weather feature interface

BREAKING CHANGE: WeatherFeature.GetForecast() now returns Task<Result<T>>
instead of Task<T>. Update all callers to handle the Result pattern.
```

Note: The `!` after the type/scope is a visual indicator of a breaking change.

### Issue References

Reference issues that this commit addresses:

```
fix(backend): correct validation logic for weather data

Fixes #123
Closes #456
```

### Co-authors

Credit co-authors when pair programming:

```
feat(frontend): implement user profile page

Co-authored-by: Jane Doe <jane@example.com>
```

## Complete Examples

### Simple Feature

```
feat(frontend): add weather forecast page
```

### Bug Fix with Details

```
fix(data): prevent duplicate migration applications

Check for existing migrations before applying to avoid
database errors in production deployments.

Fixes #78
```

### Test Changes

```
test(unit): add validation tests for transaction model
test(functional): fix flaky authentication test
test(integration): refactor database setup for better performance
```

### Refactoring with Multiple Changes

```
refactor(app): restructure weather feature organization

- Move validation logic to separate validator class
- Extract data transformation to mapper
- Improve error handling with Result pattern
- Add comprehensive unit tests

This improves code maintainability and testability while
maintaining the same external API.
```

### Documentation Update

```
docs(readme): update installation instructions for .NET 10
```

### Infrastructure Change

```
build(ci): add automated deployment workflow

Implements continuous deployment to Azure on main branch merges.
Includes environment-specific configurations and approval gates.
```

## Best Practices

1. **Make atomic commits**: Each commit should represent a single logical change
2. **Commit early and often**: Don't wait until you have a massive changeset
3. **Write meaningful messages**: Future you (and your team) will thank you
4. **Use the body**: Don't be afraid to explain the context and reasoning
5. **Reference issues**: Link commits to issue tracking for better traceability
6. **Review before pushing**: Use `git log` to review your commit messages

## Tools

In the future, we will consider using these tools to enforce commit conventions:

- **[Commitizen](https://github.com/commitizen/cz-cli)**: Interactive commit message builder
- **[commitlint](https://commitlint.js.org/)**: Lint commit messages
- **[Husky](https://typicode.github.io/husky/)**: Git hooks to enforce conventions

## Resources

- [Conventional Commits Specification](https://www.conventionalcommits.org/)
- [Angular Commit Guidelines](https://github.com/angular/angular/blob/main/CONTRIBUTING.md#commit)
- [How to Write a Git Commit Message](https://chris.beams.io/posts/git-commit/)
