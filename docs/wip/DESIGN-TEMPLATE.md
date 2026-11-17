---
status: Draft # Draft | In Review | Approved | Implemented
target_release: [Release Milestone when predominance of work is expected]
prd_document: [Link to PRD document]
parent: [Link to parent ADO Item]
ado: [Link to ADO Item]
---

# Technical Design Document: {Work Item Name}

## Scope

> Describe the scope of the change. We design features at different levels in the work item
> parentage hierarchy. Sometimes we design the entire feature, more often one or more user stories together,
> or sometimes one or more scenarios within a user story.

## Architectural Considerations

> Describe any new or novel architectural concepts we are introducing in this design.

## Entities

> Describe changes to the Entities project, or state that no entity changes are required.

## Data

> Describe changes to the Data layer, especially migrations or any new or updated data provider capabilities

## Application Features

> Describe changes to the Application layer, especially new or updated Feature classes, new or updated DTOs,
> validators, or exceptions. Include the information contained in the XMLdoc for the public interface of each.
>
> Unit tests are not required to be included. These are usually self-explanatory

## Application Integration Tests

> List the tests which must be added to test the boundary between Application Layer and Data layer
>
> Include the name of the test and its Summary XMLdoc tag

## Controllers

> List the endpoints, and their Summary XMLdoc tag
>
> Include any other new or updated Controller project components, such as middleware. Specify the information contained in the
> XMLdoc for the public interface of each.
>
> Controller integration tests are not required to be included. These are usually self-explanatory.
>
> ![IMPORTANT]
> Changes to controller project must support front-ends which do not yet have the feature implemented.
> If breaking changes are desired, describe them individually, so we can go back later and clean them
> up, once the front-end changes are fully rolled out to production.

## Front End

> Describe new or updated .vue files. Provide the JSDoc comments for any new or updated pages, components, composables,
> utils, plugins, stores or middleware. Include props and emits for components.
>
> Bias toward encapsulating functionality into components, even if not strictly required for reusability.

## Component Tests

> List each new or updated test in src\FrontEnd.Nuxt\tests. Provide the file and the `it` e.g. "requires a new preview after the import endpoint rejects the workbook"

## Functional Tests

> Within the design document, write the gherkin functional tests. Refer to the
> [Step Catalog](tests\Functional\bin\Debug\net10.0\YoFi.V3.Tests.Functional.StepCatalog.md)
> to re-use as many steps as possible and match the working/tone.
>
> Limit functional tests to 5 gherkin scenarios per user story, or one gherkin scenario per
> ADO scenario if we are implementing at that level.
>
> List the tests in priority order, so if we only implement a subset, we get the most important
> ones first.
