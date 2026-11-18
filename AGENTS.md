# Agent Guidance

## Architecture

- The frontend is a Vue/Nuxt SPA; the backend is ASP.NET Core. The backend follows Clean Architecture with a BFF API: keep controllers thin and business logic in Application Features.
- Preserve inward dependency flow: Application depends on Entities abstractions, not database or HTTP infrastructure. Data implements the Entities data-access interfaces.
- See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/adr/README.md](docs/adr/README.md) for architecture details and decision rationale.

## Work Tracking

This project tracks Epics, Features, Stories, Scenarios, and Bugs as work items in the [YoFiV3 Azure DevOps project](https://dev.azure.com/jcoliz/YoFiV3). Do not look for these work items in GitHub Issues. The source code is hosted on GitHub.

- Use the Azure CLI (`az`) and the `azure-devops` extension to interact with Azure DevOps; do not require or depend on an Azure DevOps MCP server.
- Use the organization `https://dev.azure.com/jcoliz` and project `YoFiV3`. Configure them with `az devops configure --defaults organization=https://dev.azure.com/jcoliz project=YoFiV3` if needed.
- Use `az boards` for work items and queries and `az pipelines` for builds and pipeline runs.
- Read Azure DevOps data when needed, but mutate work items or pipelines only when the user explicitly requests or confirms the operation.

CI and deployment use Azure Pipelines; see [`.azure/pipelines/ci.yaml`](.azure/pipelines/ci.yaml). The GitHub repository also has a basic Actions workflow, but it is not the primary build or deployment system.

## Logging

- Always use `[LoggerMessage]`-attributed partial methods for logging. Do not call logger methods directly (for example, `_logger.LogInformation(...)`).
- Keep application and data layers logging-free; log at the API boundary, and never log sensitive user data.
- See [docs/LOGGING-POLICY.md](docs/LOGGING-POLICY.md) for the full pattern.

## Testing

- **NEVER run ANY tests using C# Dev Kit or `dotnet test`. ALWAYS use `Run-Tests.ps1`.**
- Run focused tests for the code changed using `Run-Tests.ps1`. For end-to-end behavior, prefer the functional tests against the containerized app.
- Do not create or modify functional tests—including Gherkin scenarios, step definitions, or related test support code—unless the user explicitly asks for functional tests and specifies what aspects to cover. Do not infer functional-test work from a feature or UI implementation request.
- When Application-layer changes affect how data is queried or persisted, consider whether the Application integration tests need updates and run the relevant tests. These tests exercise Application code with the data backend; see `tests/Integration.Application/`.
- Before working in `tests/Functional`, read [tests/Functional/README.md](tests/Functional/README.md) and the relevant guides it links to. Also check area-specific READMEs when changing that area.
- In Gherkin scenarios, put all `When` action steps before `Then` assertion steps. Never add a `When` step after a `Then` step.
- Do not run functional tests against the Aspire development environment or with `development*.runsettings`; that setup is often unsuitable for functional tests. Use the container workflow below instead.
- To build and start the app containers without opening browser windows, run:

  ```powershell
  .\scripts\Start-Container.ps1 -Build -NoBrowser
  ```

- With the app containers running, run functional tests using:

  ```powershell
  .\scripts\Run-Tests.ps1 -Functional
  ```

- **Never stop or tear down the app containers**, even if you started them. Do not run `Stop-Container.ps1` or a compose-down command. Avoid test wrappers that clean up app containers automatically.

## Document Metadata

- For Markdown documents that have metadata, put it in YAML front matter at the very top of the file, between `---` delimiters.
- Do not put document metadata such as status, release, ADO work item, parent, or PRD reference in the document body. Follow the front-matter fields used by nearby documents of the same type.

## Further Guidance

- See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for setup, coding standards, and general test guidance.
- See [scripts/README.md](scripts/README.md) for project automation scripts.
