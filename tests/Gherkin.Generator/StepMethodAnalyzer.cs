using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace YoFi.V3.Tests.Generator;

/// <summary>
/// Analyzes C# syntax trees to discover step definition methods and build step metadata.
/// </summary>
/// <remarks>
/// Scans for methods with [Given], [When], or [Then] attributes and extracts:
/// - Normalized keyword (Given/When/Then)
/// - Step text pattern with placeholders
/// - Method name and parameters
/// - Containing class and namespace
/// </remarks>
public class StepMethodAnalyzer
{
    private static readonly string[] StepAttributes = { "Given", "When", "Then", "GivenAttribute", "WhenAttribute", "ThenAttribute" };

    /// <summary>
    /// Analyzes a compilation to discover all step definition methods.
    /// </summary>
    /// <param name="compilation">The compilation containing syntax trees to analyze.</param>
    /// <returns>A StepMetadataCollection containing all discovered step definitions.</returns>
    public StepMetadataCollection Analyze(Compilation compilation)
    {
        var collection = new StepMetadataCollection();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            // Find all class declarations
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                AnalyzeClass(classDecl, semanticModel, collection);
            }
        }

        return collection;
    }

    /// <summary>
    /// Analyzes a single class declaration for step definition methods.
    /// </summary>
    /// <param name="classDecl">The class declaration syntax node.</param>
    /// <param name="semanticModel">Semantic model for symbol resolution.</param>
    /// <param name="collection">Collection to add discovered step metadata to.</param>
    private void AnalyzeClass(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, StepMetadataCollection collection)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null)
            return;

        var className = classSymbol.Name;
        var namespaceName = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        // Find all method declarations in the class
        var methodDeclarations = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var methodDecl in methodDeclarations)
        {
            AnalyzeMethod(methodDecl, semanticModel, className, namespaceName, collection);
        }
    }

    /// <summary>
    /// Analyzes a single method declaration for step attributes.
    /// </summary>
    /// <param name="methodDecl">The method declaration syntax node.</param>
    /// <param name="semanticModel">Semantic model for symbol resolution.</param>
    /// <param name="className">Name of the containing class.</param>
    /// <param name="namespaceName">Namespace of the containing class.</param>
    /// <param name="collection">Collection to add discovered step metadata to.</param>
    private void AnalyzeMethod(
        MethodDeclarationSyntax methodDecl,
        SemanticModel semanticModel,
        string className,
        string namespaceName,
        StepMetadataCollection collection)
    {
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl);
        if (methodSymbol == null)
            return;

        var methodName = methodSymbol.Name;

        // Get all attributes on the method
        foreach (var attributeList in methodDecl.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attributeName = attribute.Name.ToString();

                // Check if this is a step attribute
                if (!IsStepAttribute(attributeName))
                    continue;

                // Normalize keyword (remove "Attribute" suffix if present)
                var normalizedKeyword = NormalizeKeyword(attributeName);

                // Get the step text from the attribute argument
                var stepText = GetStepTextFromAttribute(attribute);
                if (string.IsNullOrEmpty(stepText))
                    continue;

                // Get method parameters
                var parameters = GetMethodParameters(methodSymbol);

                // Create step metadata
                var metadata = new StepMetadata
                {
                    NormalizedKeyword = normalizedKeyword,
                    Text = stepText,
                    Method = methodName,
                    Class = className,
                    Namespace = namespaceName,
                    Parameters = parameters
                };

                collection.Add(metadata);
            }
        }
    }

    /// <summary>
    /// Checks if an attribute name is a step attribute (Given, When, or Then).
    /// </summary>
    private bool IsStepAttribute(string attributeName)
    {
        return StepAttributes.Contains(attributeName);
    }

    /// <summary>
    /// Normalizes a step attribute name to a keyword (Given, When, or Then).
    /// </summary>
    private string NormalizeKeyword(string attributeName)
    {
        // Remove "Attribute" suffix if present
        if (attributeName.EndsWith("Attribute"))
        {
            attributeName = attributeName.Substring(0, attributeName.Length - "Attribute".Length);
        }

        return attributeName; // Given, When, or Then
    }

    /// <summary>
    /// Extracts the step text from a step attribute's first argument.
    /// </summary>
    private string GetStepTextFromAttribute(AttributeSyntax attribute)
    {
        // Step text is the first argument to the attribute
        // Example: [Given("I am logged in")] or [Given("I have {quantity} items")]

        if (attribute.ArgumentList == null || !attribute.ArgumentList.Arguments.Any())
            return string.Empty;

        var firstArg = attribute.ArgumentList.Arguments.First();
        var expression = firstArg.Expression;

        // Handle string literal
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            // Get the token value (without quotes)
            return literal.Token.ValueText;
        }

        return string.Empty;
    }

    /// <summary>
    /// Extracts parameter information from a method symbol.
    /// </summary>
    private List<StepParameter> GetMethodParameters(IMethodSymbol methodSymbol)
    {
        var parameters = new List<StepParameter>();

        foreach (var param in methodSymbol.Parameters)
        {
            // Get the parameter type name
            var typeName = param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            parameters.Add(new StepParameter
            {
                Type = typeName,
                Name = param.Name
            });
        }

        return parameters;
    }
}
