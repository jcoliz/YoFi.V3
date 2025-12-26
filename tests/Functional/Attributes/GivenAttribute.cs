namespace YoFi.V3.Tests.Functional.Attributes;

/// <summary>
/// Marks a step method as implementing a Given step with the specified pattern.
/// </summary>
/// <param name="pattern">
/// The Gherkin pattern to match, with placeholders in {curly} braces.
/// Example: "I have an existing account with email {email}"
/// </param>
/// <remarks>
/// Given steps set up the initial state or context for a test scenario.
/// They describe preconditions and initial configurations.
/// Multiple attributes can be applied to a single method to handle pattern variations.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class GivenAttribute(string pattern) : Attribute
{
    /// <summary>
    /// Gets the Gherkin pattern that this step method implements.
    /// </summary>
    public string Pattern { get; } = pattern;
}
