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
        return Convert(feature, string.Empty);
    }

    /// <summary>
    /// Converts a Gherkin feature document to a CRIF object with a specified filename.
    /// </summary>
    /// <param name="feature">Parsed Gherkin feature document.</param>
    /// <param name="fileName">Name of the feature file without extension (e.g., "BankImport").</param>
    /// <returns>Code-Ready Intermediate Form ready for template rendering.</returns>
    public FunctionalTestCrif Convert(GherkinDocument feature, string fileName)
    {
        var crif = new FunctionalTestCrif
        {
            FileName = fileName
        };

        if (feature.Feature != null)
        {
            // Extract feature name
            crif.FeatureName = feature.Feature.Name;

            // Extract feature description lines
            if (feature.Feature.Description != null)
            {
                var lines = feature.Feature.Description.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    crif.DescriptionLines.Add(line.Trim());
                }
            }

            // Process feature tags
            foreach (var tag in feature.Feature.Tags)
            {
                if (tag.Name.StartsWith("@namespace:"))
                {
                    crif.Namespace = tag.Name.Substring("@namespace:".Length);
                }
                else if (tag.Name.StartsWith("@baseclass:"))
                {
                    var baseClassValue = tag.Name.Substring("@baseclass:".Length);
                    // Check if base class includes namespace (contains dots)
                    var lastDotIndex = baseClassValue.LastIndexOf('.');
                    if (lastDotIndex >= 0)
                    {
                        var ns = baseClassValue.Substring(0, lastDotIndex);
                        crif.BaseClass = baseClassValue.Substring(lastDotIndex + 1);
                        if (!crif.Usings.Contains(ns))
                        {
                            crif.Usings.Add(ns);
                        }
                    }
                    else
                    {
                        crif.BaseClass = baseClassValue;
                    }
                }
                else if (tag.Name.StartsWith("@using:"))
                {
                    var usingValue = tag.Name.Substring("@using:".Length);
                    if (!crif.Usings.Contains(usingValue))
                    {
                        crif.Usings.Add(usingValue);
                    }
                }
            }

            // Extract background if present
            var background = feature.Feature.Children.OfType<Background>().FirstOrDefault();
            if (background != null)
            {
                crif.Background = ConvertBackground(background);
                // Track unimplemented steps from background
                TrackUnimplementedSteps(crif, crif.Background.Steps);
            }

            // Process feature children (Rules and Scenarios)
            RuleCrif? defaultRule = null;

            foreach (var child in feature.Feature.Children)
            {
                if (child is Rule rule)
                {
                    var ruleCrif = new RuleCrif
                    {
                        Name = rule.Name,
                        Description = rule.Description ?? string.Empty
                    };

                    foreach (var ruleChild in rule.Children)
                    {
                        if (ruleChild is Scenario scenario)
                        {
                            var scenarioCrif = ConvertScenario(scenario);
                            ruleCrif.Scenarios.Add(scenarioCrif);
                            // Track unimplemented steps
                            TrackUnimplementedSteps(crif, scenarioCrif.Steps);
                        }
                    }

                    crif.Rules.Add(ruleCrif);
                }
                else if (child is Scenario scenarioWithoutRule)
                {
                    // Create default rule if needed
                    if (defaultRule == null)
                    {
                        defaultRule = new RuleCrif
                        {
                            Name = "All scenarios",
                            Description = string.Empty
                        };
                        crif.Rules.Add(defaultRule);
                    }

                    var scenarioCrif = ConvertScenario(scenarioWithoutRule);
                    defaultRule.Scenarios.Add(scenarioCrif);
                    // Track unimplemented steps
                    TrackUnimplementedSteps(crif, scenarioCrif.Steps);
                }
            }
        }

        return crif;
    }

    private BackgroundCrif ConvertBackground(Background background)
    {
        var backgroundCrif = new BackgroundCrif();

        foreach (var step in background.Steps)
        {
            var stepCrif = ConvertStep(step);
            backgroundCrif.Steps.Add(stepCrif);
        }

        return backgroundCrif;
    }

    private ScenarioCrif ConvertScenario(Scenario scenario)
    {
        var scenarioCrif = new ScenarioCrif
        {
            Name = scenario.Name,
            Method = ConvertToMethodName(scenario.Name)
        };

        // Extract scenario description as remarks
        if (!string.IsNullOrWhiteSpace(scenario.Description))
        {
            var lines = scenario.Description.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            scenarioCrif.Remarks = new RemarksCrif
            {
                Lines = lines.Select(l => l.Trim()).ToList()
            };
        }

        // Check for @explicit tag
        foreach (var tag in scenario.Tags)
        {
            if (tag.Name == "@explicit")
            {
                scenarioCrif.ExplicitTag = true;
                break;
            }
        }

        // Handle Scenario Outline examples
        if (scenario.Examples != null && scenario.Examples.Any())
        {
            var examples = scenario.Examples.First();
            var headerRow = examples.TableHeader;
            var dataRows = examples.TableBody;

            // Extract parameters from header
            foreach (var cell in headerRow.Cells)
            {
                scenarioCrif.Parameters.Add(new ParameterCrif
                {
                    Type = "string", // Default to string for unimplemented steps
                    Name = cell.Value,
                    Last = false // Will be set after all are added
                });
            }

            // Set Last flag on final parameter
            if (scenarioCrif.Parameters.Any())
            {
                scenarioCrif.Parameters[scenarioCrif.Parameters.Count - 1].Last = true;
            }

            // Generate test cases from data rows
            foreach (var dataRow in dataRows)
            {
                var values = dataRow.Cells.Select(c => $"\"{c.Value}\"");
                scenarioCrif.TestCases.Add(string.Join(", ", values));
            }
        }

        // Convert steps
        var tableCounter = 1;
        foreach (var step in scenario.Steps)
        {
            var stepCrif = ConvertStep(step);

            // Assign data table variable name if present
            if (stepCrif.DataTable != null)
            {
                stepCrif.DataTable.VariableName = $"table{tableCounter}";
                tableCounter++;
            }

            scenarioCrif.Steps.Add(stepCrif);
        }

        return scenarioCrif;
    }

    private StepCrif ConvertStep(Gherkin.Ast.Step step)
    {
        var stepCrif = new StepCrif
        {
            Keyword = step.Keyword.Trim(),
            Text = step.Text,
            Owner = "this", // Default for unimplemented steps
            Method = string.Empty // Will be set during step matching
        };

        // Convert data table if present
        if (step.Argument is DataTable dataTable)
        {
            stepCrif.DataTable = ConvertDataTable(dataTable);
        }

        return stepCrif;
    }

    private DataTableCrif ConvertDataTable(DataTable dataTable)
    {
        var tableCrif = new DataTableCrif();

        var rows = dataTable.Rows.ToList();
        if (rows.Count > 0)
        {
            // First row is headers
            var headerRow = rows[0];
            var headerCells = headerRow.Cells.ToList();
            for (int i = 0; i < headerCells.Count; i++)
            {
                tableCrif.Headers.Add(new HeaderCellCrif
                {
                    Value = headerCells[i].Value,
                    Last = i == headerCells.Count - 1
                });
            }

            // Remaining rows are data
            for (int rowIdx = 1; rowIdx < rows.Count; rowIdx++)
            {
                var row = rows[rowIdx];
                var cells = row.Cells.ToList();
                var rowCrif = new DataRowCrif
                {
                    Last = rowIdx == rows.Count - 1
                };

                for (int cellIdx = 0; cellIdx < cells.Count; cellIdx++)
                {
                    rowCrif.Cells.Add(new DataCellCrif
                    {
                        Value = cells[cellIdx].Value,
                        Last = cellIdx == cells.Count - 1
                    });
                }

                tableCrif.Rows.Add(rowCrif);
            }
        }

        return tableCrif;
    }

    private string ConvertToMethodName(string name)
    {
        // Convert scenario name to PascalCase method name
        var words = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join("", words.Select(w =>
            char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1) : "")));

        // Remove any remaining special characters
        result = new string(result.Where(c => char.IsLetterOrDigit(c)).ToArray());

        return result;
    }

    private void TrackUnimplementedSteps(FunctionalTestCrif crif, List<StepCrif> steps)
    {
        // Track keyword context for And/But normalization
        string currentKeyword = "Given";

        foreach (var step in steps)
        {
            // Normalize And/But to the current keyword
            var normalizedKeyword = step.Keyword;
            if (normalizedKeyword == "And" || normalizedKeyword == "But")
            {
                normalizedKeyword = currentKeyword;
            }
            else if (normalizedKeyword == "Given" || normalizedKeyword == "When" || normalizedKeyword == "Then")
            {
                currentKeyword = normalizedKeyword;
            }

            // Try to match step with step metadata
            var matchedStep = stepMetadata.FindMatch(normalizedKeyword, step.Text);

            if (matchedStep != null)
            {
                // Step is implemented - populate Owner, Method, and Arguments
                step.Owner = matchedStep.Class;
                step.Method = matchedStep.Method;

                // Add class and namespace to CRIF if not already present
                if (!crif.Classes.Contains(matchedStep.Class))
                {
                    crif.Classes.Add(matchedStep.Class);
                }
                if (!crif.Usings.Contains(matchedStep.Namespace))
                {
                    crif.Usings.Add(matchedStep.Namespace);
                }

                // Extract arguments if step has parameters
                if (matchedStep.Parameters.Count > 0)
                {
                    var arguments = ExtractArguments(matchedStep.Text, step.Text);
                    foreach (var arg in arguments)
                    {
                        step.Arguments.Add(arg);
                    }
                    // Mark last argument
                    if (step.Arguments.Count > 0)
                    {
                        step.Arguments[step.Arguments.Count - 1].Last = true;
                    }
                }
            }
            else
            {
                // Step is unimplemented - track it
                var existingUnimplemented = crif.Unimplemented.FirstOrDefault(u =>
                    u.Text == step.Text && u.Keyword == normalizedKeyword);

                if (existingUnimplemented == null)
                {
                    var unimplementedStep = new UnimplementedStepCrif
                    {
                        Keyword = normalizedKeyword,
                        Text = step.Text,
                        Method = ConvertToMethodName(step.Text),
                        Parameters = []
                    };

                    crif.Unimplemented.Add(unimplementedStep);
                }

                // Set step method name to match the unimplemented method
                step.Method = ConvertToMethodName(step.Text);
            }
        }
    }

    private List<ArgumentCrif> ExtractArguments(string pattern, string text)
    {
        var arguments = new List<ArgumentCrif>();

        // Build a regex pattern from the step definition text using same approach as MatchesWithPlaceholders:
        // 1. Replace {placeholder} with temporary markers BEFORE escaping
        // 2. Escape the pattern for regex special characters
        // 3. Replace markers with capture groups AFTER escaping

        // First, replace placeholders with temporary markers BEFORE escaping
        var regexPattern = System.Text.RegularExpressions.Regex.Replace(
            pattern,
            @"\{[^}]+\}",  // Match {placeholder} pattern
            "<<<PLACEHOLDER>>>"  // Temporary placeholder marker
        );

        // Now escape the pattern for regex
        regexPattern = System.Text.RegularExpressions.Regex.Escape(regexPattern);

        // Replace our markers with capture groups (note: using capturing group, not non-capturing)
        regexPattern = regexPattern.Replace(
            "<<<PLACEHOLDER>>>",
            @"((?:""[^""]*""|\S+))"  // Capture group that matches quoted or unquoted values
        );

        // Add anchors for full string match
        regexPattern = "^" + regexPattern + "$";

        try
        {
            var regex = new System.Text.RegularExpressions.Regex(
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            var match = regex.Match(text);

            if (match.Success)
            {
                // Groups[0] is the entire match, Groups[1..n] are capture groups
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    arguments.Add(new ArgumentCrif
                    {
                        Value = match.Groups[i].Value,
                        Last = false
                    });
                }
            }
        }
        catch
        {
            // Fallback: couldn't extract arguments
        }

        return arguments;
    }
}

/// <summary>
/// Collection of step definition metadata extracted from step classes.
/// </summary>
public class StepMetadataCollection
{
    private readonly List<StepMetadata> _steps = new();

    /// <summary>
    /// Adds step metadata to the collection.
    /// </summary>
    /// <param name="metadata">Step definition metadata to add.</param>
    public void Add(StepMetadata metadata)
    {
        _steps.Add(metadata);
    }

    /// <summary>
    /// Adds multiple step metadata items to the collection.
    /// </summary>
    /// <param name="metadataItems">Collection of step definition metadata to add.</param>
    public void AddRange(IEnumerable<StepMetadata> metadataItems)
    {
        _steps.AddRange(metadataItems);
    }

    /// <summary>
    /// Finds a step definition matching the given Gherkin step.
    /// </summary>
    /// <param name="normalizedKeyword">Normalized keyword (Given, When, or Then).</param>
    /// <param name="stepText">Step text from Gherkin scenario.</param>
    /// <returns>Matching step metadata, or null if no match found.</returns>
    public StepMetadata? FindMatch(string normalizedKeyword, string stepText)
    {
        // Filter by keyword first
        var candidates = _steps.Where(s =>
            s.NormalizedKeyword.Equals(normalizedKeyword, StringComparison.OrdinalIgnoreCase));

        // Try to find exact match first (no parameters)
        foreach (var candidate in candidates.Where(c => c.Parameters.Count == 0))
        {
            if (candidate.Text.Equals(stepText, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        // Try to find match with placeholders
        foreach (var candidate in candidates.Where(c => c.Parameters.Count > 0))
        {
            if (MatchesWithPlaceholders(candidate.Text, stepText))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool MatchesWithPlaceholders(string pattern, string text)
    {
        // Build a regex pattern from the step definition text
        // Replace {placeholder} with a pattern that matches:
        // - Single words (no spaces): \S+
        // - Quoted phrases (can contain spaces): "[^"]*"
        // The pattern should match either quoted text OR non-whitespace

        // First, replace placeholders in the original pattern BEFORE escaping
        var regexPattern = System.Text.RegularExpressions.Regex.Replace(
            pattern,
            @"\{[^}]+\}",  // Match {placeholder} pattern
            "<<<PLACEHOLDER>>>"  // Temporary placeholder marker
        );

        // Now escape the pattern for regex
        regexPattern = System.Text.RegularExpressions.Regex.Escape(regexPattern);

        // Replace our markers with the actual regex pattern
        regexPattern = regexPattern.Replace(
            "<<<PLACEHOLDER>>>",
            @"(?:""[^""]*""|\S+)"
        );

        // Add anchors for full string match
        regexPattern = "^" + regexPattern + "$";

        try
        {
            var regex = new System.Text.RegularExpressions.Regex(
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            return regex.IsMatch(text);
        }
        catch
        {
            return false;
        }
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
