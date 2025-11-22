# Test Control Strategy

## Problem

Functional tests need to control the system under test to set up test scenarios:
- Create test accounts
- Delete existing test accounts
- Clear data created by test accounts

## Solution: Environment-Aware Test Control API

A dual-mode approach that enables safe testing in all environments, including production, using isolated test workspaces and environment-specific capabilities.

### My initial notes

- For local development and initial CI, no actual API key is needed. This is just added complexity. API key only needed for deployed code (staging, production, etc.)
- Test control endpoints will be scoped to only allow access to users with `__TEST__` key in their username. (Or perhaps, require `__TEST__.com` domain?).
- Test accounts will be regularly deleted
- Ergo, even if the key leaks, the impact to real user data would be avoided.
- Once all tests are complete I can review the usage to design a more locked down form, e.g. pre-seeded accounts only.
- I am auto-generating an NSwag C# client against all controllers, so I can call this directly from my tests.
- As always, when I *do* make changes to production environment, I will roll those into CD pipeline in order to catch issues early.

### Implementation Steps

1. **Implement workspace/tenant isolation** — Create dedicated test workspace(s) separate from production user data, leveraging existing multi-workspace architecture (`/workspace/dashboard`). Test data operations are scoped to these test workspaces only.

2. **Create environment-aware test control API** at `/api/testcontrol` with three modes:
   - **Development/Staging**: Full capabilities (create user, delete user, reset workspace data) secured by API key
   - **Production Option A**: Completely disabled via conditional registration in `Program.cs` using `builder.Environment.IsProduction()`
   - **Production Option B**: Read-only verification endpoints only (check test workspace exists, verify account status)

3. **Create `TestControlClient` helper class** in `Helpers/TestControlClient.cs` to wrap HTTP calls to the test API, reading API key and base URL from test settings.

4. **Add environment-specific runsettings files**:
   - `local.runsettings` / `docker.runsettings`: `testControlEnabled=true`, API key via environment variable
   - `production.runsettings`: Production URL, longer timeouts, `testControlEnabled=false`, test workspace identifier

5. **Implement test data strategy** with environment-aware behavior:
   - **Non-production**: Create/delete test accounts dynamically via API for each test run
   - **Production**: Use pre-created, long-lived test accounts in dedicated workspace; verify accounts exist rather than creating them

6. **Update test base classes** in `Steps/FunctionalTest.cs` with environment-aware setup/teardown — Check `testControlEnabled` parameter, use API controls when available, fall back to pre-existing account verification for production, implement existing TODOs.

7. **Update authentication tests** in `Features/Authentication.feature` and `Steps/AuthenticationSteps.cs` to leverage test controls, replacing hardcoded assumptions about pre-existing test accounts.

### Key Design Decisions

**API vs UI Control**
API endpoint approach recommended — cleaner separation, faster execution, easier to secure. UI-based control (special admin user with privileged role) adds overhead and mixes concerns.

**Database Direct Access**
Not recommended — tightly couples tests to database schema, breaks on schema changes, requires exposing connection strings. Use API abstraction instead.

**Production Safety**
- Option A (safest): Completely disable `/api/testcontrol` in production; requires pre-created accounts
- Option B: Allow read-only verification endpoints for test diagnostics
- Option C (future): Allow reset operations scoped to test workspace only
- Recommend starting with Option A, evolving to C as confidence grows

**Data Isolation Strategy**
- If true multi-tenancy exists, workspace isolation may suffice
- For shared tables (users, auth), consider:
  - Test account email domain filter (`*@test.example.com`)
  - Separate Azure App Service deployment slot for staging
  - Feature flags to route test users to isolated data stores

**Security Hardening**
- API key authentication for all test control endpoints
- Environment variable or Azure Key Vault for API key storage
- Consider IP whitelist restrictions in shared environments
- Document API key rotation process
- Use conditional compilation (`#if DEBUG`) or runtime checks (`IsDevelopment()`, `IsStaging()`)

**Production Monitoring**
Integrate test results with production observability:
- Send test failures to monitoring system (Application Insights, Datadog)
- Set up alerts for consecutive failures
- Add test correlation IDs to application logs
- Schedule smoke tests every 15-30 minutes
