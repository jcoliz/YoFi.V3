namespace YoFi.V3.Tests.Functional.Attributes;

/// <summary>
/// Marks a step method as implementing a When step with the specified pattern.
/// </summary>
/// <param name="pattern">
/// The Gherkin pattern to match, with placeholders in {curly} braces.
/// Example: "I click the {buttonName} button"
/// </param>
/// <remarks>
/// When steps describe actions or events that occur during a test scenario.
/// They represent the user's interactions or system events being tested.
/// Multiple attributes can be applied to a single method to handle pattern variations.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class WhenAttribute(string pattern) : Attribute
{
    /// <summary>
    /// Gets the Gherkin pattern that this step method implements.
    /// </summary>
    public string Pattern { get; } = pattern;
}
