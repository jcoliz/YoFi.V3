# Implementation Workflow

**Purpose:** Systematic approach for implementing PRD stories from design through deployment.

**Target Audience:** Orchestrator agents coordinating implementation work across multiple layers.

**Philosophy:** Incremental progress with **user-controlled commits after each major step**. Always run tests after changes.

**Commit Cadence:** After completing each major step below, **pause and present work to user for review and commit**. Each step represents a logical unit of work that should be committed to source control. The orchestrator should NOT commit directly - the user maintains control over git commits.

---

## Testing Philosophy

**Test Execution Pattern:** Always run tests after creating or modifying them to verify they pass. Use `./scripts/Run-Tests.ps1` for all unit and integration tests. Fix failures immediately before proceeding. See [`.roorules`](../../.roorules) "Test Execution Pattern" for details.

**Test Distribution Target:**
- **60-70% Controller Integration tests** - API contracts, authorization, HTTP behavior (PRIMARY test layer)
- **19-25% Unit tests** - Business logic, algorithms, validation (40% for algorithm-heavy features)
- **10-15% Functional tests** - Critical user workflows, UI-dependent only

See [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) for detailed guidance on test layer selection.

**Key Principle:** Default to Controller Integration tests. Reserve Functional tests for critical paths. Use Unit tests for algorithmic complexity.

---

## Reference Implementations

For implementation patterns, refer to existing features:

**Backend:**
- Transaction CRUD: [`src/Controllers/TransactionsController.cs`](../../src/Controllers/TransactionsController.cs), [`src/Application/Features/TransactionsFeature.cs`](../../src/Application/Features/TransactionsFeature.cs)
- Tenancy Pattern: [`src/Entities/Models/BaseTenantModel.cs`](../../src/Entities/Models/BaseTenantModel.cs)
- Logging: [`docs/LOGGING-POLICY.md`](../LOGGING-POLICY.md)

**Testing:**
- Controller Integration: [`tests/Integration.Controller/TransactionsControllerTests.cs`](../../tests/Integration.Controller/TransactionsControllerTests.cs)
- Test Strategy: [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md)

**Frontend:**
- Vue Pages: [`src/FrontEnd.Nuxt/app/pages/transactions.vue`](../../src/FrontEnd.Nuxt/app/pages/transactions.vue)
- Frontend Rules: [`src/FrontEnd.Nuxt/.roorules`](../../src/FrontEnd.Nuxt/.roorules)

---

## Commit Checkpoints & User Review

**IMPORTANT:** After completing each major step below, **pause and ask the user to review and commit** the changes. The orchestrator should NOT commit directly - the user maintains control over git commits.

**After Each Step:**
1. Verify all tests pass (if applicable to that step)
2. Review changes for completeness
3. **Present summary of changes to user**
4. **Create a commit message for the work** using conventional commit format (see [`docs/COMMIT-CONVENTIONS.md`](../COMMIT-CONVENTIONS.md)). ALWAYS keep commit messages under 100 words
5. **Ask user to review and commit**
6. Wait for user confirmation before proceeding to next step

**Suggested Commit Messages by Step (for user's reference):**

**IMPORTANT:** Use the PRD feature slug (e.g., `transaction-record`, `payee-rules`) in the scope for all non-test commits. Test commits always use layer scopes (`test(unit)`, `test(integration)`, `test(functional)`).

- **Step 3 (Entities):** `feat([feature-slug]): add [EntityName] entity`
- **Step 4 (Data Layer):** `feat([feature-slug]): add [EntityName] EF Core configuration`
- **Step 5 (Data Integration Tests):** `test(integration): add [EntityName] data tests`
- **Step 6 (Application Layer):** `feat([feature-slug]): implement business logic`
- **Step 7 (Unit Tests):** `test(unit): add [feature] validation and logic tests`
- **Step 8 (Controllers):** `feat([feature-slug]): add API endpoints`
- **Step 8.5 (API Client):** `build([feature-slug]): regenerate API client`
- **Step 9 (Controller Integration Tests):** `test(integration): add API endpoint tests`
- **Step 10 (Frontend):** `feat([feature-slug]): implement UI`
- **Step 10.4 (Verify Existing Functional Tests):** `fix([feature-slug]): resolve functional test failures`
- **Step 10.5 (Functional Tests Plan):** `test(functional): add [feature] test plan`
- **Step 10.6 (Functional Test Implementation Plan):** `test(functional): add [feature] implementation plan`
- **Step 11.X (Functional Tests):** `test(functional): implement [feature] - [scenario name]` (one commit per scenario)
- **Step 11.5 (Documentation):** `docs([feature-slug]): update documentation`
- **Step 12 (Wrap-up):** `feat([feature-slug]): complete implementation`

---

## Implementation Steps

### [ ] 1. Establish Scope & Entry Readiness

**Orchestrator Checklist:**
- [ ] Identify exact PRD in `docs/wip/{feature-area}/`
- [ ] Verify PRD exists (if not, advise user to create using [`PRD-TEMPLATE.md`](PRD-TEMPLATE.md))
- [ ] Verify PRD YAML has `status: Approved`
- [ ] Check for linked design document in PRD
- [ ] Clarify which stories are in scope
- [ ] Clarify if implementing full stories or partial

**No commit for this step - planning only.**

---

### [ ] 2. Detailed Design

**Orchestrator Checklist:**
- [ ] Find existing design document OR delegate creation to Architect mode
- [ ] Verify design document location: same directory as PRD
- [ ] Verify design YAML has `status: Approved`
- [ ] Ensure PRD links to design document

**Checkpoint:** Confirm design approval with user before proceeding to implementation.

---

### [ ] 3. Entities

**Orchestrator Instructions for Code Mode:**

> Implement entities per design document. Follow patterns in [`src/Entities/Models/BaseTenantModel.cs`](../../src/Entities/Models/BaseTenantModel.cs) or [`BaseModel.cs`](../../src/Entities/Models/BaseModel.cs). Build project and run unit tests.

**Code Mode Checklist:**
- [ ] Implement entity classes per design
- [ ] Inherit from BaseTenantModel or BaseModel
- [ ] Add XML documentation to all classes
- [ ] Build: `dotnet build`
- [ ] Run unit tests: `dotnet test tests/Unit`

**Commit Template:** `feat([feature-slug]): add [EntityName] entity`

---

### [ ] 4. Data Layer

**Orchestrator Instructions for Code Mode:**

> Add entities to DbContext, configure EF Core relationships/indexes, create migration, verify with data integration tests.

**Code Mode Checklist:**
- [ ] Add DbSet to [`ApplicationDbContext.cs`](../../src/Data/Sqlite/ApplicationDbContext.cs)
- [ ] Add EF Core configurations in OnModelCreating
- [ ] Add indexes as designed
- [ ] Build: `dotnet build`
- [ ] Create migration: `.\scripts\Add-Migration.ps1 -Name "Add{FeatureName}"`
- [ ] Review migration for correctness
- [ ] Run data tests: `dotnet test tests/Integration.Data`
- [ ] Iterate until tests pass

**Commit Template:** `feat([feature-slug]): add [EntityName] EF Core configuration`

---

### [ ] 5. Data Integration Tests

**Orchestrator Instructions for Code Mode:**

> Review [`TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) to identify acceptance criteria for data integration tests. Focus on EF configurations, relationships, queries, data integrity.

**Code Mode Checklist:**
- [ ] Review TESTING-STRATEGY.md Decision Framework
- [ ] Create test file in `tests/Integration.Data/`
- [ ] Implement data integration tests (target: ~10-15% of total)
- [ ] Run tests: `dotnet test tests/Integration.Data`
- [ ] Iterate until all pass
- [ ] Check off completed acceptance criteria in PRD

**Commit Template:** `test(integration): add [EntityName] data tests`

---

### [ ] 6. Application Layer

**Orchestrator Instructions for Code Mode:**

> Implement business logic in Application Features following [`TransactionsFeature.cs`](../../src/Application/Features/TransactionsFeature.cs) pattern. Update existing tests to build/pass.

**Code Mode Checklist:**
- [ ] Implement Feature class in `src/Application/Features/`
- [ ] Add XML documentation (class + methods)
- [ ] Follow existing patterns (dependency injection, async/await)
- [ ] Build: `dotnet build`
- [ ] Run all tests: `pwsh -File ./scripts/Run-Tests.ps1`
- [ ] Fix any breaking changes

**Checkpoint:** Confirm core business logic is correct before UI work.

**Commit Template:** `feat([feature-slug]): implement business logic`

---

### [ ] 7. Unit Tests

**Orchestrator Instructions for Code Mode:**

> Review [`TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) to identify acceptance criteria for unit tests. Focus on validation, calculations, algorithms, DTO transformations. Target: 19-25% of tests.

**Code Mode Checklist:**
- [ ] Review TESTING-STRATEGY.md Decision Framework
- [ ] Create test file in `tests/Unit/`
- [ ] Implement unit tests with comprehensive coverage
- [ ] Run tests: `dotnet test tests/Unit`
- [ ] Iterate until all pass
- [ ] Check off any completed acceptance criteria in PRD

**Commit Template:** `test(unit): add [feature] validation and logic tests`

---

### [ ] 8. Controllers & Supporting Infrastructure

**Orchestrator Instructions for Code Mode:**

> Implement controller following [`TransactionsController.cs`](../../src/Controllers/TransactionsController.cs) pattern. Add XML docs, logging ([`LoggerMessage`] per [`LOGGING-POLICY.md`](../LOGGING-POLICY.md)), ASP.NET components as needed.

**Code Mode Checklist:**
- [ ] Implement controller in `src/Controllers/`
- [ ] Add XML documentation (class + all public methods)
- [ ] Add logging: LogStarting(), LogOk(), explicit event IDs, [CallerMemberName]
- [ ] Implement middleware/filters/authorization policies if needed
- [ ] Build: `dotnet build`
- [ ] Verify no compilation errors

**Commit Template:** `feat([feature-slug]): add API endpoints`

---

### [ ] 8.5. API Client Generation & Frontend Compatibility

**Orchestrator Instructions for Code Mode:**

> Regenerate TypeScript API client, fix frontend breaking changes, verify app functionality. See detailed checklist below.

**Code Mode Checklist:**
- [ ] Regenerate TypeScript client: `pwsh -File ./scripts/Generate-ApiClient.ps1`
- [ ] Verify new endpoints/DTOs in [`apiclient.ts`](../../src/FrontEnd.Nuxt/app/utils/apiclient.ts)
- [ ] Fix frontend breaking changes from API updates
- [ ] Build frontend: `pnpm run build` (from FrontEnd.Nuxt/)
- [ ] **Ask user** to run: `.\scripts\Start-LocalDev.ps1`
- [ ] **Wait for confirmation** app is running
- [ ] Run functional tests: `dotnet test tests/Functional`
- [ ] Fix failures OR ask user to verify UI if no functional tests exist

**If generation fails:** Verify [HttpGet]/[HttpPost] and [ProducesResponseType] attributes on controller methods.

**Commit Template:** `build([feature-slug]): regenerate API client`

---

### [ ] 9. Controller Integration Tests

**Orchestrator Instructions for Code Mode:**

> Controller Integration is PRIMARY test layer (60-70% of acceptance criteria). Follow [`TransactionsControllerTests.cs`](../../tests/Integration.Controller/TransactionsControllerTests.cs) pattern. Test API contracts, authorization variants, HTTP behavior.

**Code Mode Checklist:**
- [ ] Review TESTING-STRATEGY.md - Controller Integration is sweet spot
- [ ] Create test file in `tests/Integration.Controller/`
- [ ] Implement tests for all endpoints
- [ ] Include authorization variants: Success, Forbidden, Unauthorized, Tenant isolation
- [ ] Run tests: `dotnet test tests/Integration.Controller`
- [ ] Iterate until all pass
- [ ] Check off completed acceptance criteria in PRD

**Why Controller Integration:**
- Tests complete API contract (request → response)
- Includes database (in-memory, fast ~100-200ms)
- Validates auth/authz
- Low maintenance (no UI coupling)

**Commit Template:** `test(integration): add API endpoint tests`

---

### [ ] 10. Front End

**Orchestrator Instructions for Code Mode:**

> Read [`src/FrontEnd.Nuxt/.roorules`](../../src/FrontEnd.Nuxt/.roorules) before implementing. Implement Vue pages/components, add `data-test-id` attributes for functional tests. Follow detailed checklist.

**Code Mode Checklist:**
- [ ] Review FrontEnd.Nuxt/.roorules for patterns
- [ ] Verify API client up-to-date (regenerate if needed)
- [ ] Implement Vue pages in `src/FrontEnd.Nuxt/app/pages/`
- [ ] Look for reusable component opportunities
- [ ] **Add `data-test-id` to ALL interactive elements** (buttons, inputs, links)
- [ ] Use composables for shared logic
- [ ] Format: `pnpm format` (from FrontEnd.Nuxt/)
- [ ] Lint: `pnpm lint` (from FrontEnd.Nuxt/)
- [ ] Build: `pnpm run build` (from FrontEnd.Nuxt/)

**data-test-id Format:** `data-test-id="descriptive-action-name"` (kebab-case)

**Commit Template:** `feat([feature-slug]): implement UI`

---

### [ ] 10.4. Verify Existing Functional Tests

**Orchestrator Instructions for Code Mode:**

> Ensure new frontend doesn't break existing functionality before planning new tests.

**Code Mode Checklist:**
- [ ] **Ask user** to run: `.\scripts\Start-LocalDev.ps1`
- [ ] **Wait for confirmation** app is running
- [ ] Run existing tests: `dotnet test tests/Functional`
- [ ] **If all pass:** Proceed to Step 10.5
- [ ] **If failures:** Fix frontend breaking changes immediately
- [ ] Re-run tests until all pass
- [ ] **DO NOT proceed** until all existing tests green

**Why this matters:** Catches breaking changes early before planning new tests.

**Commit Template (if fixes needed):** `fix([feature-slug]): resolve functional test failures`

---

### [ ] 10.5. Functional Tests Plan

**Mode Assignment:** Delegate to **Architect** agent.

**Orchestrator Instructions for Architect Mode:**

> Create functional test plan identifying critical UI-dependent workflows (10-15% target). Review [`TESTING-STRATEGY.md`](../TESTING-STRATEGY.md). Focus on single-responsibility scenarios. Use detailed guidance in template below.

**Architect Mode:** See [`FUNCTIONAL-TEST-PLAN-TEMPLATE.md`](templates/FUNCTIONAL-TEST-PLAN-TEMPLATE.md) for complete instructions.

**Key Requirements:**
- Each scenario tests ONE workflow/acceptance criterion
- Justify each scenario (risk, why not covered by other layers)
- State risk category (business logic OR UI contract) and language tier (Tier 1 OR Tier 2)
- Use Gherkin Rule keyword to group related scenarios
- Target: 10-15% of total tests
- NO C# code at this stage

**Commit Template:** `test(functional): add [feature] test plan`

---

### [ ] 10.6. Functional Test Implementation Plan

**Mode Assignment:** Delegate to **Architect** agent.

**Orchestrator Instructions for Architect Mode:**

> Create detailed implementation plan bridging Gherkin to C# code. Read [`tests/Functional/INSTRUCTIONS.md`](../../tests/Functional/INSTRUCTIONS.md) before starting. Analyze POMs, step definitions, test control endpoints, test data for ALL scenarios.

**Architect Mode:** See [`FUNCTIONAL-TEST-IMPLEMENTATION-PLAN-TEMPLATE.md`](templates/FUNCTIONAL-TEST-IMPLEMENTATION-PLAN-TEMPLATE.md) for complete instructions.

**Key Requirements:**
- Review functional test plan (must be `status: Approved`)
- Analyze: Page Object Models, Step Definitions, Test Control Endpoints, Test Data
- Create plan document in same directory as PRD
- Update PRD YAML with implementation plan link

**Why this matters:** Identifies technical gaps before coding starts, prevents mid-implementation surprises.

**Commit Template:** `test(functional): add [feature] implementation plan`

---

### [ ] 11. Functional Tests (ONE SCENARIO AT A TIME)

**ORCHESTRATOR WORKFLOW:**

This step requires creating **ONE SUBTASK PER SCENARIO** and waiting for user approval after each completes.

1. **Read functional test implementation plan** to identify all scenarios
2. **Create subtask for Scenario 1** with instructions below
3. **Wait for subtask completion** and user commit approval
4. **Create subtask for Scenario 2** with instructions below
5. **Wait for subtask completion** and user commit approval
6. **Repeat** until all scenarios complete
7. **After all scenarios:** Ask user to run container verification

**ORCHESTRATOR INSTRUCTIONS FOR EACH CODE MODE SUBTASK:**

> Implement functional test for [Scenario N of Y]: "[Scenario Name from implementation plan]"
>
> Read functional test implementation plan section for this scenario. Follow checklist below. Implement ONLY this scenario - do NOT implement other scenarios.
>
> CRITICAL: Implement → Test → Pass → Present commit → Return to Orchestrator

**CODE MODE CHECKLIST (For Each Scenario Subtask):**

**Setup (First scenario only):**
- [ ] Locate functional test implementation plan (`status: Approved`)
- [ ] Read [`tests/Functional/INSTRUCTIONS.md`](../../tests/Functional/INSTRUCTIONS.md)
- [ ] Read [`tests/Functional/.roorules`](../../tests/Functional/.roorules)
- [ ] **Ask user** to run: `.\scripts\Start-LocalDev.ps1`
- [ ] **Wait for confirmation** app is running

**Implementation (Every scenario):**
- [ ] Review implementation plan section for THIS scenario
- [ ] Write Gherkin scenario in `tests/Functional/Features/` (THIS scenario only)
- [ ] Update Page Object Models in `tests/Functional/Pages/` (only selectors needed for THIS scenario)
- [ ] Write step definitions in `tests/Functional/Steps/` (ONLY steps for THIS scenario)
- [ ] If needed: Add test control endpoints + regenerate API client
- [ ] Manually regenerate test file per [`tests/Functional/INSTRUCTIONS.md`](../../tests/Functional/INSTRUCTIONS.md)
- [ ] Run ONLY this scenario: `dotnet test tests/Functional --filter "DisplayName~ScenarioName"`
- [ ] Iterate until THIS scenario passes
- [ ] Verify no regressions in existing scenarios

**Completion (Every scenario):**
- [ ] Update status in implementation plan (mark scenario complete)
- [ ] **Present commit message to user** (see template below)
- [ ] **Return to Orchestrator** for user approval and next scenario

**Key Principles:**
- One scenario per subtask
- One subtask per commit
- User approval required between scenarios
- App stays running across all scenarios (ask to start only once for first scenario)

**Commit Template Per Scenario:**
```
test(functional): implement [feature] - [scenario name]

Implements: "[Full Scenario Name from feature file]"
- POMs: [list changes]
- Steps: [list new methods]
- Test Control: [list additions if any]

Status: [X of Y scenarios complete]
```

---

### [ ] 11.5. Documentation Updates

**Orchestrator Instructions for Code Mode:**

> Verify XML docs on all new code, update README files if new patterns introduced.

**Code Mode Checklist:**
- [ ] Verify XML docs on all public classes/methods (Controllers, Features, Records)
- [ ] Update README files if new patterns: Frontend, Test projects, Scripts
- [ ] Consider ADR in `docs/adr/` for significant design decisions
- [ ] Build to verify 0 documentation warnings

**Reference:** XML Documentation Comments Pattern in [`.roorules`](../../.roorules)

**Commit Template:** `docs([feature-slug]): update documentation`

---

### [ ] 12. Wrap-up & Final Testing

**Orchestrator Instructions for Code Mode:**

> Run full test suite, verify distribution, update PRD/design/roadmap, create implementation summary.

**Code Mode Checklist:**
- [ ] Run all tests: `pwsh -File ./scripts/Run-Tests.ps1`
- [ ] Fix any failures before proceeding
- [ ] Calculate test distribution by layer
- [ ] Verify distribution aligns with targets (60-70% Controller, 19-25% Unit, 10-15% Functional)
- [ ] Review uncompleted acceptance criteria (propose coverage)
- [ ] Update PRD: Check off acceptance criteria, mark stories "Implemented", update YAML to `status: Implemented`
- [ ] Update design document: Add implementation summary, update YAML to `status: Implemented`
- [ ] Update [`docs/PRODUCT-ROADMAP.md`](../PRODUCT-ROADMAP.md)
- [ ] Create implementation summary document

**Final Commit Template:**
```
feat([feature-slug]): complete implementation

*** IMPLEMENTATION COMPLETE ***
Stories implemented: [list story numbers/names]
- Feature 1 description
- Feature 2 description

Test coverage: X tests ([breakdown by layer])
```

---

## Common Issues & Solutions

**Tests fail after data layer changes:**
- Check EF Core configurations in DbContext
- Verify migration applied correctly
- Check tenant isolation violations

**Functional tests cannot find elements:**
- Review Page Object Model selectors
- Verify frontend running (local or container)
- Check timing issues (use DOM waits, not explicit delays)

**API client missing new endpoints:**
- Rebuild WireApiHost: `dotnet build src/WireApiHost`
- Verify [HttpGet]/[HttpPost] and [ProducesResponseType] attributes

**Authorization tests fail:**
- Verify [RequireTenantRole] attributes on controller methods
- Check TestAuthenticationHandler configuration
- Ensure tenant context set in test setup
