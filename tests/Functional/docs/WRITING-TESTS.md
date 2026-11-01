# Writing Functional Tests

Use this checklist to take a scenario from intent to a passing browser test. The focused guides
linked below are the detailed references.

## Workflow

1. **Choose the right test layer.** Functional tests are for workflows where browser behavior is part
   of what must be verified. See the [testing strategy](../../../docs/TESTING-STRATEGY.md).
2. **Check for existing scenarios and steps.** Reuse the wording and definitions that already cover
   the behavior rather than adding near-duplicates.
3. **Write or update one Gherkin scenario.** Keep it about user-visible behavior and use a data table
   when the scenario needs explicit input values. See [writing features](writing-features.md).
4. **Build the functional test project.** `Gherkin.Generator` generates NUnit tests from the feature
   files during the build. Do not edit generated files under `obj/`.
5. **Implement any missing step definitions.** Put them in a focused domain class and use the Test
   Control client for setup that does not need the browser. See [writing steps](writing-steps.md).
6. **Add or update page/component methods as needed.** Keep locators, UI actions, and waits in page
   or component objects. See [writing pages](writing-pages.md).
7. **Run the scenario by itself and iterate until it passes.** Then run the feature's scenarios and
   the broader functional suite as appropriate.
8. **Review isolation and cleanup.** Tests must not depend on state left by another test.

## Useful References

- [Functional-test architecture](architecture.md)
- [Environment setup and runsettings](environments.md)
- [Debugging a failing test](debugging.md)
- [Front-end testing contract](frontend-contract.md)
- [Test Control APIs and precise transaction seeding](TEST-CONTROLS.md)
- [Nuxt SSR/client hydration pattern](NUXT-SSR-TESTING-PATTERN.md)

The generated NUnit source is useful for understanding how a scenario is translated, but the
`.feature` file and step definitions are the source files to change.
