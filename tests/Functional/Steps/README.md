# Step Definitions

Step definitions in this directory bind Gherkin phrases to C# methods. They receive an
`ITestContext`, orchestrate setup and assertions, and use page/component objects for browser
interaction.

## Organization

- [`AuthSteps.cs`](AuthSteps.cs), [`NavigationSteps.cs`](NavigationSteps.cs), and
  [`RegistrationSteps.cs`](RegistrationSteps.cs) cover shared site flows.
- [`Workspace/`](Workspace/) contains workspace setup, management, and permission steps.
- [`Transaction/`](Transaction/) contains transaction and bank-import steps, organized by
  responsibility.
- [`WeatherSteps.cs`](WeatherSteps.cs) contains weather scenarios.

The generator binds step text to methods decorated with `[Given]`, `[When]`, or `[Then]`. Search
existing feature files and step definitions before adding a new phrase.

For detailed authoring conventions, see [writing steps](../docs/writing-steps.md) and
[writing features](../docs/writing-features.md). For exact transaction setup, see
[Test Control APIs](../docs/TEST-CONTROLS.md).
