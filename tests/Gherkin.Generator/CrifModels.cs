using System.Collections.Generic;

namespace YoFi.V3.Tests.Generator;

/// <summary>
/// Code-Ready Intermediate Form (CRIF) for functional test generation.
/// </summary>
/// <remarks>
/// This is the root object that gets combined with the Mustache template
/// to generate compiler-ready C# test files. It represents the fully resolved
/// test structure after combining Gherkin features with step definitions metadata.
/// </remarks>
public class FunctionalTestCrif
{
    /// <summary>
    /// List of using namespaces (e.g., "NUnit.Framework", "YoFi.V3.Tests.Functional.Steps").
    /// </summary>
    /// <remarks>
    /// Includes any namespaces required by the step classes.
    /// Includes base class namespace if specified.
    /// Includes any explicit namespaces set by feature tags: `@using:Namespace.Name`.
    /// </remarks>
    public List<string> Usings { get; set; } = [];

    /// <summary>
    /// Namespace for the generated test class.
    /// </summary>
    /// <remarks>
    /// Set by a tag on the feature: `@namespace:YoFi.V3.Tests.Functional.Features`.
    /// </remarks>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// File name of the .feature file, not including the extension
    /// </summary>
    /// <remarks>
    /// e.g. "BankImport" for "BankImport.feature"
    /// </remarks>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Feature name used for the test class name (e.g., "TransactionRecord").
    /// </summary>
    /// <remarks>
    /// Taken directly from the Gherkin Feature name.
    /// </remarks>
    public string FeatureName { get; set; } = string.Empty;

    /// <summary>
    /// Short description of the feature from the Gherkin file.
    /// </summary>
    /// <remarks>
    /// Taken directly from the Gherkin Feature description.
    /// </remarks>
    public string FeatureDescription { get; set; } = string.Empty;

    /// <summary>
    /// Multi-line description of the feature (user story, acceptance criteria, etc.).
    /// </summary>
    public List<string> DescriptionLines { get; set; } = [];

    /// <summary>
    /// Base class for the test fixture (e.g., "FunctionalTestBase").
    /// </summary>
    /// <remarks>
    /// Set by a tag on the feature: `@baseclass:FunctionalTestBase`. If the tag includes a namespace,
    /// that namespace should also be included in the Usings list, and it is stripped out of the BaseClass property.
    /// </remarks>
    public string BaseClass { get; set; } = string.Empty;

    /// <summary>
    /// List of step class names required by the test (e.g., "NavigationSteps", "AuthSteps").
    /// </summary>
    /// <remarks>
    /// This is set by the step-matching process. When steps are found, their owner classes are added to this list.
    /// </remarks>
    public List<string> Classes { get; set; } = [];

    /// <summary>
    /// Background section (setup steps that run before each test).
    /// </summary>
    public BackgroundCrif? Background { get; set; }

    /// <summary>
    /// List of rules containing scenarios.
    /// </summary>
    public List<RuleCrif> Rules { get; set; } = [];

    /// <summary>
    /// List of unimplemented steps that need to be generated as stubs.
    /// </summary>
    /// <remarks>
    /// If step matching fails to find a step, we will generate a stub method for it.
    /// This ensures the test code compiles even if some steps are not yet implemented.
    /// It also gives implementers a starting point for writing the missing step definitions.
    /// </remarks>
    public List<UnimplementedStepCrif> Unimplemented { get; set; } = [];
}

/// <summary>
/// Represents the Background section of a feature.
/// </summary>
/// <remarks>
/// Gherkin Background sections don't have user-provided names, so the method name
/// is hardcoded in the template as "SetupAsync".
/// </remarks>
public class BackgroundCrif
{
    /// <summary>
    /// List of steps to execute in the background.
    /// </summary>
    public List<StepCrif> Steps { get; set; } = [];
}

/// <summary>
/// Represents a Rule grouping scenarios in a feature.
/// </summary>
public class RuleCrif
{
    /// <summary>
    /// Rule name.
    /// </summary>
    /// <remarks>
    /// Taken directly from the Gherkin Rule name.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Rule description.
    /// </summary>
    /// <remarks>
    /// Taken directly from the Gherkin Rule description.
    /// </remarks>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of scenarios within this rule.
    /// </summary>
    public List<ScenarioCrif> Scenarios { get; set; } = [];
}

/// <summary>
/// Represents a test scenario.
/// </summary>
public class ScenarioCrif
{
    /// <summary>
    /// Scenario name (human-readable).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Method name for the test method (PascalCase version of Name).
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Optional remarks for the scenario (additional documentation).
    /// </summary>
    /// <remarks>
    /// Comes from Gherkin description of scenarios
    /// </remarks>
    public RemarksCrif? Remarks { get; set; }

    /// <summary>
    /// Whether this scenario should be marked with [Explicit] attribute.
    /// </summary>
    /// <remarks>
    /// Set by a tag on the scenario: `@explicit`.
    /// </remarks>
    public bool ExplicitTag { get; set; }

    /// <summary>
    /// List of test case parameters for parameterized tests (e.g., ["\"value1\"", "123"]).
    /// </summary>
    public List<string> TestCases { get; set; } = [];

    /// <summary>
    /// List of method parameters for parameterized tests.
    /// </summary>
    public List<ParameterCrif> Parameters { get; set; } = [];

    /// <summary>
    /// List of steps in the scenario.
    /// </summary>
    public List<StepCrif> Steps { get; set; } = [];
}

/// <summary>
/// Represents remarks section for XML documentation.
/// </summary>
public class RemarksCrif
{
    /// <summary>
    /// Lines of remarks text.
    /// </summary>
    public List<string> Lines { get; set; } = [];
}

/// <summary>
/// Represents a method parameter.
/// </summary>
public class ParameterCrif
{
    /// <summary>
    /// Parameter type (e.g., "string", "int", "DataTable").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the last parameter (for comma placement).
    /// </summary>
    public bool Last { get; set; }
}

/// <summary>
/// Represents a single step in a scenario or background.
/// </summary>
/// <remarks>
/// Owner, method, and arguments are determined by step matching against available step definitions.
/// Unless the step is unimplemented, in which case it will be listed in the UnimplementedSteps collection instead.
/// and we will include a call to it here, using `this` as the owner.
/// </remarks>
public class StepCrif
{
    /// <summary>
    /// Displayed step keyword (Given, When, Then, And, But).
    /// </summary>
    /// <remarks>
    /// This is the keyword as it appears in the Gherkin file for documentation purposes.
    /// Normalized step name will be used for step matching (e.g. "And" would match "Given" steps if the previous step was a Given).
    /// </remarks>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// Step text (human-readable description).
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Owner class type name (e.g., "NavigationSteps", "AuthSteps").
    /// </summary>
    /// <remarks>
    /// Exact class name from the step definition metadata.
    /// or `this` if the step is unimplemented (stub).
    /// </remarks>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Method name to call on the owner class.
    /// </summary>
    /// <remarks>
    /// Exact method name from the step definition metadata.
    /// </remarks>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// List of arguments to pass to the method (already formatted as C# code).
    /// </summary>
    public List<ArgumentCrif> Arguments { get; set; } = [];

    /// <summary>
    /// Optional data table associated with this step.
    /// </summary>
    public DataTableCrif? DataTable { get; set; }
}

/// <summary>
/// Represents a method argument.
/// </summary>
public class ArgumentCrif
{
    /// <summary>
    /// Argument value (already formatted as C# code).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the last argument (for comma placement).
    /// </summary>
    public bool Last { get; set; }

    /// <summary>
    /// Returns the argument value for Mustache template rendering.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents a Gherkin data table for a step.
/// </summary>
public class DataTableCrif
{
    /// <summary>
    /// Variable name for the generated DataTable instance (e.g., "table", "fieldsTable").
    /// </summary>
    /// <remarks>
    /// This is used to name the DataTable variable in the generated code.
    /// We will use `table1`, `table2`, etc for uniqueness within a single implemented method
    /// (scenario or background).
    /// </remarks>
    public string VariableName { get; set; } = string.Empty;

    /// <summary>
    /// Header row cells.
    /// </summary>
    public List<HeaderCellCrif> Headers { get; set; } = [];

    /// <summary>
    /// Data rows.
    /// </summary>
    public List<DataRowCrif> Rows { get; set; } = [];
}

/// <summary>
/// Represents a header cell in a data table.
/// </summary>
public class HeaderCellCrif
{
    /// <summary>
    /// Cell value (header name).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the last header cell (for comma placement).
    /// </summary>
    public bool Last { get; set; }

    /// <summary>
    /// Returns the header value for Mustache template rendering.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents a data row in a data table.
/// </summary>
public class DataRowCrif
{
    /// <summary>
    /// List of cell values in this row.
    /// </summary>
    public List<DataCellCrif> Cells { get; set; } = [];

    /// <summary>
    /// Whether this is the last row (for comma placement).
    /// </summary>
    public bool Last { get; set; }
}

/// <summary>
/// Represents a data cell in a data table row.
/// </summary>
public class DataCellCrif
{
    /// <summary>
    /// Cell value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the last cell in the row (for comma placement).
    /// </summary>
    public bool Last { get; set; }

    /// <summary>
    /// Returns the cell value for Mustache template rendering.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents an unimplemented step that needs to be generated as a stub.
/// </summary>
public class UnimplementedStepCrif
{
    /// <summary>
    /// Exact Step keyword (Given, When, Then).
    /// </summary>
    /// <remarks>
    /// No "and" or "but" keywords here - those are normalized to the main keyword.
    /// </remarks>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// Step text (human-readable description).
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Method name for the stub method.
    /// </summary>
    /// <remarks>
    /// Generated by converting the step text to PascalCase and removing special characters,
    /// such that it can be used as a valid C# method name.
    /// </remarks>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// List of parameters for the stub method.
    /// </summary>
    public List<ParameterCrif> Parameters { get; set; } = [];
}
