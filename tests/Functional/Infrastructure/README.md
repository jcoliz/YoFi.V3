# Functional Test Infrastructure

This directory contains shared lifecycle and context code used by generated functional tests and
their step definitions.

- [`FunctionalTestBase.cs`](FunctionalTestBase.cs) manages test setup/teardown, initializes the Test
  Control client, checks prerequisites, and tracks test users and workspaces for cleanup.
- [`ITestContext.cs`](ITestContext.cs) is the context contract injected into step classes. It exposes
  the browser page, Test Control client, object store, page-object factory, and resource helpers.
- [`ObjectStoreKeys.cs`](ObjectStoreKeys.cs) defines keys used to share state between steps.
- [`TestControl/`](TestControl/) contains the Test Control API client base and OpenAPI/client
  configuration. The generated C# client is build output under `obj/GeneratedFiles/`.

See [functional-test architecture](../docs/architecture.md),
[writing steps](../docs/writing-steps.md), and
[Test Control APIs](../docs/TEST-CONTROLS.md) for how these pieces are used.
