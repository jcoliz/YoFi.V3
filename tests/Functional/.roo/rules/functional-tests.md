# Functional Test Rules

## Source of Truth

- Gherkin feature files in `Features/` describe scenario intent.
- C# step definitions in `Steps/` implement those phrases.
- Page/component objects in `Pages/` and `Components/` own Playwright locators, UI operations, and
  readiness waits.
- Generated test/client source under `obj/GeneratedFiles/` is build output. Never create or edit it
  by hand.

`Gherkin.Generator` uses its package-provided default template. Do not add a project-specific
Mustache template unless that requirement changes.

## Authoring Workflow

1. Work on one scenario at a time: write it, build, implement missing steps and page-object methods,
   run it to passing, then continue.
2. Search existing features and step definitions before adding new step phrases.
3. Use Test Control APIs for test setup that does not require the UI. For transactions, seed the
   exact scenario values with `TestControl_SeedTransactionsPreciseAsync`; do not use random data
   when the scenario depends on transaction fields.
4. Keep Playwright selectors and mechanics in page/component objects, not step definitions.
5. Wait on observable UI state. Do not add fixed delays to make tests pass.
6. Track created test users and workspaces so the test base can clean them up.

## References

- [Functional test overview](../../README.md)
- [Writing tests workflow](../../docs/WRITING-TESTS.md)
- [Feature files](../../docs/writing-features.md)
- [Step definitions](../../docs/writing-steps.md)
- [Page and component objects](../../docs/writing-pages.md)
- [Test Control APIs](../../docs/TEST-CONTROLS.md)
- [Debugging](../../docs/debugging.md)
