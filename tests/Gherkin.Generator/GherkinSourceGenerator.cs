using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Gherkin;
using Gherkin.Ast;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace YoFi.V3.Tests.Generator;

/// <summary>
/// Incremental source generator that generates NUnit test code from Gherkin feature files.
/// </summary>
/// <remarks>
/// This generator:
/// - Discovers step definitions from C# source files using Roslyn analysis
/// - Processes .feature files and .mustache template from AdditionalFiles
/// - Generates test code automatically at build time
/// - Always produces compilable code (generates stubs for unimplemented steps)
/// </remarks>
[Generator]
public class GherkinSourceGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the incremental generator pipeline.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Collect Mustache templates from AdditionalFiles (any .mustache file)
        var templateProvider = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".mustache"))
            .Select((file, cancellationToken) => file.GetText(cancellationToken)?.ToString() ?? string.Empty)
            .Collect()
            .Select((templates, _) => templates.FirstOrDefault() ?? string.Empty);

        // 2. Collect .feature files from AdditionalFiles
        var featureFilesProvider = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".feature"))
            .Select((file, cancellationToken) => new
            {
                FileName = System.IO.Path.GetFileNameWithoutExtension(file.Path),
                Content = file.GetText(cancellationToken)?.ToString() ?? string.Empty
            });

        // 3. Analyze compilation to discover step definitions
        var stepMetadataProvider = context.CompilationProvider
            .Select((compilation, cancellationToken) =>
            {
                var analyzer = new StepMethodAnalyzer();
                return analyzer.Analyze(compilation);
            });

        // 4. Combine template, feature files, and step metadata
        var combinedProvider = templateProvider
            .Combine(featureFilesProvider.Collect())
            .Combine(stepMetadataProvider);

        // 5. Generate source for each feature file
        context.RegisterSourceOutput(combinedProvider, (spc, source) =>
        {
            var ((template, featureFiles), stepMetadata) = source;

            // Skip if no template found
            if (string.IsNullOrEmpty(template))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "GHERKIN001",
                        "Missing Mustache Template",
                        "No .mustache template file found in AdditionalFiles. Add a .mustache file to AdditionalFiles in your project.",
                        "Gherkin.Generator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Microsoft.CodeAnalysis.Location.None));
                return;
            }

            // Process each feature file
            foreach (var featureFile in featureFiles)
            {
                try
                {
                    GenerateTestForFeature(spc, featureFile.FileName, featureFile.Content, template, stepMetadata);
                }
                catch (System.Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "GHERKIN002",
                            "Feature Generation Error",
                            $"Error generating test for {featureFile.FileName}: {ex.Message}",
                            "Gherkin.Generator",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        Microsoft.CodeAnalysis.Location.None));
                }
            }
        });
    }

    /// <summary>
    /// Generates test code for a single feature file.
    /// </summary>
    private void GenerateTestForFeature(
        SourceProductionContext context,
        string fileName,
        string featureContent,
        string template,
        StepMetadataCollection stepMetadata)
    {
        // 1. Parse Gherkin feature
        var parser = new Parser();
        GherkinDocument gherkinDocument;

        try
        {
            var reader = new System.IO.StringReader(featureContent);
            gherkinDocument = parser.Parse(reader);
        }
        catch (System.Exception ex)
        {
            // Create diagnostic with full exception message
            // The message template uses {0} placeholder which will be filled with the exception message
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "GHERKIN003",
                    "Gherkin Parse Error",
                    "Error parsing {0}.feature: {1}",
                    "Gherkin.Generator",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Microsoft.CodeAnalysis.Location.None,
                fileName,
                ex.Message));
            return;
        }

        // 2. Convert Gherkin to CRIF
        var converter = new GherkinToCrifConverter(stepMetadata);
        var crif = converter.Convert(gherkinDocument, fileName);

        // 3. Report warnings for unimplemented steps (optional)
        if (crif.Unimplemented.Any())
        {
            var unimplementedSteps = string.Join(", ", crif.Unimplemented.Select(u => $"{u.Keyword} {u.Text}"));
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "GHERKIN004",
                    "Unimplemented Steps",
                    $"Feature '{fileName}' has unimplemented steps: {unimplementedSteps}",
                    "Gherkin.Generator",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Microsoft.CodeAnalysis.Location.None));
        }

        // 4. Generate C# code from CRIF using template
        var generator = new FunctionalTestGenerator();
        var generatedCode = generator.GenerateString(template, crif);

        // 5. Add generated source to compilation
        var sourceText = SourceText.From(generatedCode, Encoding.UTF8);
        context.AddSource($"{fileName}.feature.g.cs", sourceText);
    }
}
