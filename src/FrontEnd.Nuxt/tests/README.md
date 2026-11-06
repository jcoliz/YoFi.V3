# Frontend Unit Tests

This directory contains unit tests for the Vue frontend. These tests run in Node.js without a browser
and complement the Playwright functional tests in [`tests/Functional/`](../../../tests/Functional/).

## Philosophy

**Test logic, not presentation.**

The functional tests already verify that the application works end-to-end in a real browser. Frontend
unit tests exist to cover things that functional tests cannot easily reach:

- **Timezone-sensitive date handling** — a functional test cannot easily run in three timezones
- **Component behavior contracts** — props in, events out, API calls made, loading states, error paths
- **Edge cases in pure utility functions** — boundary values, empty inputs, error conditions

What unit tests should **not** do:

- Assert exact HTML structure or CSS class names — these break on every layout change and add no value
- Duplicate scenarios already covered by functional tests
- Test that a button has a particular Bootstrap class

### The Brittleness Test

Before writing an assertion, ask: *"Would this break if I changed the layout without changing the
behavior?"* If yes, don't write it. A test that breaks when you change `mb-0` to `mb-1` is not
finding bugs — it is creating churn.

## Test Runner

Tests use **[Vitest](https://vitest.dev/)** with **[`@vue/test-utils`](https://test-utils.vuejs.org/)**
for component mounting.

```bash
# Run all frontend unit tests
pnpm test:components

# Run in watch mode during development
pnpm test:components --watch

# Run a specific file
pnpm test:components date-only
```

## What Belongs Here

### ✅ Pure logic tests (highest value, lowest maintenance)

Tests for TypeScript utility functions that contain real logic. No Vue, no DOM, no mocking needed.

**Examples in this directory:**

- [`date-only.test.ts`](date-only.test.ts) — Verifies that date-only values from the API keep their
  calendar day across timezones (America/Los_Angeles, UTC, Pacific/Kiritimati). This catches a real
  class of bug where `new Date("2026-01-01")` shifts to Dec 31 in UTC-8.
- [`sample-data-dates.test.ts`](sample-data-dates.test.ts) — Verifies date range calculations for
  sample data seeding produce correct local calendar dates regardless of timezone.

These tests are cheap to write, cheap to maintain, and catch real bugs.

### ✅ Component behavior tests (medium value, medium maintenance)

Tests that verify a component's **contract**: given these props and user interactions, these API
calls are made and these events are emitted. Use `@vue/test-utils` `mount()` with mocked dependencies.

**Examples in this directory:**

- [`error-display.test.ts`](error-display.test.ts) — Verifies validation error display, server error
  auto-expansion, dismissal behavior, and the global `ProblemDetailsViewer` adapter.
- [`workspace-selector.test.ts`](workspace-selector.test.ts) — Verifies workspace switching, role-based
  management links, and empty-state messaging.
- [`workspace-delete-modal.test.ts`](workspace-delete-modal.test.ts) — Verifies the name-confirmation
  guard before deletion and the data-deletion flow.
- [`transaction-splits.test.ts`](transaction-splits.test.ts) — Verifies that split transactions
  protect their category field in the quick-edit modal.

### ❌ What does NOT belong here

- **HTML structure assertions** — Do not assert exact tag names, CSS classes, or layout. These break
  on every styling change and are already verified visually and through functional tests.
- **Duplicates of functional test scenarios** — If a Playwright test already covers "user can create
  a workspace", don't write a component test for the same scenario.
- **Snapshot tests** — Snapshots of rendered HTML are the worst form of presentation testing. They
  fail on every change and tell you nothing about whether the behavior is correct.
- **Page-level navigation tests** — Do not write unit tests for Nuxt pages (`app/pages/**/*.vue`).
  Pages depend on deeply Nuxt-specific composables (`useAuth`, `useAuthenticationGuard`,
  `definePageMeta`, `onBeforeRouteLeave`, `onBeforeRouteUpdate`, `useRouter`) that cannot be
  meaningfully mocked without recreating the entire Nuxt router. Page navigation, auth guards, and
  route transitions are covered by the functional tests. If you find yourself importing a page
  component into a unit test, stop and write a functional test instead.

## Writing a New Test

### Pure logic test

```typescript
import { describe, expect, it } from 'vitest'
import { myUtilityFunction } from '~/utils/myUtility'

describe('myUtilityFunction', () => {
  it('handles the empty case', () => {
    expect(myUtilityFunction('')).toBe('')
  })

  it('transforms valid input correctly', () => {
    expect(myUtilityFunction('hello')).toBe('HELLO')
  })
})
```

### Component behavior test

```typescript
import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import MyComponent from '~/components/MyComponent.vue'

describe('MyComponent', () => {
  it('calls the API and emits success on submit', async () => {
    // Given: A mock API client
    const mockSave = vi.fn().mockResolvedValue({ key: 'new-key' })
    const wrapper = mount(MyComponent, {
      props: { show: true },
      global: {
        provide: {
          apiClient: { saveItem: mockSave },
        },
      },
    })

    // When: User submits the form
    await wrapper.find('[data-test-id="submit-button"]').trigger('click')

    // Then: API was called and success event was emitted
    expect(mockSave).toHaveBeenCalledOnce()
    expect(wrapper.emitted('saved')).toBeTruthy()
  })
})
```

### Mocking Nuxt composables

There are two distinct patterns depending on how the component imports its dependency:

#### Explicitly imported modules → `vi.mock()`

Use `vi.mock()` for modules that are **explicitly imported** in the component source (e.g., Pinia stores). This must be called at module scope, before any imports that use the mocked module:

```typescript
// Component has: import { useUserPreferencesStore } from '~/stores/userPreferences'
vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: vi.fn(),
}))

// Import the mocked function AFTER vi.mock()
import { useUserPreferencesStore } from '~/stores/userPreferences'

// In your test:
vi.mocked(useUserPreferencesStore).mockReturnValue(fakeStore as any)
```

#### Nuxt auto-imported composables → `vi.stubGlobal()`

Use `vi.stubGlobal()` for composables that Nuxt **auto-imports** (no explicit import statement in the component). These are globals in the Nuxt runtime:

```typescript
// Component uses useApiClient(), navigateTo(), handleApiError() without importing them
const useApiClientMock = vi.fn()
vi.stubGlobal('useApiClient', useApiClientMock)
vi.stubGlobal('navigateTo', vi.fn())
vi.stubGlobal('handleApiError', (err: unknown) => err)
vi.stubGlobal('useProblemDetails', vi.fn())

// In your test:
useApiClientMock.mockReturnValue({ getItems: vi.fn().mockResolvedValue([]) })
```

#### Components with `<Teleport>` → inline stub

`ModalDialog` uses `<Teleport to="body">` which renders outside the test wrapper. Stub it with an inline version that renders slots directly so `wrapper.find()` can locate elements:

```typescript
import { h } from 'vue'

const ModalDialogStub = {
  props: ['show', 'testId', 'primaryButtonTestId', 'secondaryButtonTestId', 'loading', 'primaryButtonDisabled'],
  emits: ['primary', 'update:show'],
  setup(props: any, { slots, emit }: any) {
    return () => props.show
      ? h('div', { 'data-test-id': props.testId }, [
          slots.default?.(),
          h('button', {
            'data-test-id': props.primaryButtonTestId,
            disabled: (props.loading || props.primaryButtonDisabled) ? true : undefined,
            onClick: () => emit('primary'),
          }),
          h('button', {
            'data-test-id': props.secondaryButtonTestId,
            onClick: () => emit('update:show', false),
          }),
        ])
      : null
  },
}
```

#### `NuxtLink` → `inheritAttrs: false`

`NuxtLink` must use `inheritAttrs: false` so that `data-test-id` attributes pass through to the rendered `<a>` element:

```typescript
const NuxtLink = {
  inheritAttrs: false,
  props: ['to'],
  template: '<a v-bind="$attrs" :href="to"><slot /></a>',
}
```

## Relationship to Other Test Layers

```
Frontend Unit Tests (this directory)
  ↓ covers: logic, component contracts, timezone edge cases
  ↓ does NOT cover: full user workflows, real API calls, browser rendering

Functional Tests (tests/Functional/)
  ↓ covers: complete user workflows in a real browser
  ↓ does NOT cover: timezone edge cases, error paths that are hard to trigger
```

The two layers are complementary. When deciding where to test something:

| Question | Answer |
|---|---|
| Is it a pure TypeScript function with real logic? | Unit test here |
| Does it involve timezone-sensitive date handling? | Unit test here |
| Is it a component behavior that's hard to trigger in a browser? | Unit test here |
| Is it a complete user workflow? | Functional test |
| Is it already covered by a functional test? | Don't duplicate it |
| Does it test HTML structure or CSS classes? | Don't test it at all |

## Prohibited Patterns

The following patterns were used in earlier tests and **must not be reintroduced**:

### ❌ Manual TypeScript compilation

```js
// DO NOT DO THIS
const compiled = ts.transpileModule(source, { ... })
new Function('require', 'exports', compiled)(require, exports)
```

Use Vitest's native TypeScript support instead. Vitest handles `.ts` and `.vue` files directly.

### ❌ Custom in-memory renderer

```js
// DO NOT DO THIS
const renderer = createRenderer({
  createElement: (type) => node(type),
  createText: (text) => node('#text', text),
  // ... 40 more lines
})
```

Use `@vue/test-utils` `mount()` instead. It provides a proper component environment with far less
boilerplate.

### ❌ HTML structure assertions

```js
// DO NOT DO THIS
assert.match(html, /<h5 class="card-title mb-0">Upload Files<\/h5>/)
assert.match(html, /^<div class="mb-3">/)
```

These break whenever layout or spacing classes change. Test behavior, not markup.

### ❌ `node:test` runner

```js
// DO NOT DO THIS
import { test } from 'node:test'
import assert from 'node:assert/strict'
```

Use Vitest's `test`/`it`/`describe`/`expect` instead. Vitest integrates with the project's toolchain
and provides better error messages, watch mode, and coverage.
