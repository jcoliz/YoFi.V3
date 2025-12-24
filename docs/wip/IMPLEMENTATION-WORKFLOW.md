# Implementation Workflow

Here's how I want to approach implementing each PRD. I will stop to commit after each step!

## [ ] 1. Establish Scope & Entry Readiness

1. Ensure clarity on which exact PRD we are working from. These are located in `docs/wip/{feature-area}`.
2. Ensure PRD exists, otherwise abort and advise to create PRD using [`docs/wip/PRD-TEMPLATE.md`](docs/wip/PRD-TEMPLATE.md)
3. Ensure PRD has YAML metadata with `state` containing `Approved`
4. Check if PRD already links to a detailed design document (see PRD "Related Documents" section)
5. Ensure clarity on which story or stories from within that PRD are in scope
6. Ensure clarity on whether we are implementing the entire identified stories, or just some part--and if just some part, which part?

## [ ] 2. Detailed Design

1. Determine whether there is already a detailed design document. If not, create one scoped to only the stories, story, or part of story that's in scope. The file should go in the same directory where the PRD is located.
2. Ensure clarity on which file contains the detailed design.
3. Ensure detailed design has YAML metadata with `state` containing `Approved`. If not, work with user to review as needed and approve.
4. Ensure PRD has a link to the detailed design document. If not, add a link in the PRD

## [ ] 3. Entities

1. Implement Entities, as designed

## [ ] 4. Data Layer

1. Add new entities to application db context
2. Add model creating (configurations for EF Core)
3. Add indices, as needed
4. Build and test (verify no compilation errors)
5. Create migration using `.\scripts\Create-MigrationScript.ps1 -MigrationName "AddFeatureName"`
6. Review generated migration for correctness
7. Run data integration tests to verify schema changes

## [ ] 5. Data Integration Tests

1. Review [`docs/TESTING-STRATEGY.md`](docs/TESTING-STRATEGY.md) to determine which acceptance criteria are best suited for data integration tests
2. Create Data Integration tests covering the new data layer changes (EF configurations, relationships, queries)
3. Implement chosen Data Integration tests for relevant acceptance criteria
4. Run tests using `dotnet test tests/Integration.Data`
5. Iterate until all tests pass
6. Check off any completed acceptance criteria in PRD

## [ ] 7. Unit Tests

1. Review [`docs/TESTING-STRATEGY.md`](docs/TESTING-STRATEGY.md) Decision Framework to determine which acceptance criteria require unit tests
2. Focus on: Business logic, validation rules, calculations, algorithms, DTO transformations
3. Implement Unit tests with comprehensive coverage (target: 60% of total tests)
4. Run tests using `dotnet test tests/Unit`
5. Iterate until all tests pass
6. Check off any completed acceptance tests in PRD

## [ ] 9. Controller Integration Tests

1. Review [`docs/TESTING-STRATEGY.md`](docs/TESTING-STRATEGY.md) - Controller Integration is the PRIMARY test layer (60-70% of acceptance criteria)
2. Focus on: API contracts, authorization, HTTP status codes, request/response formats, error handling
3. Implement Controller Integration Tests for all API endpoints with authorization variants
4. Run tests using `dotnet test tests/Integration.Controller`
5. Iterate until all tests pass
6. Check off any completed acceptance tests in PRD

1. Create Data Integration tests covering the new data layer changes
2. Review test strategy and PRD to determine if any acceptance criteria should be implemented as data integration tests
3. Create Data Integration tests covering any chosen acceptance criteria
4. Check off any completed acceptance tests in PRD.

## [ ] 6. Application Layer

1. Implement Application Layer

## [ ] 7. Unit Tests

1. Review test strategy and PRD to determine which acceptance criteria are best suited for unit tests
2. Implement chosen Unit tests
3. Check off any completed acceptance tests in PRD.

## [ ] 8. Controllers

1. Implement Controller
2. Implement any ASP.NET supporting components, such as middleware

## [ ] 9. Controller Integration Tests

1. Review test strategy and PRD to determine which acceptance criteria are best suited for controller integration tests
2. Implement chosen Controller Integration Tests
3. Check off any completed acceptance tests in PRD.

## [ ] 10. Front End

1. Implement UI

## [ ] 11. Functional Tests

1.  Identify critical workflows worthy of functional tests, as described in test strategy.
2.  Write complete Gherkin feature in functional tests covering those. One `.feature` file usually maps to one PRD.
3.  Ask user to keep backend project running, so you can actually execute individual functional tests (this contravenes project rules, but we can do it here when we are bringing up functional tests)
4.  Implement one scenario at a time
    1.  Create the test implementation
    2.  Write the step definitions.
    3.  Test and iterate until green.
5.  Check off any completed acceptance tests in PRD.

## [ ] 12. Wrap-up

1.  Review any uncompleted acceptance tests, propose how we should cover them.
2.  Mark Individual stories as "Implemented" when vast majority of acceptance tests pass.
3.  When entire PRD complete, mark it "Implemented".
4.  Update product roadmap.
