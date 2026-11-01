# Writing Page and Component Objects

Page and component objects under [`Pages/`](../Pages/) and [`Components/`](../Components/) are the
only layer that should know how to locate and operate UI elements with Playwright.

## Core Rules

- Keep selectors in one place. Prefer stable semantic test IDs and `GetByTestId` over selectors
  coupled to CSS structure or visible layout.
- Scope component locators to their parent when the page contains multiple instances.
- Expose page-specific locators and user-level actions as methods or properties; do not make steps
  assemble selectors.
- Use descriptive methods that represent a user action, such as `SubmitCreateFormAsync`, rather than
  generic click helpers that leak UI details into the steps.
- Keep reusable UI behavior in a component object and page-specific behavior in its page object.

## Navigation and Readiness

Navigation methods should wait for a deterministic, observable signal that the destination is ready
for interaction. Avoid fixed `Task.Delay` waits. A visible page heading, loaded content, or an enabled
control can be appropriate depending on the page.

Nuxt pages may render server-side HTML before client hydration makes controls interactive. Follow the
[Nuxt hydration pattern](NUXT-SSR-TESTING-PATTERN.md): wait for a meaningful ready state before
acting, and ensure the front end exposes a reliable signal where needed.

## Wait for the User-Visible Operation

An action should settle before the page-object method returns. For example, after submitting a form,
wait for the resulting dialog, navigation, or updated data—not only for the initiating request.
Prefer Playwright's locator/action waits and existing helpers from the shared functional-test
library over fixed delays.

## Example

```csharp
public class HomePage(IPage page) : BasePage(page)
{
    public ILocator BrochureSection => Page.GetByTestId("brochure-section");

    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await BrochureSection.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeout
        });
    }
}
```

See [`HomePage.cs`](../Pages/HomePage.cs) and [`Pages/README.md`](../Pages/README.md) for project
examples and conventions.
