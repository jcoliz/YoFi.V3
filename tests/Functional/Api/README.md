# API Client Generation

This directory contains the NSwag configuration for generating the C# API client used by functional tests.

## Configuration

- [`nswag.json`](nswag.json) - NSwag configuration for C# client generation
- Output: [`Generated/ApiClient.cs`](Generated/ApiClient.cs) (checked into source control)

## Generation Process

**IMPORTANT**: API client generation is now **manual** to avoid rebuilding on every test run.

The generated file is checked into source control, so most developers won't need to regenerate it unless the API changes.

## When to Regenerate

Regenerate the API client when:
- You add/modify/remove API endpoints
- You change request/response models
- You update API documentation attributes

## How to Regenerate

From the repository root, run:

```powershell
.\scripts\Generate-ApiClient.ps1
```

This script generates **both**:
1. TypeScript client for the frontend (`src/FrontEnd.Nuxt/app/utils/apiclient.ts`)
2. C# client for functional tests (`tests/Functional/Api/Generated/ApiClient.cs`)

## CI/CD Builds

For clean builds in CI/CD pipelines, the script should be run before building tests:

```bash
pwsh scripts/Generate-ApiClient.ps1
dotnet build tests/Functional
```

## Why Manual Generation?

Previously, NSwag ran automatically on every build, adding ~15-20 seconds even when the API hadn't changed. By moving to manual generation with source-controlled output, we:
- Eliminate unnecessary build time for test runs
- Make API changes more explicit (committed file shows what changed)
- Maintain build reproducibility (generated code is consistent across environments)
