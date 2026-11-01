# Front-End Testing Contract

Front-end markup and behavior are part of the functional test interface. A front-end change that
renames or removes a test hook can break page objects even when the visible UI still looks the same.

## Test IDs

- Add a stable `data-test-id` to controls or regions that tests need to find.
- Use kebab-case semantic names for new IDs. Existing IDs such as `BaseSpinner` and `SideBar` are
  established; do not rename them casually or require unrelated code to migrate them.
- IDs do not need to be globally unique if the page object scopes the locator through a stable
  parent.
- When changing a test ID, update the corresponding page/component object in the same change.

## Readiness

Tests need deterministic, observable readiness—not timing guesses. Pages should expose a reliable
signal that user interactions are safe. Where Nuxt SSR/client hydration can leave rendered controls
temporarily non-interactive, use the [hydration pattern](NUXT-SSR-TESTING-PATTERN.md) and wait for
the signal in the page object.

Coordinate substantial front-end changes with the functional test owner: identify changed test IDs,
readiness behavior, and affected page objects as part of the change.
