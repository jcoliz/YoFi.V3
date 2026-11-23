---
description: "Use when creating or using Vue components in the Nuxt frontend, including Nuxt component auto-import names."
applyTo: "src/FrontEnd.Nuxt/app/**/*.vue"
---
# Nuxt Component Auto-imports

Components under `app/components` are auto-imported by Nuxt.

Derive the component identifier from its complete path relative to `app/components`, excluding `.vue`, and convert every path segment to PascalCase.

Example:

- `app/components/transaction/PayeeRulesAction.vue`
- Auto-import name: `TransactionPayeeRulesAction`
- Template usage: `<TransactionPayeeRulesAction />`

Do not use only the filename (`<PayeeRulesAction />`) when the component is inside a subdirectory.

Before adding a component usage:

1. Calculate the identifier from the full relative path.
2. Use that identifier in the template.
3. Run `pnpm typecheck` from `src/FrontEnd.Nuxt`.
