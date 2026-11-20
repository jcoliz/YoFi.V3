# Implementation Workflow

**Purpose:** Systematic approach for implementing PRD stories from planning through merge and confirmation.

**Target audience:** Any agent (or human) executing or coordinating implementation work. The primary
responsibility for development remains with the human developer; agents execute the parts they are able
to and report the rest.

**Philosophy:** Incremental progress in phases, with **user-controlled commits after each phase**. Always
run the relevant tests after changes. Never claim a step was done without direct evidence.

---

## Agent Ground Rules

1. **Do what you can, report what you cannot.** If you lack access to Azure DevOps, cannot create a branch,
   or cannot commit, say so and continue with the parts you can perform. Draft the branch name or commit
   message and ask the user to perform the action. Never imply a branch, commit, or work-item change
   happened unless you have direct evidence.
2. **The user commits.** After each phase, present a summary and a drafted commit message (under 100 words,
   following [`docs/COMMIT-CONVENTIONS.md`](../COMMIT-CONVENTIONS.md)), then wait for the user to commit
   before starting the next phase. Do not commit directly.
3. **Read before writing.** Each phase names the documents to read first. Read them. Do not design or
   implement from memory of what the code "probably" looks like; open the neighboring implementation.
4. **Implement only what the current phase and scenario require.** Over-implementation (extra steps, page
   objects, endpoints, DTO fields) causes rework. If something seems missing from the design, raise it;
   do not invent it.
5. **Never stop or tear down the application containers.** Refreshing them is fine; stopping is not.
   See [`AGENTS.md`](../../AGENTS.md).

---

## Phases and Exit Criteria

Feature development goes through these phases. Not every feature touches every layer, but when it does,
this is the order. A phase is not complete until its exit criterion is met and the output is committed.

| # | Phase | Exit criterion |
| --- | --- | --- |
| 1 | Planning | Work item Active, branch created, PRD and Design both `status: Approved`, scope agreed |
| 2 | Entities | Domain model compiles in `src/Entities` |
| 3 | Data & Migrations | DbContext configured, migration generated and reviewed, data integration tests from the design pass |
| 4 | Application Features & Unit Tests | Features implemented, unit tests pass, coverage of modified paths meets threshold |
| 5 | Application Integration Tests | Tests listed in the design pass against the real database |
| 6 | Controllers & API | Endpoints implemented, auth rules validated, controller integration tests pass |
| 7 | Back End Validation | Existing functional tests still pass against the new backend with the unchanged frontend |
| 8 | Front End | TypeScript client regenerated; UI implemented with `data-test-id`s from the design; format, lint, typecheck, and component tests pass |
| 9 | Functional Tests | Each scenario from the design passes individually against the containers |
| 10 | Merge & Confirm | Branch merged to `main`, CI passes, documents and work item updated |

Commit scope conventions: use the PRD feature slug (for example `payee-rules`) as the scope for
non-test commits. Test commits use layer scopes: `test(unit)`, `test(integration)`, `test(functional)`,
`test(frontend)`.

---

## Testing Strategy Summary

See [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) for the full decision framework.

**Primary layer: Application Integration tests.** Most of the application's work is the interaction
between Application Features and the database, so that boundary is tested directly in
`tests/Integration.Application/`.

| Layer | Use when | Location |
| --- | --- | --- |
| Unit | Pure logic with no data involvement: validation, calculations, algorithms, DTO mapping | `tests/Unit/` |
| Application Integration | Feature behavior that depends on real queries: filtering, ordering, tenant isolation, navigation loading, multi-entity operations | `tests/Integration.Application/` |
| Data Integration | As required: new schema, indexes, constraints, conversions, or data-provider capabilities | `tests/Integration.Data/` |
| Controller Integration | As required: new routes, authorization policies, status-code mapping, HTTP-specific behavior | `tests/Integration.Controller/` |
| Frontend Component | Component contracts (props in, events out, API calls), timezone-sensitive logic | `src/FrontEnd.Nuxt/tests/` |
| Functional | Critical user workflows where browser behavior must be verified | `tests/Functional/` |

The design document lists the Data Integration, Application Integration, Component, and Functional tests
to write. Unit and Controller Integration tests are usually self-explanatory from the code and are not
listed in the design.

**Run tests with the project scripts**, never `dotnet test` at the solution root:

```powershell
.\scripts\Run-Tests.ps1                                  # unit + frontend component (default)
.\scripts\Run-Tests.ps1 -Unit -Integration               # unit + all three integration projects
.\scripts\Run-Tests.ps1 -Integration -Filter "FullyQualifiedName~MyFeature"
.\scripts\Run-Tests.ps1 -FrontEnd
.\scripts\Run-Tests.ps1 -Functional -Filter "FullyQualifiedName~ScenarioName"
```

Integration and functional tests require Docker. Integration tests manage their own PostgreSQL
Testcontainers. Functional tests require the application containers to be running.

---

## Reference Implementations

Use these as the pattern for new code. Open and read them; do not rely on memory.

**Backend:**

- Transaction CRUD: [`TransactionsController.cs`](../../src/Controllers/TransactionsController.cs), [`TransactionsFeature.cs`](../../src/Application/Features/TransactionsFeature.cs)
- Tenancy: [`BaseTenantModel.cs`](../../src/Entities/Models/BaseTenantModel.cs)
- Logging: [`docs/LOGGING-POLICY.md`](../LOGGING-POLICY.md)
- Validation: [`docs/VALIDATION-POLICY.md`](../VALIDATION-POLICY.md)

**Testing:**

- Application Integration: [`TransactionsFeatureTests.cs`](../../tests/Integration.Application/TransactionsFeatureTests.cs) and [`tests/Integration.Application/README.md`](../../tests/Integration.Application/README.md)
- Data Integration: [`TransactionTests.cs`](../../tests/Integration.Data/TransactionTests.cs) and [`tests/Integration.Data/README.md`](../../tests/Integration.Data/README.md)
- Controller Integration: [`TransactionsControllerTests.cs`](../../tests/Integration.Controller/TransactionsControllerTests.cs)
- Frontend Component: [`src/FrontEnd.Nuxt/tests/README.md`](../../src/FrontEnd.Nuxt/tests/README.md)
- Functional: [`tests/Functional/README.md`](../../tests/Functional/README.md)

**Frontend:**

- Pages: [`transactions.vue`](../../src/FrontEnd.Nuxt/app/pages/transactions.vue)
- Rules: [`src/FrontEnd.Nuxt/.roorules`](../../src/FrontEnd.Nuxt/.roorules)

---

## Phase 1: Planning

**Read first:** [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md), the ADRs in [`docs/adr`](../adr/README.md),
[`PRD-GUIDANCE.md`](PRD-GUIDANCE.md), [`DESIGN-TEMPLATE.md`](DESIGN-TEMPLATE.md).

### 1.1 Work item and branch

- [ ] Identify the ADO work item (Feature, Story, or Scenario) in the
      [YoFiV3 project](https://dev.azure.com/jcoliz/YoFiV3). Create one if it does not exist.
- [ ] Set the work item to **Active**. If you cannot, ask the user to do it.
- [ ] Create a branch named `{type}/{number}-{slug}`, for example `story/2225-transaction-splits`.
      `{type}` is `feature`, `story`, `scenario`, or `bug`. If you cannot create the branch, draft the
      name and ask the user.

### 1.2 Document conventions

- [ ] Documents are named `{DOC-TYPE}-{SLUG}.md`, for example `PRD-PAYEE-RULES.md`,
      `DESIGN-PAYEE-RULES.md`, and live in `docs/wip/{feature-area}/`.
- [ ] Every document that corresponds to an ADO item records that item in its YAML front matter (`ado:`).
- [ ] When multiple documents exist for one feature, each links to the others in its front matter
      (`prd_document:`, `design_document:`).
- [ ] Metadata goes in YAML front matter only, never in the document body.
- [ ] Check documents in each time they change status. Iterate each document to `Approved` before
      starting the next, and before leaving Planning.

### 1.3 PRD

- [ ] Locate the PRD in `docs/wip/{feature-area}/`. If none exists, create one from
      [`PRD-TEMPLATE.md`](PRD-TEMPLATE.md).
- [ ] Confirm the PRD is `status: Approved`.
- [ ] Agree with the user exactly which stories or scenarios are in scope for this pass. PRDs are often
      implemented in stages.

### 1.4 Design

- [ ] Locate the design document in the same directory as the PRD, or create one from
      [`DESIGN-TEMPLATE.md`](DESIGN-TEMPLATE.md). Follow the template's author instructions exactly:
      every section present, no "work remaining" lists, open questions resolved, Gherkin written,
      `data-test-id` table complete.
- [ ] Confirm the design is `status: Approved` and the PRD links to it.
- [ ] The design is the contract for every phase below. If implementation reveals a gap, update the
      design and get it re-approved rather than improvising.

**No commit of code for this phase.** Commit documents as they change status:
`docs([feature-slug]): add design for [stories]`.

---

## Phase 2: Entities

**Read first:** the Entities section of the design; [`BaseTenantModel.cs`](../../src/Entities/Models/BaseTenantModel.cs)
or [`BaseModel.cs`](../../src/Entities/Models/BaseModel.cs); the XML Documentation pattern in
[`.roorules`](../../.roorules).

- [ ] Implement entity classes in `src/Entities` exactly as specified in the design (names, types,
      nullability, length limits, validation attributes, navigation properties).
- [ ] Add XML documentation to every class and public member.
- [ ] Build: `dotnet build src/Entities`
- [ ] Run unit tests: `.\scripts\Run-Tests.ps1 -Unit`

**Commit:** `feat([feature-slug]): add [EntityName] entity`

Skip this phase if the design states no entity changes.

---

## Phase 3: Data & Migrations

**Read first:** the Data and Data Integration Tests sections of the design;
[`tests/Integration.Data/README.md`](../../tests/Integration.Data/README.md).

### 3.1 Configuration and migration

- [ ] Add `DbSet`s and `OnModelCreating` configuration (indexes, foreign keys, delete behavior,
      conversions) in the PostgreSQL data project under `src/Data/`, exactly as designed.
- [ ] Add or change `IDataProvider` capabilities only if the design calls for them.
- [ ] Build: `dotnet build`
- [ ] Create the migration: `.\scripts\Add-Migration.ps1 -Name "<MigrationNameFromDesign>"`
- [ ] Review the generated migration: confirm it matches the intended schema change and nothing else.

### 3.2 Data integration tests

- [ ] Implement every test listed in the design's Data Integration Tests section, in
      `tests/Integration.Data/`, using Given/When/Then comments.
- [ ] Run: `.\scripts\Run-Tests.ps1 -Integration -Filter "FullyQualifiedName~<Fixture>"`
- [ ] Iterate until all pass.

**Commits:** `feat([feature-slug]): add [EntityName] EF Core configuration` then
`test(integration): add [EntityName] data tests`

Skip this phase if the design states no data changes.

---

## Phase 4: Application Features & Unit Tests

**Read first:** the Application Features section of the design;
[`TransactionsFeature.cs`](../../src/Application/Features/TransactionsFeature.cs);
[`docs/VALIDATION-POLICY.md`](../VALIDATION-POLICY.md); the Application Layer Change pattern in
[`.roorules`](../../.roorules).

- [ ] Add DTOs, validators, and exceptions in `src/Application` with the exact names and signatures
      from the design.
- [ ] Implement Feature methods. Use `IReadOnlyCollection<T>` for collections. Apply tenant scoping to
      every query. No logging in this layer.
- [ ] Add XML documentation to every class and public member.
- [ ] Build: `dotnet build src/Backend`
- [ ] Write unit tests in `tests/Unit/` covering normal, boundary, and error paths for logic that does
      not require a database. Update existing unit tests affected by signature changes.
- [ ] Run: `.\scripts\Run-Tests.ps1 -Unit`; iterate until all pass.

**Commits:** `feat([feature-slug]): implement application layer`. If unit tests are written separately, they can be committed with
`test(unit): add [feature] validation and logic tests`. If they're all together, include them with the `feat` commit.

> ![NOTE]
> Previous iterations of this guide directed developers to run the `.\scripts\Collect-CodeCoverage.ps1` and confirm the modified code paths are covered.
> However, this script takes too long given how long it takes the integration tests to execute, and the sporadic
> nature of our coverage, we will skip this step until we can re-think code coverage.

---

## Phase 5: Application Integration Tests

**Read first:** the Application Integration Tests section of the design;
[`tests/Integration.Application/README.md`](../../tests/Integration.Application/README.md).

This is the primary test layer.

- [ ] Implement every test listed in the design, in `tests/Integration.Application/`, extending the
      fixture the design names. Use `FeatureTestBase` and Given/When/Then comments.
- [ ] Cover tenant isolation for every query the feature adds.
- [ ] Run: `.\scripts\Run-Tests.ps1 -Application -Filter "FullyQualifiedName~<Fixture>"`
- [ ] Iterate until all pass, then run the whole project once: `.\scripts\Run-Tests.ps1 -Application`

**Commit:** `tests(integration): add [feature] application tests`

**Checkpoint:** core business logic is verified against the database before any HTTP or UI work.

---

## Phase 6: Controllers

**Read first:** the Controllers section of the design;
[`TransactionsController.cs`](../../src/Controllers/TransactionsController.cs);
[`docs/LOGGING-POLICY.md`](../LOGGING-POLICY.md).

Do **not** regenerate the TypeScript API client in this phase. Regenerating it changes
[`apiclient.ts`](../../src/FrontEnd.Nuxt/app/utils/apiclient.ts), which is frontend code and may require
frontend changes to compile. That happens in Phase 8, after Phase 7 has proven the new backend works with
the unchanged frontend.

### 6.1 Endpoints

- [ ] Implement each endpoint with the route, tenant role, request/response types, and
      `[ProducesResponseType]` attributes from the design. Keep controllers thin: delegate to the Feature.
- [ ] Add `[LoggerMessage]` methods with explicit sequential event IDs and `[CallerMemberName]`.
- [ ] Add middleware, filters, authorization policies, or exception-to-status mappings if the design
      lists them.
- [ ] Add XML documentation to the controller and every action.
- [ ] Preserve backward compatibility with the currently deployed frontend. Any breaking change must
      already be itemized in the design.
- [ ] Build: `dotnet build src/Backend`

### 6.2 Controller integration tests

- [ ] Add tests in `tests/Integration.Controller/` for each new endpoint: success, 400 validation,
      404 not-in-tenant, unauthorized, forbidden role, and tenant isolation.
- [ ] Run: `.\scripts\Run-Tests.ps1 -Controller -Filter "FullyQualifiedName~<Fixture>"`
- [ ] Iterate until all pass.

**Commits:** `feat([feature-slug]): add API endpoints` then
`test(integration): add [feature] API endpoint tests`

---

## Phase 7: Back End Validation

The backend deploys before the frontend, so the previous frontend must keep working against the new
backend. Verify this before starting UI work. At this point the frontend, including `apiclient.ts`, is
unchanged from `main`, which is exactly the production condition being simulated.

- [ ] Rebuild and start the containers: `.\scripts\Refresh-Container.ps1`
- [ ] Run the existing functional suite: `.\scripts\Run-Tests.ps1 -Functional`
- [ ] If anything fails, fix the backend (not the tests) until the existing suite is green.
- [ ] Do not stop the containers.

**Commit (only if fixes were needed):** `fix([feature-slug]): preserve compatibility with current frontend`

---

## Phase 8: Front End

**Read first:** the Front End and Component Tests sections of the design;
[`src/FrontEnd.Nuxt/.roorules`](../../src/FrontEnd.Nuxt/.roorules);
[`tests/Functional/docs/frontend-contract.md`](../../tests/Functional/docs/frontend-contract.md);
[`src/FrontEnd.Nuxt/tests/README.md`](../../src/FrontEnd.Nuxt/tests/README.md); the API Client
Generation pattern in [`.roorules`](../../.roorules).

### 8.1 API client

Run from the repository root.

- [ ] Regenerate the TypeScript client: `pwsh -File ./scripts/Generate-ApiClient.ps1`
- [ ] Verify the new methods and DTOs named in the design's Controllers section appear in
      [`apiclient.ts`](../../src/FrontEnd.Nuxt/app/utils/apiclient.ts). Never hand-edit this file.
- [ ] Confirm the only generated-file change in `git diff` is `apiclient.ts`.
- [ ] From `src/FrontEnd.Nuxt/`, run `pnpm typecheck`. Fix any existing frontend code broken by the
      regenerated client (renamed methods, changed DTO shapes) before adding new UI.
- [ ] The same script also refreshes `tests/Functional/Infrastructure/TestControl/openapi-test-control.json`;
      the C# Test Control client regenerates from it when `tests/Functional` builds.

**Commit:** `build([feature-slug]): regenerate API client`

Run all remaining commands in this phase from `src/FrontEnd.Nuxt/`.

### 8.2 Front End Implementation

- [ ] Implement the pages, components, composables, stores, and utils named in the design, with the
      documented props and emits.
- [ ] Prefer small components. Extract discrete sections into components even when used once.
- [ ] Apply **every** `data-test-id` from the design's Test IDs table, exactly as named. Add
      `data-test-id` to every interactive element and every element whose content changes materially
      from user action. Follow the frontend contract (naming, uniqueness, page-root-id, readiness).
- [ ] Handle loading, empty, success, and each error status the design lists.
- [ ] Add JSDoc to new or changed files.

### 8.3 Component tests

- [ ] Implement every test listed in the design's Component Tests section in `src/FrontEnd.Nuxt/tests/`.
      Test behavior contracts, not markup.

### 8.4 Checks

- [ ] `pnpm run format`
- [ ] `pnpm run lint`
- [ ] `pnpm typecheck`
- [ ] `pnpm test:components`
- [ ] From the repository root, rebuild and start the containers: `.\scripts\Refresh-Container.ps1`.
      The container build runs `pnpm generate`; it must complete without errors. This also leaves the
      containers ready for Phase 9.

**Commits:** `feat([feature-slug]): implement UI` then `test(frontend): add [feature] component tests`

---

## Phase 9: Functional Tests

**Read first, before any functional test work:** [`tests/Functional/README.md`](../../tests/Functional/README.md)
and every guide it links, especially
[`WRITING-TESTS.md`](../../tests/Functional/docs/WRITING-TESTS.md),
[`writing-features.md`](../../tests/Functional/docs/writing-features.md),
[`writing-steps.md`](../../tests/Functional/docs/writing-steps.md),
[`writing-pages.md`](../../tests/Functional/docs/writing-pages.md), and
[`TEST-CONTROLS.md`](../../tests/Functional/docs/TEST-CONTROLS.md).
Also read the Functional Tests section of the design, which contains the Gherkin scenarios to implement
in priority order.

### 9.1 Environment

- [ ] Confirm the containers are running the current frontend (Phase 8.4 refreshed them). If anything
      has changed since, run `.\scripts\Refresh-Container.ps1` again. Agents may run this at any time
      without asking; run it after every backend or frontend change.
- [ ] Run the existing functional suite once: `.\scripts\Run-Tests.ps1 -Functional`. Fix any regression
      caused by the new frontend before adding scenarios.
- [ ] Never run `Stop-Container.ps1` or a compose-down command.

### 9.2 One scenario at a time

Work through the design's scenarios in priority order. For each scenario, implement, pass, present a
commit, and wait for the user before starting the next one.

- [ ] Copy the scenario's Gherkin from the design into the appropriate file in `tests/Functional/Features/`.
      Keep all `When` steps before `Then` steps. Reuse step wording from the Step Catalog
      (`tests/Functional/bin/Debug/net10.0/YoFi.V3.Tests.Functional.StepCatalog.md`).
- [ ] Build the test project: `dotnet build tests/Functional`. Watch the output for warnings about
      unbound steps; those are the step definitions you need.
- [ ] Implement **only** the step definitions, page/component object members, and Test Control
      endpoints this scenario needs. Do not pre-build steps or page objects for later scenarios.
- [ ] If a Test Control endpoint was added to
      [`TestControlController.cs`](../../src/Controllers/TestControlController.cs):
      1. From the repository root, run `pwsh -File ./scripts/Generate-ApiClient.ps1`. This refreshes
         `tests/Functional/Infrastructure/TestControl/openapi-test-control.json` (and `apiclient.ts`).
      2. Rebuild `tests/Functional`; the MSBuild target regenerates the C# Test Control client from that
         spec.
      3. Refresh the containers so the backend serves the new endpoint: `.\scripts\Refresh-Container.ps1`
- [ ] Run only this scenario:
      `.\scripts\Run-Tests.ps1 -Functional -Filter "FullyQualifiedName~<GeneratedScenarioName>"`
      The generated name is in `tests/Functional/obj/GeneratedFiles/.../<Feature>.feature.g.cs`.
- [ ] Iterate until this scenario passes. It is not necessary to re-run the full fixture or suite after
      every scenario.
- [ ] Present the commit message and wait for the user.

**Commit per scenario:**

```text
test(functional): implement [feature] - [scenario name]

Implements: "[Full scenario name from feature file]"
- Pages: [changes]
- Steps: [new methods]
- Test Control: [additions, if any]

Status: [X of Y scenarios complete]
```

### 9.3 Full suite

After the last scenario:

- [ ] Run the complete functional suite against the containers: `.\scripts\Run-Tests.ps1 -Functional`
- [ ] Fix any failures. Do not proceed to merge with a red suite.

---

## Phase 10: Merge & Confirm

### 10.1 Documentation

- [ ] Verify XML docs exist on every new public class and member (build with 0 documentation warnings).
- [ ] Update README files if a new pattern was introduced (frontend, test projects, scripts).
- [ ] Add an ADR in [`docs/adr`](../adr/README.md) if the design introduced a significant architectural
      decision.
- [ ] Run the full non-functional suite once more: `.\scripts\Run-Tests.ps1 -Unit -Integration -FrontEnd`

**Commit:** `docs([feature-slug]): update documentation`

### 10.2 Merge

The agent typically cannot perform these; draft the commands and ask the user.

- [ ] Rebase the feature branch onto `main`.
- [ ] Merge into `main` with `--ff-only`.
- [ ] Push.
- [ ] Confirm the Azure Pipelines CI run ([`.azure/pipelines/ci.yaml`](../../.azure/pipelines/ci.yaml))
      succeeds. Iterate until it does.

### 10.3 Status updates

Document lifecycle: `Draft -> In Review -> Approved -> Implemented`.
ADO lifecycle: `New -> Active -> Resolved -> Closed`.

Once CI is green on `main`:

- [ ] PRD: check off the acceptance criteria that were implemented. If every story in the PRD is done,
      set `status: Implemented`; otherwise leave it `Approved` and note which stories remain.
- [ ] Design: set `status: Implemented`.
- [ ] Update [`docs/PRODUCT-ROADMAP.md`](../PRODUCT-ROADMAP.md).
- [ ] Set the ADO work item to **Resolved**. If you cannot, ask the user.

**Commit:** `docs([feature-slug]): mark [stories] implemented`

### 10.4 Validation

Performed by a human after deployment to a cloud environment (UAT or Release). Features may be
validated individually or batched per deployment.

- [ ] Manually validate the feature in the deployed environment.
- [ ] Set the ADO work item to **Closed**.
- [ ] Documents stay `Implemented`; there is no "Closed" document status.

---

## Common Issues

**Tests fail after data layer changes:** check `OnModelCreating` configuration, confirm the migration was
generated and applied, look for tenant isolation violations.

**Functional tests cannot find elements:** confirm the containers were refreshed after the last frontend
change; compare page-object selectors with the design's Test IDs table; use deterministic waits, never
fixed delays.

**Gherkin steps report as unbound at build time:** the step wording does not match any definition. Check
the Step Catalog for the existing phrase, or implement the definition.

**API client missing new endpoints:** rebuild `src/WireApiHost` or rerun `Generate-ApiClient.ps1`; verify
`[HttpGet]`/`[HttpPost]` and `[ProducesResponseType]` attributes on the action.

**Authorization tests fail:** verify `[RequireTenantRole]` on the action, check the test authentication
handler configuration, ensure tenant context is set in test setup.

**Functional tests fail after backend-only changes (Phase 7):** this is the compatibility check working.
Fix the backend to keep the deployed frontend working; do not change the tests.
