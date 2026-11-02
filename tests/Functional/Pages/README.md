# Page Object Models

Page objects encapsulate locators, UI operations, and readiness waits for a page. Components in
[`../Components/`](../Components/) provide the same boundary for reusable UI regions.

Keep Playwright selectors and mechanics in these objects; steps should call semantic methods rather
than construct locators. Prefer stable test IDs, scope locators to a parent when needed, and wait for
observable UI state instead of fixed delays.

[`PayeeRulesPage`](PayeeRulesPage.cs) supports the basic rule-creation scenario: navigation,
creating a substring rule, and reading its category by exact payee pattern. Other rule operations
are intentionally not implemented yet.

See [writing page objects](../docs/writing-pages.md) for the authoring guide and
[the Nuxt hydration pattern](../docs/NUXT-SSR-TESTING-PATTERN.md) for client-readiness requirements.
