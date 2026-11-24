---
status: Draft
ado: Feature 2334
---
# Technical Design: Scalar Token Auth to NuxtIdentity

## Problem Statement

YoFi implements scalar auth for ease of implementing Scalar for API troubleshooting. It would be nice to
have this available for other apps as well.

## Prompt

> This file implements Scalar auth, making it much easier to interact with Scalar. I want to move this into NuxtIdentity. This is cloned locally, you can see the controllers in src\AspNetCore\Controllers\NuxtAuthControllerBase.Auth.cs.
>
> The question is, how would you recommend we make this OPT-IN for other users of NuxtIdentity?


I’d make the **token adapter reusable in NuxtIdentity, but require the application to explicitly expose the route**. That gives consumers an opt-in they can see in their controller, without changing the endpoints of existing NuxtIdentity applications.

## Recommended design

Move the exchange logic from `AuthController.Token.cs` into a **protected helper** on `NuxtAuthControllerBase<TUser>`, along with the form and response models. A protected method isn’t an MVC action, so installing or upgrading NuxtIdentity would not expose `/api/auth/token`.

Consumers who want it add a thin action to their concrete controller, conceptually:

```csharp
[AllowAnonymous]
[HttpPost("token")]
[Consumes("application/x-www-form-urlencoded")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public Task<IActionResult> Token(
    [FromForm] ScalarTokenRequest request,
    CancellationToken cancellationToken)
    => ExchangeScalarTokenAsync(request, cancellationToken);
```

NuxtIdentity would supply the request model, helper, response types, and appropriate OpenAPI response metadata. The application still controls whether the action exists, its route, and any endpoint-specific policies.

This fits the existing design: `NuxtAuthControllerBase.Auth.cs` already exposes virtual `Login` and `RefreshTokens` methods. The helper should call those virtual methods so existing overrides continue to apply, while documenting that a direct method call does **not** execute MVC filters or action-level middleware configured specifically on those endpoints.

## Scalar configuration remains separately opt-in

The endpoint alone doesn’t configure Scalar. YoFi’s `SetupOpenApi.cs` also adds the OAuth-style security scheme and makes it selectable. I’d initially leave that setup with the host and provide a documented example. That avoids coupling NuxtIdentity’s core ASP.NET package to Scalar or a particular OpenAPI implementation.

Only advertise the scheme when the token route is enabled. Hiding Scalar itself is **not** sufficient to disable the endpoint.

## Important security boundary

This uses the resource-owner password grant as a convenience adapter, not a general-purpose OAuth authorization server. Current OAuth security guidance says the [password grant MUST NOT be used](https://www.rfc-editor.org/rfc/rfc9700.html#section-2.4). I’d document this explicitly and recommend restricting the adapter to development or trusted, controlled deployments; production users should prefer authorization code with PKCE through a proper identity provider.

The endpoint also needs HTTPS, appropriate rate limiting, non-sensitive logging, no-store responses, and the same credential and refresh-token protections as the existing login paths. In particular, NuxtIdentity’s default login currently calls `CheckPasswordSignInAsync` with `lockoutOnFailure: false`; the new route should not imply brute-force protection exists when it does not.

> Updated: We now limit the endpoint to "development or trusted, controlled deployments", by checking
> DotNetNuxt.Abstractions.Options.StartupOptions.EnableSwaggerUI, and returning 404 if false.
>
> Note that this will remain the caller's responsibility to determine the trust level of the
> current running deployment.

## Alternative

If a one-line application configuration is more important than an explicit controller action, NuxtIdentity could use an MVC application-model convention to remove an inherited token action by default and add it only when configured. That is more convenient for consumers, but introduces startup-order and discovery complexity. I’d start with the explicit wrapper unless avoiding even that small amount of host code is a primary goal.

For validation, I’d test that the route and OpenAPI scheme are **absent by default**, that opting in supports login and refresh rotation, and that controller overrides and endpoint policies behave as documented.
