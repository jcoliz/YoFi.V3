# Nuxt SSR/Client Hydration Testing Pattern

## The Problem

When testing Nuxt applications with Playwright, a critical timing issue exists: **initial page navigation delivers server-rendered (or statically-generated) HTML that is non-interactive**. Tests that attempt to interact with elements immediately after navigation will fail because:

1. **Server-Side Rendering (SSR)**: The initial page load returns HTML generated on the server
2. **Client Hydration**: Vue/Nuxt must then "hydrate" this HTML to make it interactive
3. **Timing Gap**: There's a delay between page load and client hydration completion

If tests interact with elements before hydration completes, they're testing against **non-interactive server HTML**, leading to:
- Elements appearing to be present but not responding to events
- Form submissions failing silently
- Button clicks having no effect
- Inconsistent test failures (race conditions)

## The Solution

Use a **client-ready indicator pattern** where Vue pages signal when client-side hydration is complete. The most reliable indicator is **disabling interactive elements until the page is ready**.

### Frontend Pattern (Vue/Nuxt)

On every page that requires user interaction, implement this pattern:

```vue
<script setup lang="ts">
// 1. Create a "ready" ref that starts as false
const ready = ref(false)

// 2. Set it to true once the component is mounted (client-side)
onMounted(() => {
  ready.value = true
})
</script>

<template>
  <!-- 3. Disable interactive elements until ready -->
  <button
    type="submit"
    class="btn btn-primary"
    data-test-id="Login"
    :disabled="isLoading || !ready"
  >
    Sign In
  </button>
</template>
```

**Key Points:**
- The `ready` ref starts `false`, so elements are disabled during SSR/initial load
- `onMounted()` only runs on the client after hydration
- Interactive elements (buttons, inputs, links) remain disabled until `ready` is true
- This prevents tests from interacting with non-hydrated elements

### Test Pattern (Page Object Models)

In Page Object Models, implement a `WaitForPageReadyAsync()` method that waits for the ready indicator:

```csharp
/// <summary>
/// Waits for the page to be ready for interaction
/// </summary>
public async Task WaitForPageReadyAsync(float timeout = 5000)
{
    await WaitForLoginButtonEnabledAsync(timeout);
}

/// <summary>
/// Waits until the login button becomes enabled
/// </summary>
/// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
/// <remarks>
/// Waits for the button to transition from disabled (SSR/hydration) to enabled (client-ready).
/// This ensures the Vue client has finished hydrating and the page is interactive.
/// </remarks>
public async Task WaitForLoginButtonEnabledAsync(float timeout = 5000)
{
    await LoginButton.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = timeout });
    await LoginButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

    // Poll until the button is enabled
    var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
    while (DateTime.UtcNow < deadline)
    {
        var isDisabled = await LoginButton.IsDisabledAsync();
        if (!isDisabled)
        {
            return; // Button is now enabled - page is ready
        }
        await Task.Delay(50);
    }

    throw new TimeoutException($"Login button did not become enabled within {timeout}ms");
}
```

**Always call `WaitForPageReadyAsync()` after navigation:**

```csharp
/// <summary>
/// Navigates to this page
/// </summary>
public async Task NavigateAsync(bool waitForReady = true)
{
    await Page!.GotoAsync("/login");
    if (waitForReady)
    {
        await WaitForPageReadyAsync();
    }
}
```

## Why This Pattern Works

1. **Explicit Signal**: The page explicitly signals when it's ready for interaction
2. **Client-Side Only**: `onMounted()` only runs after hydration completes
3. **Observable State**: Button enabled/disabled state is observable from Playwright
4. **Zero False Positives**: If the button is enabled, hydration is definitely complete
5. **Prevents Race Conditions**: Tests can't proceed until the page is interactive

## Implementation Checklist

When creating or updating Vue pages that will be tested:

- [ ] Add `const ready = ref(false)` to the script setup
- [ ] Add `onMounted(() => { ready.value = true })` hook
- [ ] Add `:disabled="isLoading || !ready"` to primary action button
- [ ] Ensure the primary button has a `data-test-id` attribute

When creating or updating Page Object Models:

- [ ] Add `WaitForPageReadyAsync()` method
- [ ] Implement button-enabled polling logic
- [ ] Call `WaitForPageReadyAsync()` in `NavigateAsync()`
- [ ] Document the ready-check strategy in XML comments

## Examples from Production

### Frontend Examples

**Login Page** ([`src/FrontEnd.Nuxt/app/pages/login.vue`](../../src/FrontEnd.Nuxt/app/pages/login.vue)):
```vue
<script setup lang="ts">
const ready = ref(false)
onMounted(() => {
  ready.value = true
})
</script>

<template>
  <button
    type="submit"
    class="btn btn-primary"
    data-test-id="Login"
    :disabled="isLoading || !ready"
  >
    {{ isLoading ? 'Signing In...' : 'Sign In' }}
  </button>
</template>
```

### Test Examples

**LoginPage** ([`tests/Functional/Pages/LoginPage.cs`](./Pages/LoginPage.cs)):
```csharp
public async Task NavigateAsync(bool waitForReady = true)
{
    await Page!.GotoAsync("/login");
    if (waitForReady)
    {
        await WaitForPageReadyAsync();
    }
}

public async Task WaitForPageReadyAsync(float timeout = 5000)
{
    await WaitForLoginButtonEnabledAsync(timeout);
}
```

**RegisterPage** ([`tests/Functional/Pages/RegisterPage.cs`](./Pages/RegisterPage.cs)):
```csharp
public async Task NavigateAsync()
{
    await Page!.GotoAsync("/register");
    await WaitForPageReadyAsync();
}

public async Task WaitForPageReadyAsync(float timeout = 5000)
{
    await WaitForRegisterButtonEnabledAsync(timeout);
}
```

## Alternative Patterns (Not Recommended)

### ❌ Don't: Wait for API calls only
```csharp
// This doesn't guarantee client hydration is complete
await Page.WaitForResponseAsync(response => response.Url.Contains("/api/"));
```
API calls may complete before or after hydration.

### ❌ Don't: Use fixed delays
```csharp
// Brittle and slow
await Task.Delay(2000);
```
Fixed delays are unreliable (too short = flaky tests, too long = slow tests).

### ❌ Don't: Wait for network idle
```csharp
// Unreliable for SPAs with continuous polling
await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
```
Network idle doesn't correlate with client interactivity in modern SPAs.

### ✅ Do: Wait for explicit client-ready signal
```csharp
// Reliable and fast
await WaitForPageReadyAsync();
```
This pattern waits for the exact moment the page becomes interactive.

## Troubleshooting

### Tests failing with "Element is not enabled"
- **Cause**: Test attempting to interact before hydration completes
- **Fix**: Ensure `WaitForPageReadyAsync()` is called after navigation

### Tests timing out waiting for button to enable
- **Cause**: Frontend missing `ready` ref or `onMounted()` hook
- **Fix**: Add the ready pattern to the Vue component

### Button never becomes enabled
- **Cause**: Button has additional disabled conditions (e.g., `isLoading` stuck as true)
- **Fix**: Check component logic and ensure `isLoading` is managed correctly

### Inconsistent test failures
- **Cause**: Race condition between test execution and hydration
- **Fix**: Add missing `WaitForPageReadyAsync()` calls

## Related Documentation

- **Page Object Model Patterns**: [`tests/Functional/Pages/README.md`](./Pages/README.md)
- **Functional Test Overview**: [`tests/Functional/README.md`](./README.md)
- **Vue.js SSR Guide**: https://vuejs.org/guide/scaling-up/ssr.html
- **Nuxt.js Rendering Modes**: https://nuxt.com/docs/guide/concepts/rendering

## Historical Context

This pattern was discovered after experiencing widespread test flakiness where:
- Tests would pass locally but fail in CI
- Tests would fail intermittently with "element not interactive" errors
- Form submissions appeared to succeed but had no effect
- The same test would fail at random intervals

The root cause was identified as **testing against server-rendered HTML before Vue client hydration**. Implementing this pattern eliminated all hydration-related test flakiness.

## Migration Notes

If you have existing tests that use complex Vue reactivity waiting logic (like `FillCredentialsWithVueWaitAsync`), you may be able to simplify them once the ready pattern is implemented on all pages. The legacy Vue waiting logic was a workaround for this same problem.

**Before (complex workaround):**
```csharp
private async Task FillCredentialsWithVueWaitAsync(string email, string password)
{
    // Complex polling logic to wait for Vue reactivity
    // Multiple retries, blur events, value checks...
    // 50+ lines of workaround code
}
```

**After (simple and reliable):**
```csharp
public async Task EnterCredentialsAsync(string email, string password)
{
    await WaitForPageReadyAsync(); // Ensure page is ready first
    await UsernameInput.FillAsync(email);
    await PasswordInput.FillAsync(password);
    // Simple and reliable - no polling needed
}
```

The ready pattern moves the responsibility for signaling "ready" to the page itself, eliminating the need for tests to guess when hydration is complete.
