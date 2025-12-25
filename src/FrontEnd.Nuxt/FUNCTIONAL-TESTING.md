# Functional Testing Requirements for Frontend

This document describes patterns that **MUST** be implemented in Vue pages to enable reliable Playwright functional testing.

## Critical: Client-Ready Pattern for SSR/Hydration

**Problem**: When Playwright navigates to a Nuxt page, the initial response is server-rendered (SSR) HTML that is **non-interactive**. Vue must "hydrate" this HTML to make it interactive. Tests that interact with elements before hydration completes will fail or behave unpredictably.

**Solution**: Implement the client-ready pattern on every page that will be tested.

### Required Implementation

```vue
<script setup lang="ts">
// 1. Create a ready ref that starts false
const ready = ref(false)

// 2. Set to true once component is mounted (client-side only)
onMounted(() => {
  ready.value = true
})
</script>

<template>
  <!-- 3. Disable interactive elements until ready -->
  <button
    type="submit"
    data-test-id="Login"
    :disabled="isLoading || !ready"
  >
    {{ isLoading ? 'Signing In...' : 'Sign In' }}
  </button>
</template>
```

### Why This Works

1. **SSR renders `disabled` state**: During server-side rendering, `ready` is `false`, so buttons render as `disabled`
2. **Client hydration enables buttons**: `onMounted()` only runs client-side after hydration, setting `ready = true`
3. **Tests wait for enabled state**: Playwright Page Object Models wait for buttons to become enabled before proceeding
4. **Zero race conditions**: If the button is enabled, hydration is guaranteed complete

### Rules

- ✅ **DO**: Add `const ready = ref(false)` to every testable page
- ✅ **DO**: Add `onMounted(() => { ready.value = true })` hook
- ✅ **DO**: Include `!ready` in button disabled conditions: `:disabled="isLoading || !ready"`
- ✅ **DO**: Combine with loading state - never use ready alone
- ✅ **DO**: Add `data-test-id` to the primary action button
- ❌ **DON'T**: Remove the `!ready` condition - it breaks functional tests
- ❌ **DON'T**: Set `ready = true` anywhere except `onMounted()`
- ❌ **DON'T**: Use `ready` for visual loading indicators - use `isLoading` instead

### Examples from Production

**Login Page** ([`app/pages/login.vue`](./app/pages/login.vue)):

```vue
<script setup lang="ts">
const ready = ref(false)
onMounted(() => {
  ready.value = true
})

const isLoading = ref(false)
</script>

<template>
  <button
    type="submit"
    class="btn btn-primary"
    data-test-id="Login"
    :disabled="isLoading || !ready"
  >
    <span
      v-if="isLoading"
      class="spinner-border spinner-border-sm me-2"
      role="status"
      aria-hidden="true"
    />
    {{ isLoading ? 'Signing In...' : 'Sign In' }}
  </button>
</template>
```

## Data Test IDs

**ALWAYS** add `data-test-id` attributes to elements used in functional tests:

- **Buttons**: `data-test-id="action-button"` (e.g., `"Login"`, `"create-submit-button"`)
- **Form inputs**: `data-test-id="field-name"` (e.g., `"username"`, `"email"`, `"password"`)
- **Forms**: `data-test-id="FormName"` (e.g., `"LoginForm"`, `"RegisterForm"`)
- **Error displays**: `data-test-id="error-display"` or `"Errors"`
- **Loading states**: `data-test-id="BaseSpinner"`, `"loading-state"`

See [`.roorules`](./.roorules) for complete testing patterns.

## Complete Documentation

For comprehensive documentation including:

- Detailed explanation of the SSR/hydration timing issue
- Test-side implementation in Page Object Models
- Troubleshooting guide
- Alternative patterns (and why they don't work)
- Historical context and migration notes

See: [`tests/Functional/NUXT-SSR-TESTING-PATTERN.md`](../../tests/Functional/NUXT-SSR-TESTING-PATTERN.md)
