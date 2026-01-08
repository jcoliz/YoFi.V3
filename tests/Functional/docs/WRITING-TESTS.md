# Handbook for writing YoFi.V3 functional test

## Write test-ready code

When creating front-end code, be sure to:
- [_] Add `data-test-id` annotations to controls user will interact with, or use to check status
- [_] Ensure one control per page is disabled until the page is ready, see [NUXT-SSR-TESTING-PATTERN](NUXT-SSR-TESTING-PATTERN.md)

## Plan good tests

- [_] Review the testing strategy to ensure we're creating the right kinds of tests (TODO: link)
- [_] Review existing test steps in Steps directory. Try to use existing steps where possible
- [_] Always limit to a single when/then group (no `when` following `then`)
- [_] Always prioritize test scenarios precisely, so we can be sure to focus on the most important first

## Implement Incrementally

- [_] Always implement one scenario at a time. From writing the feature file, through implementing steps and page object models, to iterating the test to passing, through to checking in the code. One at a time!

## Write tests

- [_] Write a scenario into the apprrpriate Gherkin feature file in `Features` folder. Try to match the PRD for the name, e.g. `PRD-BANK-IMPORT` -> `BankImport.feature`

## Generate implementations

NOTE: The project uses [Gherkin.Generator](https://github.com/jcoliz/Gherkin.Generator) to generate C# test implementations, based on Gherkin feature files and C# step definitions.

- [_] Build the project `dotnet build` to generate implementation files from the Gherkin features. You can find these in `obj\GeneratedFiles\Gherkin.Generator\Gherkin.Generator.GherkinSourceGenerator`.
- [_] Notice that unimplemented steps will produce stubs in the {feature}.g.cs file

## Create step definitions

- [_] Copy over stubs from generated files into well-organized step definition classes
- [_] Create new step definitions in either an existing or new class in `Steps`
- [_] Be sure to add the correct and precise keyword attributes, e.g. `[Given("I am logged in")]`.

## Implement the step definitions

- [_] Create new page (or component) object models and/or methods on them as needed
- [_] Ensure any page-specific locator details are hidden in the page object models
- [_] Be sure to follow object store key patterns (TBD, see code for examples now)
- [_] Iterate as needed by building the project to ensure no stubs remain

## Iterate tests to passing

- [_] First, ensure the application is running in the background. If it's not, you can start it with `scripts\Start-LocalDev.ps1`
- [_] Run the current test scenario individually using `dotnet test --filter {ScenarioMethod}`
- [_] Make code or test fixes as needed until test passes
- [_] Finally, run all the scenarios in the feature we're working on with `dotnet test --filter {FeatureClass}`

## Clean-up

- [_] Mark the test as complete in the test plan
- [_] Create a commit message, under 50 words, for the new test
- [_] Commit the scenario before moving to the next one
