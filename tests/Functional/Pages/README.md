# Page Object Models

Page objects encapsulate locators, UI operations, and readiness waits for a page. Components in
[`../Components/`](../Components/) provide the same boundary for reusable UI regions.

Keep Playwright selectors and mechanics in these objects; steps should call semantic methods rather
than construct locators. Prefer stable test IDs, scope locators to a parent when needed, and wait for
observable UI state instead of fixed delays.

[`PayeeRulesPage`](PayeeRulesPage.cs) supports basic rule creation, editing the only rule in the
list, and empty-category validation: navigation, submitting a substring rule, reading its category
by exact payee pattern and regex status, reading validation errors, and checking the empty list.
Rows are matched by the dedicated payee-pattern text span and presence or absence of the regex badge,
not the combined cell text. List assertions can also check the rule count and regex badges. Other rule operations are
intentionally not implemented yet.

See [writing page objects](../docs/writing-pages.md) for the authoring guide and
[the Nuxt hydration pattern](../docs/NUXT-SSR-TESTING-PATTERN.md) for client-readiness requirements.

[`ImportPage`](ImportPage.cs) provides retrying assertions for a single imported transaction's
category text and read-only state, including the absence of category editing controls.
