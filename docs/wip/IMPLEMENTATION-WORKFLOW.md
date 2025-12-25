# Implementation Workflow

**Purpose:** Systematic approach for implementing PRD stories from design through deployment.

**Target Audience:** Orchestrator agents coordinating implementation work across multiple layers.

**Philosophy:** Incremental progress with **user-controlled commits after each major step**. Always run tests after changes.

**Commit Cadence:** After completing each of the 13 major steps below, **pause and present work to user for review and commit**. Each step represents a logical unit of work that should be committed to source control. The orchestrator should NOT commit directly - the user maintains control over git commits.

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

**IMPORTANT:** After completing each of the 13 major steps below, **pause and ask the user to review and commit** the changes. The orchestrator should NOT commit directly - the user maintains control over git commits.

**After Each Step:**
1. Verify all tests pass (if applicable to that step)
2. Review changes for completeness
3. **Present summary of changes to user**
4. **Create a commit message for the work** using conventional commit format (see [`docs/COMMIT-CONVENTIONS.md`](../COMMIT-CONVENTIONS.md)). ALWAYS keep commit messages under 100 words (I know it's hard!)
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
- **Step 11 (Functional Tests):** `test(functional): add [feature] user workflow tests`
- **Step 11.5 (Documentation):** `docs([feature-slug]): update documentation`
- **Step 12 (Wrap-up):** `feat([feature-slug]): complete implementation`

**Examples:**
- `feat(transaction-record): add Transaction entity`
- `test(integration): add Transaction data persistence tests`
- `feat(payee-rules): implement rule matching logic`
- `test(unit): add payee rule validation tests`
- `test(functional): add transaction creation workflow`

**Benefits of Commit-Per-Step:**
- Provides rollback points if issues arise
- Makes code review easier (smaller, logical chunks)
- Ensures work is backed up regularly
- Creates clear audit trail of implementation progress

---

## [ ] 1. Establish Scope & Entry Readiness

1. Ensure clarity on which exact PRD we are working from. These are located in `docs/wip/{feature-area}`.
2. Ensure PRD exists, otherwise abort and advise to create PRD using [`docs/wip/PRD-TEMPLATE.md`](PRD-TEMPLATE.md)
3. Ensure PRD has YAML metadata with `state` containing `Approved`
4. Check if PRD already links to a detailed design document (see PRD "Related Documents" section)
5. Ensure clarity on which story or stories from within that PRD are in scope
6. Ensure clarity on whether we are implementing the entire identified stories, or just some part--and if just some part, which part?

---

## [ ] 2. Detailed Design

1. Determine whether there is already a detailed design document. If not, create one scoped to only the stories, story, or part of story that's in scope. The file should go in the same directory where the PRD is located (`docs/wip/` per Documentation and Planning Pattern).
2. Ensure clarity on which file contains the detailed design.
3. Ensure detailed design has YAML metadata with `state` containing `Approved`. If not, work with user to review as needed and approve.
4. Ensure PRD has a link to the detailed design document. If not, add a link in the PRD

**Checkpoint:** Confirm design approval with user before proceeding to implementation.

---

## [ ] 3. Entities

1. Implement Entities, as designed
2. Follow existing patterns: [`src/Entities/Models/BaseTenantModel.cs`](../../src/Entities/Models/BaseTenantModel.cs) or [`src/Entities/Models/BaseModel.cs`](../../src/Entities/Models/BaseModel.cs)
3. Build project and run unit tests. Note that integration tests would fail until next step is completed.

**Commit:** Present entity implementation to user for review and commit.

---

## [ ] 4. Data Layer

1. Add new entities to application db context
2. Add model creating (EF Core configurations)
3. Add indices, as needed
4. Build and test (verify no compilation errors)
5. Create migration using `.\scripts\Add-Migration.ps1 -Name "Add{FeatureName}"`
6. Review generated migration for correctness
7. Run data integration tests to verify schema changes: `dotnet test tests/Integration.Data`
8. Iterate until tests pass

**Commit:** Present data layer changes and migration to user for review and commit.

---

## [ ] 5. Data Integration Tests

1. Review [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) Decision Framework to determine which acceptance criteria are best suited for data integration tests
2. Create Data Integration tests covering the new data layer changes (focus: EF configurations, relationships, complex queries, data integrity)
3. Implement chosen Data Integration tests for relevant acceptance criteria
4. Run tests: `dotnet test tests/Integration.Data`
5. Fix failures immediately and iterate until all tests pass
6. Check off any completed acceptance criteria in PRD

**Note:** Data Integration tests typically cover ~10-15% of acceptance criteria. Most will map to Controller Integration tests (Step 9).

**Commit:** Present data integration tests to user for review and commit.

---

## [ ] 6. Application Layer

1. Implement Application Layer business logic
2. Follow existing patterns: [`src/Application/Features/TransactionsFeature.cs`](../../src/Application/Features/TransactionsFeature.cs)
3. Update all tests to ensure they build and pass. No new tests needed at this point, that will come in the following step.

**Checkpoint:** Confirm core business logic is correct before UI work.

**Commit:** Present application layer implementation to user for review and commit.

---

## [ ] 7. Unit Tests

1. Review [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) Decision Framework to determine which acceptance criteria require unit tests
2. Focus on: Business logic, validation rules, calculations, algorithms, DTO transformations
3. Implement Unit tests with comprehensive coverage
4. Run tests: `dotnet test tests/Unit`
5. Fix failures immediately and iterate until all tests pass
6. Check off any completed acceptance tests in PRD

**Target:** 19-25% of total tests (40% for algorithm-heavy features like receipt matching)
**Pattern:** Pure logic, mocked dependencies, no HTTP/database context

**Commit:** Present unit tests to user for review and commit.

---

## [ ] 8. Controllers & Supporting Infrastructure

1. Implement Controller following [`src/Controllers/TransactionsController.cs`](../../src/Controllers/TransactionsController.cs) pattern
2. Add comprehensive XML documentation comments (class and all public methods) per XML Documentation Comments Pattern in `.roorules`
3. Add logging using `[LoggerMessage]` pattern per [`docs/LOGGING-POLICY.md`](../LOGGING-POLICY.md)
   - Use `LogStarting()`, `LogOk()`, `LogProblemWarning()` patterns
   - Include explicit event IDs (1, 2, 3, etc.)
   - Add `[CallerMemberName]` parameter to all log methods
4. Implement any ASP.NET supporting components (middleware, filters, authorization policies)
5. Build and verify no compilation errors

**Commit:** Present controller implementation to user for review and commit.

---

## [ ] 8.5. API Client Generation

1. Build the WireApiHost project to generate the updated TypeScript API client: `dotnet build src/WireApiHost`
2. Verify the API client was regenerated successfully
   - Location: [`src/FrontEnd.Nuxt/app/utils/apiclient.ts`](../../src/FrontEnd.Nuxt/app/utils/apiclient.ts)
   - Check that new endpoints are included
   - **DO NOT edit this file manually** - it's auto-generated from NSwag
3. Review the generated client for any unexpected changes
4. If client generation fails, verify:
   - Controller methods have proper HTTP attributes ([HttpGet], [HttpPost], etc.)
   - Controller methods have [ProducesResponseType] attributes
   - NSwag configuration in [`src/WireApiHost/nswag.json`](../../src/WireApiHost/nswag.json) is correct

**Commit:** Present API client generation to user for review and commit.

---

## [ ] 9. Controller Integration Tests

1. Review [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) - **Controller Integration is the PRIMARY test layer** (60-70% of acceptance criteria belong here)
2. Focus on: API contracts, authorization (Viewer/Editor/Owner variants), HTTP status codes, request/response formats, error handling, ProblemDetails
3. Implement Controller Integration Tests for all API endpoints
4. Include authorization test variants:
   - Success case (authorized user with correct role)
   - Forbidden case (insufficient permissions)
   - Unauthorized case (no authentication)
   - Tenant isolation case (different tenant)
5. Run tests: `dotnet test tests/Integration.Controller`
6. Fix failures immediately and iterate until all tests pass
7. Check off any completed acceptance tests in PRD

**Why Controller Integration is the sweet spot:**
- Tests complete API contract (request → response)
- Includes database operations (in-memory, fast ~100-200ms)
- Validates authentication/authorization
- Low maintenance (no UI coupling)
- Reflects real-world API usage

**Commit:** Present controller integration tests to user for review and commit.

---

## [ ] 10. Front End

1. Verify API client is up-to-date (regenerated from NSwag after controller changes)
   - Location: [`src/FrontEnd.Nuxt/app/utils/apiclient.ts`](../../src/FrontEnd.Nuxt/app/utils/apiclient.ts)
   - **DO NOT edit manually** - auto-generated file
2. Implement Vue pages/components following patterns in [`src/FrontEnd.Nuxt/app/pages/`](../../src/FrontEnd.Nuxt/app/pages/)
3. **Look for opportunities to abstract functionality into reusable components**
   - Review if UI patterns can be extracted to [`src/FrontEnd.Nuxt/app/components/`](../../src/FrontEnd.Nuxt/app/components/)
   - Consider modals, forms, data tables, or repeated UI patterns
   - Reference existing components: [`ModalDialog.vue`](../../src/FrontEnd.Nuxt/app/components/ModalDialog.vue), [`ErrorDisplay.vue`](../../src/FrontEnd.Nuxt/app/components/ErrorDisplay.vue)
4. **Add `data-test-id` attributes to all interactive elements** that users will interact with
   - Buttons, inputs, links, form fields, etc.
   - Format: `data-test-id="descriptive-action-name"` (kebab-case)
   - Example: `data-test-id="create-transaction-button"`, `data-test-id="payee-input"`
   - **Purpose:** Functional tests will need these selectors in Page Object Models
5. Use composables for shared logic (see [`src/FrontEnd.Nuxt/app/composables/`](../../src/FrontEnd.Nuxt/app/composables/))
6. Verify TypeScript compilation passes: `npm run typecheck` (from FrontEnd.Nuxt directory)
7. Build frontend to verify no errors: `npm run build`
8. Review frontend-specific rules in [`src/FrontEnd.Nuxt/.roorules`](../../src/FrontEnd.Nuxt/.roorules)

**Commit:** Present frontend implementation to user for review and commit.

---

## [ ] 11. Functional Tests

1. Review [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) to identify critical workflows worthy of functional tests
   - **Target:** 10-15% of total tests
   - **Focus:** UI-dependent workflows only (login, registration, critical feature paths)
   - **Avoid:** Testing API behavior that's already covered by Controller Integration tests
2. Write complete Gherkin feature file in [`tests/Functional/Features/`](../../tests/Functional/Features/)
   - **Note:** One `.feature` file usually maps to one PRD
3. **Implement scenarios ONE AT A TIME (never multiple simultaneously):**
   - For the current scenario only:
     - Create or update Page Object Models in [`tests/Functional/Pages/`](../../tests/Functional/Pages/)
     - Write step definitions in [`tests/Functional/Steps/`](../../tests/Functional/Steps/)
     - Use Gherkin comments (Given/When/Then/And) in step implementations
   - Complete and verify one scenario before moving to the next
4. **ITERATIVE TESTING PROCESS (for each scenario):**
   - First functional test only: Ask user to run .\scripts\Start-LocalDev.ps1 and wait for confirmation
   - Subsequent tests reuse the running application
   - Implement one scenario at a time
   - Run ONLY the scenario you're working on: `dotnet test tests/Functional --filter "DisplayName~ScenarioName"`
     - Example: `dotnet test tests/Functional --filter "DisplayName~CreateNewTransaction"`
     - This runs against the local development environment (fast feedback loop)
   - Review test results and fix issues immediately
   - Iterate on that one scenario until it passes
   - Move to next scenario and repeat
5. **FULL TEST SUITE VERIFICATION:**
   - Once all scenarios pass locally, ask user to run full suite against container: `.\scripts\Run-FunctionalTestsVsContainer.ps1`
   - This ensures tests work in CI/CD environment
   - Wait for user to report results
   - Fix any container-specific issues
6. Check off any completed acceptance tests in PRD

**Why functional tests are minimal:**
- Slow execution (seconds per test)
- Brittle (UI changes break tests)
- High maintenance overhead
- Most API behavior already verified by Controller Integration tests

**Checkpoint:** Confirm all critical user workflows are verified.

**Commit:** Present functional tests to user for review and commit.

---

## [ ] 11.5. Documentation Updates

1. **XML Documentation:** Verify all public classes and methods have XML documentation comments
   - Controllers: `<summary>`, `<param>`, `<returns>` (except `Task<IActionResult>`), `<exception>`
   - Features: Primary constructor parameters documented with `<param>` on class
   - Records/DTOs: Class-level `<summary>` required
2. **README Updates:** Update relevant README files if new patterns were introduced
   - [`src/FrontEnd.Nuxt/README.md`](../../src/FrontEnd.Nuxt/README.md) - Frontend patterns
   - Test project READMEs - New test patterns
3. **Script Documentation:** If new scripts were created, update [`scripts/README.md`](../../scripts/README.md)
4. **Architectural Decisions:** If significant design decisions were made, consider creating ADR in [`docs/adr/`](../adr/)
5. **Product Roadmap:** Update [`docs/PRODUCT-ROADMAP.md`](../PRODUCT-ROADMAP.md) to reflect progress

**Reference:** See XML Documentation Comments Pattern in [`.roorules`](../../.roorules)

**Commit:** Present documentation updates to user for review and commit.

---

## [ ] 12. Wrap-up

1. Run full test suite to verify all tests pass: `pwsh -File ./scripts/Run-Tests.ps1`
   - This runs ALL unit and integration tests (excludes functional tests)
   - Fix any failures before proceeding
2. Verify test distribution aligns with strategy:
   - Target: 60-70% Controller Integration, 19-25% Unit, 10-15% Functional
   - Actual distribution will vary by feature type (see TESTING-STRATEGY.md examples)
3. Review any uncompleted acceptance tests, propose how we should cover them
4. Update PRD status:
   - Check off all completed acceptance criteria
   - Mark individual stories as "Implemented" when vast majority of acceptance tests pass
   - When entire PRD complete, update YAML metadata: `status: Implemented`
5. Update [`docs/PRODUCT-ROADMAP.md`](../PRODUCT-ROADMAP.md) with completion status
6. Create final commit with conventional commit message per [`docs/COMMIT-CONVENTIONS.md`](../COMMIT-CONVENTIONS.md)
   - Format: `type(scope): subject` (under 100 words total)
   - Examples: `feat(transactions): add split transaction support`, `fix(auth): resolve token refresh race condition`
7. Provide implementation summary:
   - Features implemented
   - Test coverage metrics (count by layer)
   - Any known limitations or future work
   - Links to PRD and detailed design (if applicable)

**Final Commit:** Present complete implementation summary to user for final review and commit.

---

## Common Issues & Solutions

**Issue: Tests fail after data layer changes**
- Solution: Check EF Core configurations in DbContext
- Verify migration was applied correctly
- Check for tenant isolation violations

**Issue: Functional tests cannot find elements**
- Solution: Review Page Object Model selectors
- Verify frontend is running (either locally, or built and deployed to container)
- Check for timing issues (avoid explicit waits if possible--bias toward DOM elements)

**Issue: API client (apiclient.ts) doesn't include new endpoints**
- Solution: Rebuild WireApiHost project
- Regenerate API client using NSwag
- Verify controller methods have proper attributes ([HttpGet], [ProducesResponseType])

**Issue: Authorization tests fail unexpectedly**
- Solution: Verify [RequireTenantRole] attributes on controller methods
- Check TestAuthenticationHandler configuration
- Ensure tenant context is set correctly in test setup
