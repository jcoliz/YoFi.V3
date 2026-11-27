---
status: Draft # Draft | In Review | Approved | Implemented
target_release: [Release Milestone when predominance of work is expected]
prd_document: [Link to PRD document]
parent: [Link to parent ADO Item]
ado: [Link to ADO Item]
---

# Technical Design Document: {Work Item Name}

> **Instructions for the author (human or agent).** Delete this block before setting
> status to `In Review`.
>
> This document defines **HOW** we build what the PRD says we will build. The PRD owns
> WHAT and WHY; do not restate requirements here except to clarify scope.
>
> **Before writing anything:**
>
> 1. Read the PRD in full, including acceptance criteria for every story in scope.
> 2. Read [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md), the ADRs in [`docs/adr`](../adr/README.md),
>    and the project rules in [`.roorules`](../../.roorules) and [`AGENTS.md`](../../AGENTS.md).
> 3. Open the actual source files for the closest existing feature and use them as the pattern.
>    Name them in the sections below. Do not design from memory of what the code "probably" looks like.
> 4. If the PRD leaves a decision open, **make the decision here** and record it under
>    [Design Decisions](#design-decisions). Do not defer it.
>
> **Rules for every section:**
>
> - Every section must be present. If a layer is untouched, write one sentence saying so and why
>   (for example, "No entity changes: the feature reads existing `Transaction` rows only.").
> - Specify public interfaces precisely: names, signatures, routes, DTO shapes, props, emits.
>   Implementers must not have to invent names.
> - Do **not** write a "work remaining", "to be finalized", or "TBD" list. Anything you would put
>   there belongs either as a decision in this document or as an entry in
>   [Open Questions](#open-questions) with a proposed answer. A design with open questions cannot be
>   `Approved`.
> - Link to existing files using relative paths. Verify the file exists.
> - Prefer prose and tables over large code blocks. Include code only where the exact shape matters
>   (DTOs, signatures, Gherkin, data-test-id names).
> - Do not include level-of-effort estimates.

## Scope

> Describe the scope of the change. We design features at different levels in the work item
> parentage hierarchy. Sometimes we design the entire feature, more often one or more user stories together,
> or sometimes one or more scenarios within a user story.
>
> State explicitly:
>
> - Which PRD stories and/or ADO scenarios this design covers, by number and title.
> - Which stories/scenarios from the same PRD are **excluded**, and whether this design
>   must leave room for them (for example, a column added now that a later story will use).
> - Any acceptance criteria from the PRD that this design intentionally does not satisfy, and why.

## Architectural Considerations

> Describe any new or novel architectural concepts we are introducing in this design.
>
> Cover, where applicable:
>
> - New cross-cutting components (middleware, filters, authorization policies, background work, caches).
> - New third-party dependencies and why an existing one does not suffice.
> - Anything that departs from an ADR, with a note that a new ADR is required.
> - Performance, concurrency, or data-volume concerns and how the design addresses them.
>
> If nothing new is introduced, say so in one sentence and name the existing pattern being followed.

## Design Decisions

> Record each non-obvious decision made in this design: what was decided, the alternatives considered,
> and why this one won. Include decisions that resolve PRD open questions. Keep entries short.
>
> | Decision | Alternatives considered | Rationale |
> | -------- | ----------------------- | --------- |
>
> This section replaces any temptation to leave decisions for implementation. If a decision
> genuinely cannot be made yet, it goes under [Open Questions](#open-questions).

## Entities

> Describe changes to the Entities project, or state that no entity changes are required.
>
> For each new or modified entity: name, base class (`BaseTenantModel` or `BaseModel`), each
> property with type, nullability, length limits and validation attributes, navigation properties,
> and the summary XMLdoc for the class. Note any invariants the entity must uphold.
> Follow the pattern of a named existing entity.

## Data

> Describe changes to the Data layer, especially migrations or any new or updated data provider capabilities.
>
> Include:
>
> - `DbContext` changes: new `DbSet`s, `OnModelCreating` configuration, indexes (with the query that
>   justifies each), foreign keys and delete behavior, value conversions.
> - Migration name(s). Note any provider-specific (SQLite vs PostgreSQL) considerations.
> - New or changed `IDataProvider` capabilities, with the reason the existing surface is insufficient.
> - Data integrity rules the schema must enforce versus rules enforced in Application code.

## Data Integration Tests

> List the tests which must be added to verify the schema and EF Core configuration against real
> PostgreSQL. These live in `tests/Integration.Data/` and run against a Testcontainers database with
> migrations applied; see [`tests/Integration.Data/README.md`](../../tests/Integration.Data/README.md).
> These tests are rare, so they must be designed explicitly rather than left to the implementer.
>
> Include the name of the test and its Summary XMLdoc tag. Name the existing fixture to extend
> (for example [`TransactionTests.cs`](../../tests/Integration.Data/TransactionTests.cs)) or the new
> fixture to create, which must derive from `PostgreSqlTestBase`.
>
> Cover each of the following that applies to the [Data](#data) changes:
>
> - **Schema**: the migration creates the expected table, columns, types, nullability, and length limits
>   (query PostgreSQL catalogs; do not assert on the migration file).
> - **Indexes and uniqueness**: each index declared in the Data section exists; unique constraints reject
>   duplicates with the expected exception.
> - **Foreign keys and delete behavior**: cascade or restrict behaves as configured when the parent is deleted.
> - **Value conversions and precision**: `DateTimeOffset`, `DateOnly`, `decimal` currency, enums, and
>   JSON columns round-trip without loss; timestamps keep the intended offset semantics.
> - **Tenant isolation**: queries scoped by `TenantId` do not return another tenant's rows.
> - **Defaults and computed values**: database defaults and generated keys populate as designed.
> - **New `IDataProvider` capabilities**: each new or changed provider method executes against the real
>   provider and translates to SQL (no client-side evaluation).
>
> Do not test Application business rules here; those belong in unit or Application integration tests.
> If there are no Data layer changes, state that in one sentence and leave the list empty.

## Application Features

> Describe changes to the Application layer, especially new or updated Feature classes, new or updated DTOs,
> validators, or exceptions. Include the information contained in the XMLdoc for the public interface of each.
>
> For each public Feature method: signature, summary, parameters, return type, and every exception it
> throws and under what condition. Collections use `IReadOnlyCollection<T>`.
>
> For each DTO: full record signature and summary. Distinguish edit (input) DTOs from result (output) DTOs.
>
> For each validator: the rules, in order, with the user-facing error message where it matters.
>
> For each new exception: name, base class, and which HTTP status the exception-handling middleware
> maps it to (add the mapping in [Controllers](#controllers) if it is new).
>
> State where business rules from the PRD are enforced (validator, feature method, or database), and
> how tenant scoping is applied to every query.
>
> Unit tests are not required to be included. These are usually self-explanatory.

## Application Integration Tests

> Identify the data contracts that this design introduces or changes at the boundary between the
> Application and Data layers. These live in `tests/Integration.Application/`.
>
> First inventory every Application method that calls `IDataProvider`. Then group those calls into
> distinct data contracts rather than proposing one integration test per call or per line of code.
> A contract is a meaningful provider behavior whose correctness depends on real EF Core or
> PostgreSQL semantics, such as:
>
> - A query shape that requires provider-specific LINQ translation
> - Tenant-scoped filtering or isolation
> - Navigation property loading or tracking behavior
> - Ordering, pagination, filtering, grouping, or boundary dates
> - Persistence of an entity graph or a multi-entity operation
> - Bulk insert, update, or delete behavior
> - Transaction semantics, cancellation, rollback, concurrency, or failure injection
> - Database constraints or cascade behavior
>
> For each contract, specify one representative happy-path test and, only where meaningful, one
> focused failure or rollback test. Different Application branches that use the same provider contract
> should normally be covered through unit tests, not additional database tests.
>
> Keep pure business logic, validation, mapping, cache behavior, DTO construction, and provider
> orchestration in unit tests. Use Moq or narrow scenario-specific stubs for `IDataProvider`; execute
> collection behavior with LINQ-to-Objects only when appropriate. LINQ-to-Objects success is not
> evidence of EF Core translation or PostgreSQL behavior.
>
> For each proposed integration test include:
>
> - The test name and its Summary XMLdoc tag
> - The data contract it verifies
> - The `IDataProvider` operations or entity graph involved
> - The provider behavior that cannot be proven with unit tests
> - The existing fixture to extend, or the new fixture to create
>
> Do not add an Application Integration test merely because Application code calls `IDataProvider`.

## Controllers

> List the endpoints, and their Summary XMLdoc tag.
>
> For each endpoint provide a row in this table, then the summary XMLdoc below it:
>
> | Method | Route | Tenant role | Request body | Success response | Error responses |
> | ------ | ----- | ----------- | ------------ | ---------------- | --------------- |
>
> - Routes are full paths, for example `POST /api/tenant/{tenantKey:guid}/transactions/{key:guid}/splits`.
> - Error responses list each status code and the condition that produces it (400 validation,
>   404 not found in tenant, 409 conflict, and so on). All errors are `ProblemDetails`.
> - Note the `[LoggerMessage]` events added, following [`docs/LOGGING-POLICY.md`](../LOGGING-POLICY.md).
>   Controllers log; Application and Data do not.
>
> Include any other new or updated Controller project components, such as middleware, filters,
> authorization policies, or exception-to-status mappings. Specify the information contained in the
> XMLdoc for the public interface of each.
>
> State that the TypeScript client will be regenerated with
> [`Generate-ApiClient.ps1`](../../scripts/Generate-ApiClient.ps1) and name the new client methods
> the frontend will call.
>
> Controller integration tests are not required to be included. These are usually self-explanatory.
>
> > [!IMPORTANT]
> > The backend is deployed before the frontend. Changes to the Controller project must support front-ends
> > which do not yet have the feature implemented. New fields must be optional or defaulted; existing
> > routes, status codes, and response shapes must keep working for the previous frontend.
> >
> > If breaking changes are desired, describe them individually under a **Breaking changes** heading
> > here, so we can go back later and clean them up once the front-end changes are fully rolled out
> > to production.

## Front End

> Describe new or updated .vue files. Provide the JSDoc comments for any new or updated pages, components, composables,
> utils, plugins, stores or middleware. Include props and emits for components.
>
> Bias toward encapsulating functionality into components, even if not strictly required for reusability.
>
> For each file: path and JSDoc summary. For components, also provide the Nuxt auto-import name
> derived from the full path below `app/components`, plus `props` and `emits` with types.
> For example, `app/components/transaction/PayeeRulesAction.vue` is auto-imported as
> `TransactionPayeeRulesAction`.
> Describe the user-visible behavior for loading, empty, success, and error states, and what the
> user sees when the API returns each error status listed in [Controllers](#controllers).
> Identify which generated API client methods are called, and any store or localStorage keys read or written.
>
> Follow [`src/FrontEnd.Nuxt/.roorules`](../../src/FrontEnd.Nuxt/.roorules).
>
> ### Test IDs
>
> List every `data-test-id` the feature adds or relies on, in a table:
>
> | data-test-id | Element | Purpose |
> | ------------ | ------- | ------- |
>
> Include every element a user interacts with and every element whose content changes materially as a
> result of user action. Functional tests will be written against these names; the implementer must
> use them exactly.
>
> ![IMPORTANT]
> data-test-ids are not required to be unique on a page. They are only required to be unique amongst their
> siblings. When constructing Playwright locators in the functional tests, we will use a chain of
> data-test-id's (or IDs or element tags, as appropriate.)

## Component Tests

> List each new or updated test in `src/FrontEnd.Nuxt/tests`. Provide the file and the `it` e.g.
> "requires a new preview after the import endpoint rejects the workbook".
>
> Follow the philosophy in [`src/FrontEnd.Nuxt/tests/README.md`](../../src/FrontEnd.Nuxt/tests/README.md):
> test behavior contracts (props in, events out, API calls made, error paths) and timezone-sensitive
> logic, not markup.

## Functional Tests

> Within the design document, write the gherkin functional tests. Refer to the
> Step Catalog at `tests/Functional/bin/Debug/net10.0/YoFi.V3.Tests.Functional.StepCatalog.md`
> to re-use as many steps as possible and match the wording/tone. The catalog is generated when the
> functional test assembly loads; if it is missing, build `tests/Functional` first.
>
> Limit functional tests to 5 gherkin scenarios per user story, or one gherkin scenario per
> ADO scenario if we are implementing at that level.
>
> List the tests in priority order, so if we only implement a subset, we get the most important
> ones first.
>
> Rules:
>
> - Put all `When` steps before any `Then` step. Never add a `When` after a `Then`.
> - Each scenario verifies one workflow or acceptance criterion.
> - Use the `data-test-id` names from the [Test IDs](#test-ids) table; do not invent new ones here.
> - Mark steps that do not exist in the catalog with a trailing `# NEW` comment so the implementer
>   knows which step definitions must be written.
> - Gherkin only. No C#, page objects, or step definitions in this document.
> - Before writing new functional tests, read [`tests/Functional/README.md`](../../tests/Functional/README.md).

## Open Questions

> Questions that block approval, each with a proposed answer and who must decide. This list must be
> empty before status moves to `Approved`. Resolved questions move to [Design Decisions](#design-decisions).
>
> - [ ] [Question] — Proposed: [answer]. Decider: [who].

## Completion Checklist

> The author verifies every item before moving status to `In Review`. Reviewers verify before `Approved`.
>
> - [ ] Every PRD acceptance criterion in scope is traceable to a section of this design.
> - [ ] Every section is present; untouched layers say so explicitly.
> - [ ] Every endpoint lists its tenant role, request/response types, and error statuses.
> - [ ] Every new exception has a status-code mapping.
> - [ ] Every index, constraint, conversion, and new data-provider capability in Data has a Data Integration Test.
> - [ ] Backward compatibility with the previous frontend is stated; breaking changes are itemized.
> - [ ] Every `data-test-id` used in Gherkin appears in the Test IDs table.
> - [ ] Gherkin scenarios have all `When` steps before `Then` steps and reuse catalog steps where possible.
> - [ ] No "TBD", "to be finalized", or "work remaining" text appears anywhere.
> - [ ] Open Questions is empty, or every entry has a proposed answer.
> - [ ] All relative links resolve to existing files.
> - [ ] The author instruction block at the top has been deleted.
