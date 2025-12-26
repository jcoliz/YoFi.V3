namespace YoFi.V3.Tests.Functional.Attributes;

/// <summary>
/// Marks a step method as implementing a Then step with the specified pattern.
/// </summary>
/// <param name="pattern">
/// The Gherkin pattern to match, with placeholders in {curly} braces.
/// Example: "I should see {expectedText} on the page"
/// </param>
/// <remarks>
/// Then steps describe expected outcomes and assertions for a test scenario.
/// They verify that the system behaves correctly after the When actions.
/// Multiple attributes can be applied to a single method to handle pattern variations.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ThenAttribute(string pattern) : Attribute
{
    /// <summary>
    /// Gets the Gherkin pattern that this step method implements.
    /// </summary>
    public string Pattern { get; } = pattern;
}
