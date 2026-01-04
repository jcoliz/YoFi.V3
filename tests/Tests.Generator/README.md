# Test Generator Prototype

This project is a space for prototyping components of the Gherkin-based test generation pipeline

## Generation Flow

Here's how it works

### Inputs

1. Gherkin feature. Text file written in Gherkin. Human-readable BDD source of truth
2. Step defitinions. C# source files containing classes with methods having Given/When/Then step attributes
3. Mustache template. Describes exactly how the code should look.

### Process

1. Gherkin feature loaded into memory using Gherkin library, as a Gherkin object
2. Step definitions scanned and metadata retainas as a Step Metadata collection.
3. Gherkin object and Step Metadata combined in a step lookup process, results in a Code-Ready Intermediate Form (Crif) object.
4. Mustache template and Crif object combined to create a compiler-ready C# file.
5. This could be checked in, or could reside in obj directory

## Step Metadata

- Normalized keyword: Given, When, Then **only**
- Text: The matching text explaining the step
- Method: Exact method name as defined
- Class: Exact name of the class containing the step
- Namespace: Namespace in which the class resides

For example, the following step...

```c#
namespace YoFi.V3.Tests.Functional.Steps;
public class CarSteps
{
[Given("I have {quantity} cars in my {place}")]
public async Task IHazThemCars(int quantity, string place) {}
}
```

...produces the following metadata...

- Normalized keyword: Given
- Text: I have {quantity} cars in my {place}
- Method: IHazThemCars
- Args: [ int quantity; string place ]
- Class: CarSteps
- Namespace: YoFi.V3.Tests.Functional.Steps

## Step matching

After the steps have been extracted into metadata, and the gherkin file has been loaded, we can match the steps.

The goal is to find the step definition method which corresponds to each step line in the scenario.

The step line in the scenario has two parts:
- Step keyword: "Given", "And", "Then" etc
- Step text: the part that comes after

For matching, the step keyword is normalized to a pure Given/When/Then, based on its position in the order.

To match a gherkin step to a definition, we search only amongst the defintions which match the normalized keyword

We look for an *exact* match (case insensitive) on the step text, while considering the placeholder keywords.

Placeholder keywords match whole words only (no internal whitespace) unless the Gherkin text has quotes, e.g.
- Definition: I have an account named {account}
- Matches: I have an account named Ski-Village
- Matches: I have an account named "Ski Village"
- Doesn't match: I have an account named Ski Village

When we get a match, we populate the CRIF with
- Displayed keyword from the gherkin scenario text (includes then)
- Text from the gherkin scenario text
- Method & Class in the step from the step definition metadata
- Using in the file metadata if this is unique
- Class in the list of classes if not already there
- Arguments matching the expected order, type and form of the called method

If we do not find a match, we populate the Unimplemented Crif using the gherkin scenario information
