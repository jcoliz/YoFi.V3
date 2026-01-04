using Gherkin.Ast;

namespace YoFi.V3.Tests.Generator;

/// <summary>
/// Converts a Gherkin feature document into a Code-Ready Intermediate Form (CRIF)
/// for test generation, with step matching against available step definitions.
/// </summary>
/// <param name="stepMetadata">Collection of step definition metadata for matching.</param>
public class GherkinToCrifConverter(StepMetadataCollection stepMetadata)
{
    /// <summary>
    /// Converts a Gherkin feature document to a CRIF object.
    /// </summary>
    /// <param name="feature">Parsed Gherkin feature document.</param>
    /// <returns>Code-Ready Intermediate Form ready for template rendering.</returns>
    public FunctionalTestCrif Convert(GherkinDocument feature)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Collection of step definition metadata extracted from step classes.
/// </summary>
public class StepMetadataCollection
{
    /// <summary>
    /// Adds step metadata to the collection.
    /// </summary>
    /// <param name="metadata">Step definition metadata to add.</param>
    public void Add(StepMetadata metadata)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds multiple step metadata items to the collection.
    /// </summary>
    /// <param name="metadataItems">Collection of step definition metadata to add.</param>
    public void AddRange(IEnumerable<StepMetadata> metadataItems)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Finds a step definition matching the given Gherkin step.
    /// </summary>
    /// <param name="normalizedKeyword">Normalized keyword (Given, When, or Then).</param>
    /// <param name="stepText">Step text from Gherkin scenario.</param>
    /// <returns>Matching step metadata, or null if no match found.</returns>
    public StepMetadata? FindMatch(string normalizedKeyword, string stepText)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Metadata for a single step definition method.
/// </summary>
public class StepMetadata
{
    /// <summary>
    /// Normalized keyword (Given, When, or Then).
    /// </summary>
    public string NormalizedKeyword { get; set; } = string.Empty;

    /// <summary>
    /// Step text pattern with placeholders (e.g., "I have {quantity} cars in my {place}").
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Method name as defined in the step class.
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Class name containing the step definition.
    /// </summary>
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// Namespace of the class.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Method parameters with types and names.
    /// </summary>
    public List<StepParameter> Parameters { get; set; } = [];
}

/// <summary>
/// Represents a parameter in a step definition method.
/// </summary>
public class StepParameter
{
    /// <summary>
    /// Parameter type (e.g., "string", "int", "DataTable").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
