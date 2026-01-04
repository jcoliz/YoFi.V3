using Stubble.Core.Builders;
using Stubble.Core.Settings;

namespace YoFi.V3.Tests.Generator;

/// <summary>
/// Generates functional test C# code from CRIF objects and Mustache templates.
/// </summary>
/// <remarks>
/// This class combines a Code-Ready Intermediate Form (CRIF) object with a Mustache template
/// to produce compiler-ready C# test files.
/// </remarks>
public class FunctionalTestGenerator
{
    /// <summary>
    /// Generates test code from a CRIF object and Mustache template.
    /// </summary>
    /// <param name="template">Mustache template content as a string.</param>
    /// <param name="crif">Code-Ready Intermediate Form object containing test data.</param>
    /// <returns>Stream containing the generated C# code.</returns>
    public Stream Generate(string template, FunctionalTestCrif crif)
    {
        var renderer = new StubbleBuilder()
            .Configure(settings => settings.SetIgnoreCaseOnKeyLookup(true))
            .Build();
        var result = renderer.Render(template, crif);
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(result);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Generates test code from a CRIF object and Mustache template stream.
    /// </summary>
    /// <param name="templateStream">Stream containing Mustache template content.</param>
    /// <param name="crif">Code-Ready Intermediate Form object containing test data.</param>
    /// <returns>Stream containing the generated C# code.</returns>
    public Stream Generate(Stream templateStream, FunctionalTestCrif crif)
    {
        using var reader = new StreamReader(templateStream);
        var template = reader.ReadToEnd();
        return Generate(template, crif);
    }

    /// <summary>
    /// Generates test code from a CRIF object and Mustache template file.
    /// </summary>
    /// <param name="templatePath">Path to the Mustache template file.</param>
    /// <param name="crif">Code-Ready Intermediate Form object containing test data.</param>
    /// <returns>Stream containing the generated C# code.</returns>
    /// <remarks>
    /// This method is for testing and standalone use only. Source generators should use
    /// the Generate(string, FunctionalTestCrif) overload with template content from AdditionalFiles.
    /// </remarks>
#pragma warning disable RS1035 // Do not use APIs banned for analyzers - File IO is for testing only
    public Stream GenerateFromFile(string templatePath, FunctionalTestCrif crif)
    {
        var template = File.ReadAllText(templatePath);
        return Generate(template, crif);
    }
#pragma warning restore RS1035

    /// <summary>
    /// Generates test code from a template file and writes it directly to an output file.
    /// </summary>
    /// <param name="templatePath">Path to the Mustache template file.</param>
    /// <param name="crif">Code-Ready Intermediate Form object containing test data.</param>
    /// <param name="outputPath">Path where the generated C# file should be written.</param>
    /// <remarks>
    /// This method is for testing and standalone use only. Source generators should use
    /// the Generate(string, FunctionalTestCrif) overload and register output via context.
    /// </remarks>
#pragma warning disable RS1035 // Do not use APIs banned for analyzers - File IO is for testing only
    public void GenerateToFile(string templatePath, FunctionalTestCrif crif, string outputPath)
    {
        var template = File.ReadAllText(templatePath);
        var renderer = new StubbleBuilder()
            .Configure(settings => settings.SetIgnoreCaseOnKeyLookup(true))
            .Build();
        var result = renderer.Render(template, crif);
        File.WriteAllText(outputPath, result);
    }
#pragma warning restore RS1035

    /// <summary>
    /// Generates test code and returns it as a string.
    /// </summary>
    /// <param name="template">Mustache template content as a string.</param>
    /// <param name="crif">Code-Ready Intermediate Form object containing test data.</param>
    /// <returns>Generated C# code as a string.</returns>
    public string GenerateString(string template, FunctionalTestCrif crif)
    {
        var renderer = new StubbleBuilder()
            .Configure(settings => settings.SetIgnoreCaseOnKeyLookup(true))
            .Build();
        return renderer.Render(template, crif);
    }

    /// <summary>
    /// Generates test code from a template file and returns it as a string.
    /// </summary>
    /// <param name="templatePath">Path to the Mustache template file.</param>
    /// <param name="crif">Code-Ready Intermediate Form object containing test data.</param>
    /// <returns>Generated C# code as a string.</returns>
    /// <remarks>
    /// This method is for testing and standalone use only. Source generators should use
    /// the GenerateString(string, FunctionalTestCrif) overload with template content from AdditionalFiles.
    /// </remarks>
#pragma warning disable RS1035 // Do not use APIs banned for analyzers - File IO is for testing only
    public string GenerateStringFromFile(string templatePath, FunctionalTestCrif crif)
    {
        var template = File.ReadAllText(templatePath);
        var renderer = new StubbleBuilder()
            .Configure(settings => settings.SetIgnoreCaseOnKeyLookup(true))
            .Build();
        return renderer.Render(template, crif);
    }
#pragma warning restore RS1035
}
