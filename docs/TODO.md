# YoFi.V3 Remaining Work

**Version**: 2.0
**Date**: December 2025
**Status**: Phase 1 Complete - Multi-tenant foundation production-ready

---

## Executive Summary

### What's Been Accomplished

YoFi.V3 has successfully completed **Phase 1** of the production roadmap. The application now has a **production-ready multi-tenant foundation** with:

- ✅ **Authentication & Authorization** - ASP.NET Core Identity + NuxtIdentity with JWT tokens
- ✅ **Multi-Tenancy Infrastructure** - Complete tenant isolation with role-based access control
- ✅ **Error Handling Framework** - Global exception handlers with RFC 7807 ProblemDetails
- ✅ **Transaction Feature** - Full CRUD with tenant isolation and comprehensive tests
- ✅ **State Management** - Pinia stores implemented in frontend
- ✅ **Integration Testing** - Comprehensive test coverage for tenancy and transactions

See [`TENANCY.md`](TENANCY.md) for complete documentation of the multi-tenancy implementation.

### Current State

**Production-Ready Components:**
- Multi-tenant authentication and authorization
- Tenant management API (create, read, update, delete)
- Transaction management with tenant isolation
- Exception handling with proper HTTP responses
- Integration tests validating security boundaries

**Remaining Work Focus:**
- Input validation framework
- Enhanced logging and observability
- API design improvements (versioning, pagination)
- Performance optimizations (caching, indexing)
- Security hardening
- Production infrastructure
- Frontend feature completion

**Estimated Timeline**: 6-10 weeks to production-ready state

---

## High Priority 🔴

### 1. Input Validation Framework

**Status**: Basic validation exists, needs FluentValidation integration

**Current State:**
- Manual validation in [`TransactionsFeature.ValidateTransactionEditDto()`](../src/Application/Features/TransactionsFeature.cs:159-197)
- Attribute-based validation on DTOs ([`DateRangeAttribute`](../src/Application/Validation/DateRangeAttribute.cs), [`NotWhiteSpaceAttribute`](../src/Application/Validation/NotWhiteSpaceAttribute.cs))
- No standardized validation framework

**Implement:**
- Add **FluentValidation** NuGet package
- Create validators for all DTOs:
  - `TransactionEditDtoValidator`
  - `TenantEditDtoValidator`
  - Future DTO validators
- Register validators in dependency injection
- Add validation middleware or controller filters
- Implement consistent validation error responses

**Benefits:**
- Declarative, reusable validation rules
- Better separation of concerns
- Improved error messages
- Easier testing of validation logic

**Example:**
```csharp
public class TransactionEditDtoValidator : AbstractValidator<TransactionEditDto>
{
    public TransactionEditDtoValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .Must(BeWithinValidRange)
            .WithMessage("Date must be between 1900-01-01 and 2099-12-31");

        RuleFor(x => x.Amount)
            .NotEqual(0)
            .WithMessage("Amount cannot be zero");

        RuleFor(x => x.Payee)
            .NotEmpty()
            .MaximumLength(200)
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Payee is required");
    }
}
```

### 2. User Role Management API

**Status**: Repository methods exist, no API endpoints

**Current State:**
- [`ITenantRepository`](../src/Entities/Tenancy/ITenantRepository.cs) has role management methods
- No controllers or features for managing user roles
- No way to invite users to tenants via API

**Implement:**
- Create `UserRoleFeature` for business logic
- Add endpoints to [`TenantController`](../src/Controllers/Tenancy/TenantController.cs):
  - `POST /api/tenant/{tenantKey}/users` - Invite user (requires user lookup)
  - `PUT /api/tenant/{tenantKey}/users/{userId}/role` - Change role
  - `DELETE /api/tenant/{tenantKey}/users/{userId}` - Remove user
  - `GET /api/tenant/{tenantKey}/users` - List tenant users
- Implement authorization (only Owners can manage roles)
- Add validation:
  - Cannot remove last Owner
  - Validate role transitions
- Create integration tests

**Note:** Requires user invitation system or user lookup mechanism.

### 3. API Design Enhancements

**Current Issues:**
- No API versioning
- No pagination support for list endpoints
- No filtering/sorting capabilities
- Hardcoded limits (e.g., date ranges)

**Implement API Versioning:**
```csharp
// Install: Asp.Versioning.Mvc
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenant/{tenantKey:guid}/[controller]")]
public class TransactionsController : ControllerBase
```

**Add Pagination Support:**
```csharp
public record PagedResult<T>
{
    public ICollection<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public async Task<PagedResult<TransactionResultDto>> GetTransactions(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50,
    [FromQuery] DateOnly? fromDate = null,
    [FromQuery] DateOnly? toDate = null)
```

**Add Filtering and Sorting:**
- Query parameters for common filters
- Sortable columns with direction (asc/desc)
- Consider OData or GraphQL for advanced scenarios

### 4. Functional Tests

**Status**: Functional test framework exists, but tests are minimal

**Current State:**
- Playwright infrastructure in place
- ✅ Comprehensive authentication flow tests exist ([`Authentication.feature.cs`](../tests/Functional/Tests/Authentication.feature.cs))
  - Registration (valid, weak password, mismatched passwords, existing email)
  - Login/logout (success, invalid credentials, missing password)
  - Account management (view profile)
  - Access control (protected pages, login redirects)
- Limited tests for other features

**Implement:**
- Tenant creation and management workflow tests
- Multi-tenant access scenarios (tenant switching)
- Role-based authorization validation (Viewer/Editor/Owner)
- Transaction CRUD workflows
- Cross-tenant isolation verification
- Error handling and edge cases
- Cross-browser testing expansion

### 5. Frontend Enhancements

**Status**: ✅ Frontend feature parity with backend is COMPLETE

**Implemented Features:**
- ✅ Tenant selection/switching UI ([`WorkspaceSelector.vue`](../src/FrontEnd.Nuxt/app/components/WorkspaceSelector.vue))
- ✅ Tenant management UI with full CRUD ([`workspaces.vue`](../src/FrontEnd.Nuxt/app/pages/workspaces.vue))
- ✅ Transaction list with date range filtering ([`transactions.vue`](../src/FrontEnd.Nuxt/app/pages/transactions.vue))
- ✅ Transaction create/edit/delete forms with modals
- ✅ Error display components
- ✅ Loading states with spinners
- ✅ Pinia state management ([`userPreferences.ts`](../src/FrontEnd.Nuxt/app/stores/userPreferences.ts))

**Remaining Enhancements:**
- Toast notifications for user feedback (currently using alerts)
- Optimistic UI updates (currently refetches after mutations)
- Advanced filtering/sorting on transactions list
- Pagination for large transaction lists (backend supports date filtering)
- User role management UI (requires backend API first - see #2)

---

## Medium Priority 🟡

### 6. Enhanced Logging & Observability

**Current State:**
- ✅ Good use of [`LoggerMessage`](../src/Controllers/WeatherController.cs:37) attributes
- ✅ Structured logging with Application Insights in place
- Basic performance monitoring via Application Insights
- No correlation IDs for request tracing
- No formal logging policy document

**Enhancements:**
- Add correlation IDs for distributed tracing across frontend/backend
- Add custom metrics and telemetry to Application Insights
- Implement request/response logging middleware (with PII filtering)
- Create logging policy document (levels, what to log, security considerations)
- Monitor database query performance with EF Core logging
- Consider additional log sinks if needed (File, Console for development)

**Optional: Serilog Integration**
If you want more control over logging configuration and multiple sinks, consider Serilog as a wrapper around Application Insights. Not required since Application Insights already provides excellent structured logging.

### 7. Performance Optimizations

**Implement:**
- Response caching middleware for read-heavy endpoints
- **Redis** or in-memory caching for frequently accessed data
- Database indexes for common query patterns (already some in place)
- Response compression middleware
- API rate limiting using `AspNetCoreRateLimit`
- Pagination for all list endpoints (see API Enhancements)
- Consider compiled queries for hot paths

**Caching Example:**
```csharp
[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "fromDate", "toDate" })]
public async Task<IActionResult> GetTransactions([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
```

**Database Indexing:**
- Review query patterns in [`TransactionsFeature`](../src/Application/Features/TransactionsFeature.cs)
- Add indexes on frequently queried columns
- Add composite indexes for tenant + date queries
- Monitor query performance with EF Core logging

### 8. Configuration Management Improvements

**Current State:**
- Basic [`ApplicationOptions`](../src/Entities/Options/ApplicationOptions.cs) exists
- No options validation
- Limited environment-specific configuration

**Enhance:**
- Expand options pattern for all configurable components
- Add options validation using `IValidateOptions<T>`
- Implement Azure Key Vault integration for production secrets
- Create strongly-typed configuration classes for each feature
- Document all configuration options
- Use User Secrets for local development
- Environment-specific appsettings files (Development, Staging, Production)

**Example:**
```csharp
public class ApplicationOptionsValidator : IValidateOptions<ApplicationOptions>
{
    public ValidateOptionsResult Validate(string name, ApplicationOptions options)
    {
        if (options.AllowedCorsOrigins.Length == 0)
            return ValidateOptionsResult.Fail("At least one CORS origin required");

        return ValidateOptionsResult.Success;
    }
}
```

### 9. Testing Improvements

**Current State:**
- ✅ Code coverage collection in CI/CD ([`run-tests.yaml`](../.azure/pipelines/steps/run-tests.yaml))
- ✅ Code coverage published to Azure DevOps with Cobertura format
- ✅ Comprehensive authentication functional tests
- ✅ Comprehensive integration tests for tenancy and transactions
- No coverage gates (minimum threshold enforcement)
- No performance/load testing
- No security testing (SAST/DAST)
- No unit tests for authorization logic

**Implement:**
- Add code coverage gates to CI/CD with minimum threshold (e.g., 80%)
- Expand functional tests for tenant management workflows (see High Priority #4)
- Add API contract tests to ensure frontend/backend compatibility
- Implement mutation testing with Stryker.NET (optional)
- Add security scanning (SAST/DAST) to CI pipeline
- Create load tests for critical paths using k6 or NBomber
- Add unit tests for [`TenantRoleHandler`](../src/Controllers/Tenancy/TenantRoleHandler.cs)
- Test edge cases (invalid GUIDs, malformed claims, missing claims)

### 10. Security Hardening

**Implement:**
- Security scanning in CI/CD (Dependabot, CodeQL, OWASP)
- HTTPS enforcement in production (partially done)
- Content Security Policy headers
- Dependency vulnerability scanning
- Rate limiting per tenant/user
- Input sanitization for all user inputs
- SQL injection prevention verification (EF Core provides this)
- XSS prevention in frontend
- CSRF protection (if needed beyond JWT)
- Audit logging for sensitive operations

---

## Nice-to-Have Improvements 🟢

### 11. Developer Experience Enhancements

- Enforce `.editorconfig` in CI (file exists but not enforced)
- Implement pre-commit hooks with Husky for:
  - Linting (ESLint, Roslyn analyzers)
  - Formatting (Prettier, dotnet format)
  - Commit message validation (conventional commits)
- Add Roslyn analyzers for C# code quality
- Create code snippets/templates for common patterns
- Improve README with troubleshooting section
- Add PR templates and issue templates
- Create developer onboarding guide

### 12. Documentation Improvements

**Current State:**
- Excellent [`TENANCY.md`](TENANCY.md) documentation
- Good [`ARCHITECTURE.md`](ARCHITECTURE.md)
- Comprehensive ADRs
- Missing some implementation pattern documentation

**Add:**
- Document [`CustomExceptionHandler`](../src/Controllers/Middleware/CustomExceptionHandler.cs) pattern
- Document Transaction feature pattern as reference implementation
- Add sequence diagrams for complex flows:
  - Authentication and tenant context flow
  - Tenant switching workflow
  - Transaction processing with tenant isolation
- Document database schema with ER diagrams
- Create runbooks for common operations
- API documentation improvements (already good with Swagger)
- Consolidate or archive [`wip/TENANCY-TODO.md`](wip/TENANCY-TODO.md) into main docs

### 13. Infrastructure as Code Enhancements

**Current State:**
- Basic Bicep templates exist in [`infra/main.bicep`](../infra/main.bicep)
- No environment-specific parameters

**Enhancements:**
- Add environment-specific parameter files (dev.bicepparam, prod.bicepparam)
- Implement Azure Key Vault integration for secrets
- Add monitoring alerts and dashboards (Application Insights, Log Analytics)
- Create disaster recovery procedures
- Add cost optimization configurations (auto-scaling rules)
- Document resource naming conventions
- Add Azure Storage account for blob storage (noted as TODO in main.bicep)

### 14. CI/CD Enhancements

**Current State:**
- Basic build and test in GitHub Actions
- No deployment automation

**Add:**
- Semantic versioning and automated release notes
- Deployment stages (dev, staging, production)
- Create rollback procedures
- Add smoke tests after deployment
- Implement blue-green or canary deployment strategy
- Add performance benchmarks in CI
- Auto-generate and publish API documentation
- Container registry integration for Docker images

### 15. Advanced Tenancy Features

**See [`wip/TENANCY-TODO.md`](wip/TENANCY-TODO.md) for detailed list:**
- Tenant deactivation/soft delete
- Tenant quotas and limits
- Audit logging for tenant operations
- Tenant invitation system with email workflow
- Bulk tenant operations
- Tenant metadata and custom settings
- Advanced authorization (granular permissions)
- Multi-tenant reporting and analytics

---

## Migration Strategy for YoFi Features

Once the high-priority items above are complete, migrate YoFi features using this proven pattern:

### Per-Feature Checklist

1. ✅ Create entity models in [`Entities`](../src/Entities) project
2. ✅ Add DbSet to [`ApplicationDbContext`](../src/Data/Sqlite/ApplicationDbContext.cs)
3. ✅ Create and apply EF migration
4. ✅ Implement Feature class in [`Application`](../src/Application)
5. ✅ Create Controller in [`Controllers`](../src/Controllers) project
6. ✅ Write unit tests (Application layer)
7. ✅ Write integration tests (Controller layer)
8. ✅ Regenerate TypeScript client (run WireApiHost)
9. ✅ Create Vue components and pages
10. ✅ Write functional tests (Playwright)
11. ✅ Update documentation

### Reference Implementation

The **Transactions** feature is the reference implementation:
- [`Transaction`](../src/Entities/Models/Transaction.cs) entity with [`ITenantModel`](../src/Entities/Tenancy/ITenantModel.cs)
- [`TransactionsFeature`](../src/Application/Features/TransactionsFeature.cs) with tenant isolation pattern
- [`TransactionsController`](../src/Controllers/TransactionsController.cs) with role-based authorization
- [`TransactionsControllerTests`](../tests/Integration.Controller/TransactionsControllerTests.cs) with comprehensive scenarios
- Single enforcement point: `GetBaseTransactionQuery()` automatically filters by tenant

### Priority Features to Migrate

Based on original YoFi functionality:

1. **Categories** - For transaction categorization
2. **Payees** - Payee management and auto-categorization
3. **Budgets** - Budget planning and tracking
4. **Reports** - Financial reports and analytics
5. **Import/Export** - Transaction import from banks
6. **Receipt Storage** - Document upload and association (requires Azure Storage)

---

## Documentation Gaps & Recommendations

### Critical Documentation Needed

1. **Exception Handling Pattern**
   - Document [`CustomExceptionHandler`](../src/Controllers/Middleware/CustomExceptionHandler.cs) approach
   - Explain how to add new exception types
   - Document [`TenancyExceptionHandler`](../src/Controllers/Tenancy/Exceptions/TenancyExceptionHandler.cs) as example

2. **Feature Implementation Pattern**
   - Use [`TransactionsFeature`](../src/Application/Features/TransactionsFeature.cs) as canonical example
   - Document tenant isolation pattern (`GetBaseQuery()` approach)
   - Explain validation approach
   - Show integration test patterns

3. **Frontend Architecture**
   - Document Pinia store structure (already implemented)
   - Explain composables pattern
   - Document API client usage (NSwag-generated)
   - Show component patterns

### Consolidation Opportunities

- **[`wip/TENANCY-TODO.md`](wip/TENANCY-TODO.md)** - Core features are done; move remaining items here or to backlog
- **Consider creating** - `docs/PATTERNS.md` for common implementation patterns
- **Consider creating** - `docs/TESTING-STRATEGY.md` consolidating test documentation

### Well-Documented Areas ✅

- Multi-tenancy implementation ([`TENANCY.md`](TENANCY.md))
- Architecture and ADRs
- Database migrations and setup
- Development setup ([`CONTRIBUTING.md`](CONTRIBUTING.md))

---

## Prioritized Roadmap

### Phase 2: Robustness & Polish (3-4 weeks)

**Goal**: Production-grade reliability and completeness

1. ✅ **Input Validation** (1 week)
   - FluentValidation framework
   - Validators for all DTOs
   - Client-side validation

2. ✅ **API Enhancements** (1 week)
   - API versioning
   - Pagination support
   - Filtering and sorting
   - Standardized responses

3. ✅ **Frontend Features** (1-2 weeks)
   - Tenant selector/switcher
   - Transaction CRUD UI
   - Tenant management UI
   - Enhanced error handling

4. ✅ **User Role Management** (1 week)
   - Role management API
   - User invitation flow
   - Frontend UI for roles

### Phase 3: Production Ready (3-4 weeks)

**Goal**: Deploy to production with confidence

1. ✅ **Enhanced Logging** (1 week)
   - Serilog integration
   - Correlation IDs
   - Performance monitoring

2. ✅ **Performance Optimizations** (1 week)
   - Response caching
   - Redis integration
   - Database indexing
   - Rate limiting

3. ✅ **Security & Testing** (1 week)
   - Security scanning in CI/CD
   - Functional test suite
   - Code coverage gates
   - Performance benchmarks

4. ✅ **Infrastructure & DevOps** (1 week)
   - Environment-specific configs
   - Deployment automation
   - Monitoring alerts
   - Documentation updates

### Phase 4: YoFi Feature Migration (Variable)

**Goal**: Achieve feature parity with original YoFi

Migrate features one-by-one using the established pattern, starting with Categories and Payees.

---

## Conclusion

### Current Assessment

YoFi.V3 has **successfully completed Phase 1** with a production-ready multi-tenant foundation. The implementation demonstrates:

- ✅ Excellent architectural discipline
- ✅ Comprehensive security implementation
- ✅ Well-documented patterns
- ✅ Solid testing foundation
- ✅ Modern technology stack

### Critical Success Factors

1. **Complete Phase 2** before migrating YoFi features
2. **Maintain architectural consistency** with established patterns
3. **Keep documentation updated** as implementation evolves
4. **Prioritize security and testing** in all features
5. **Regular code reviews** to ensure quality

### Timeline Summary

- **Phase 2 (Robustness)**: 3-4 weeks
- **Phase 3 (Production)**: 3-4 weeks
- **Total to Production**: 6-10 weeks
- **Phase 4 (Features)**: Variable based on scope

### Next Steps

1. Review and approve this updated roadmap
2. Prioritize Phase 2 work items
3. Begin with Input Validation framework
4. Establish sprint/milestone tracking
5. Update this document as work progresses

The foundation is **excellent**. With focused execution on the remaining work, YoFi.V3 will be ready to replace the original YoFi application.

---

**Document Version**: 2.0
**Last Updated**: December 2025
**Previous Version**: 1.0 (November 2025) - Phase 1 objectives achieved
