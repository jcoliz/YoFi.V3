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
