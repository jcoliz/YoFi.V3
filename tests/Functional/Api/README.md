# Backend API Client

This folder contains the configuration for generating the C# API client used by functional tests.

## How It Works

The C# API client is automatically generated during the test project build process:

1. **NSwag Configuration**: [`nswag.json`](nswag.json) defines how to generate the C# client from the backend's OpenAPI specification
2. **MSBuild Integration**: The project file ([`../YoFi.V3.Tests.Functional.csproj`](../YoFi.V3.Tests.Functional.csproj)) includes MSBuild targets that:
   - Run NSwag before compilation to generate `ApiClient.cs`
   - Place the generated file in `obj/Generated/ApiClient.cs` (not in source control)
   - Clean up the generated file during build clean operations

## Generated Files

- **Location**: `tests/Functional/obj/Generated/ApiClient.cs`
- **Namespace**: `YoFi.V3.Tests.Functional.Generated`
- **Source Control**: NOT checked in (lives only in obj directory)

## Regenerating the Client

The client is automatically regenerated whenever you:
- Build the Functional tests project
- Rebuild the solution

You don't need to manually regenerate the C# client - it happens automatically.

## Configuration

If you need to modify the C# client generation:
1. Edit [`nswag.json`](nswag.json) in this directory
2. Rebuild the test project to regenerate with new settings

## Technical Details

This implementation uses:
- **NSwag.MSBuild** package for client generation
- **MSBuild targets** (`GenerateApiClient` and `CleanApiClient`) for build integration
- **`$(BaseIntermediateOutputPath)`** to place generated file in `obj/` directory

See [`docs/wip/functional-tests/API-CLIENT-GENERATION-IMPROVEMENT.md`](../../../docs/wip/functional-tests/API-CLIENT-GENERATION-IMPROVEMENT.md) for the complete implementation details.
