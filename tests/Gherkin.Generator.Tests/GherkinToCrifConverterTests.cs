using Gherkin;
using Gherkin.Ast;
using NUnit.Framework;

namespace YoFi.V3.Tests.Generator;

/// <summary>
/// Tests for converting Gherkin feature documents to CRIF without step matching.
/// </summary>
[TestFixture]
public class GherkinToCrifConverterTests
{
    private GherkinToCrifConverter _converter = null!;
    private StepMetadataCollection _emptySteps = null!;

    [SetUp]
    public void SetUp()
    {
        // Given: An empty step metadata collection (no step matching)
        _emptySteps = new StepMetadataCollection();
        _converter = new GherkinToCrifConverter(_emptySteps);
    }

    [Test]
    public void Convert_MinimalFeature_ExtractsFeatureName()
    {
        // Given: A minimal Gherkin feature
        var gherkin = """
            Feature: Transaction Management
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Feature name should be extracted
        Assert.That(crif.FeatureName, Is.EqualTo("Transaction Management"));
    }

    [Test]
    public void Convert_FeatureWithDescription_ExtractsDescription()
    {
        // Given: A feature with description
        var gherkin = """
            Feature: Transaction Management
              As a user
              I want to manage transactions
              So that I can track my finances
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Description lines should be extracted
        Assert.That(crif.DescriptionLines, Has.Count.EqualTo(3));
        Assert.That(crif.DescriptionLines[0], Is.EqualTo("As a user"));
        Assert.That(crif.DescriptionLines[1], Is.EqualTo("I want to manage transactions"));
        Assert.That(crif.DescriptionLines[2], Is.EqualTo("So that I can track my finances"));
    }

    [Test]
    public void Convert_FeatureWithNamespaceTag_ExtractsNamespace()
    {
        // Given: A feature with @namespace tag
        var gherkin = """
            @namespace:YoFi.V3.Tests.Functional.Features
            Feature: Transaction Management
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Namespace should be extracted from tag
        Assert.That(crif.Namespace, Is.EqualTo("YoFi.V3.Tests.Functional.Features"));
    }

    [Test]
    public void Convert_FeatureWithBaseClassTag_ExtractsBaseClass()
    {
        // Given: A feature with @baseclass tag
        var gherkin = """
            @baseclass:FunctionalTestBase
            Feature: Transaction Management
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Base class should be extracted from tag
        Assert.That(crif.BaseClass, Is.EqualTo("FunctionalTestBase"));
    }

    [Test]
    public void Convert_FeatureWithBaseClassAndNamespace_SplitsNamespaceAndClass()
    {
        // Given: A feature with @baseclass tag including namespace
        var gherkin = """
            @baseclass:YoFi.V3.Tests.Functional.FunctionalTestBase
            Feature: Transaction Management
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Base class should have namespace stripped
        Assert.That(crif.BaseClass, Is.EqualTo("FunctionalTestBase"));

        // And: Namespace should be added to Usings
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional"));
    }

    [Test]
    public void Convert_FeatureWithUsingTag_AddsUsingToList()
    {
        // Given: A feature with @using tag
        var gherkin = """
            @using:System.Collections.Generic
            Feature: Transaction Management
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Using should be added to Usings list
        Assert.That(crif.Usings, Contains.Item("System.Collections.Generic"));
    }

    [Test]
    public void Convert_FeatureWithMultipleUsingTags_AddsAllUsingsToList()
    {
        // Given: A feature with multiple @using tags
        var gherkin = """
            @using:System.Collections.Generic
            @using:System.Linq
            Feature: Transaction Management
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: All usings should be added to Usings list
        Assert.That(crif.Usings, Contains.Item("System.Collections.Generic"));
        Assert.That(crif.Usings, Contains.Item("System.Linq"));
    }

    [Test]
    public void Convert_FeatureWithScenario_ExtractsScenarioName()
    {
        // Given: A feature with a scenario
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
              When I create a transaction
              Then the transaction is saved
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Scenario name should be extracted
        Assert.That(crif.Rules[0].Scenarios, Has.Count.EqualTo(1));
        Assert.That(crif.Rules[0].Scenarios[0].Name, Is.EqualTo("Create new transaction"));
    }

    [Test]
    public void Convert_FeatureWithScenarioWithoutRule_CreatesDefaultRule()
    {
        // Given: A feature with a scenario but no rule
        var gherkin = """
            Feature: Transaction Management

            Scenario: Create new transaction
              Given I am logged in
              When I create a transaction
              Then the transaction is saved
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Should create a default rule named "All scenarios"
        Assert.That(crif.Rules, Has.Count.EqualTo(1));
        Assert.That(crif.Rules[0].Name, Is.EqualTo("All scenarios"));

        // And: Scenario should be under that rule
        Assert.That(crif.Rules[0].Scenarios, Has.Count.EqualTo(1));
        Assert.That(crif.Rules[0].Scenarios[0].Name, Is.EqualTo("Create new transaction"));
    }

    [Test]
    public void Convert_FeatureWithScenario_GeneratesMethodName()
    {
        // Given: A feature with a scenario
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Method name should be PascalCase version of scenario name
        Assert.That(crif.Rules[0].Scenarios[0].Method, Is.EqualTo("CreateNewTransaction"));
    }

    [Test]
    public void Convert_ScenarioWithExplicitTag_SetsExplicitFlag()
    {
        // Given: A scenario with @explicit tag
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            @explicit
            Scenario: Create new transaction
              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: ExplicitTag should be set to true
        Assert.That(crif.Rules[0].Scenarios[0].ExplicitTag, Is.True);
    }

    [Test]
    public void Convert_FeatureWithRule_ExtractsRuleName()
    {
        // Given: A feature with a rule
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Rule name should be extracted
        Assert.That(crif.Rules, Has.Count.EqualTo(1));
        Assert.That(crif.Rules[0].Name, Is.EqualTo("Transaction Creation"));
    }

    [Test]
    public void Convert_FeatureWithBackground_ExtractsBackgroundSteps()
    {
        // Given: A feature with background
        var gherkin = """
            Feature: Transaction Management

            Background:
              Given I am logged in
              And I have a workspace

            Rule: Transaction Creation

            Scenario: Create new transaction
              When I create a transaction
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Background should be extracted with steps
        Assert.That(crif.Background, Is.Not.Null);
        Assert.That(crif.Background!.Steps, Has.Count.EqualTo(2));
        Assert.That(crif.Background.Steps[0].Text, Is.EqualTo("I am logged in"));
        Assert.That(crif.Background.Steps[1].Text, Is.EqualTo("I have a workspace"));
    }

    [Test]
    public void Convert_BackgroundSteps_ExtractsKeywords()
    {
        // Given: A feature with background containing different keywords
        var gherkin = """
            Feature: Transaction Management

            Background:
              Given I am logged in
              And I have a workspace
              But I have no transactions

            Rule: Transaction Creation

            Scenario: Create new transaction
              When I create a transaction
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Each step should preserve its original keyword
        Assert.That(crif.Background!.Steps[0].Keyword, Is.EqualTo("Given"));
        Assert.That(crif.Background!.Steps[1].Keyword, Is.EqualTo("And"));
        Assert.That(crif.Background!.Steps[2].Keyword, Is.EqualTo("But"));
    }

    [Test]
    public void Convert_ScenarioSteps_ExtractsStepText()
    {
        // Given: A scenario with steps
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
              When I create a transaction
              Then the transaction is saved
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Step text should be extracted
        var steps = crif.Rules[0].Scenarios[0].Steps;
        Assert.That(steps, Has.Count.EqualTo(3));
        Assert.That(steps[0].Text, Is.EqualTo("I am logged in"));
        Assert.That(steps[1].Text, Is.EqualTo("I create a transaction"));
        Assert.That(steps[2].Text, Is.EqualTo("the transaction is saved"));
    }

    [Test]
    public void Convert_ScenarioSteps_ExtractsKeywords()
    {
        // Given: A scenario with steps
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
              When I create a transaction
              Then the transaction is saved
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Keywords should be extracted
        var steps = crif.Rules[0].Scenarios[0].Steps;
        Assert.That(steps[0].Keyword, Is.EqualTo("Given"));
        Assert.That(steps[1].Keyword, Is.EqualTo("When"));
        Assert.That(steps[2].Keyword, Is.EqualTo("Then"));
    }

    [Test]
    public void Convert_StepWithDataTable_ExtractsDataTable()
    {
        // Given: A step with a data table
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Fields

            Scenario: Transaction has required fields
              Given the following fields exist:
                | Field  | Type   |
                | Date   | date   |
                | Amount | number |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Data table should be extracted
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.DataTable, Is.Not.Null);
    }

    [Test]
    public void Convert_DataTable_ExtractsHeaders()
    {
        // Given: A step with a data table
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Fields

            Scenario: Transaction has required fields
              Given the following fields exist:
                | Field  | Type   |
                | Date   | date   |
                | Amount | number |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Headers should be extracted
        var table = crif.Rules[0].Scenarios[0].Steps[0].DataTable!;
        Assert.That(table.Headers, Has.Count.EqualTo(2));
        Assert.That(table.Headers[0].Value, Is.EqualTo("Field"));
        Assert.That(table.Headers[1].Value, Is.EqualTo("Type"));
    }

    [Test]
    public void Convert_DataTable_ExtractsRows()
    {
        // Given: A step with a data table
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Fields

            Scenario: Transaction has required fields
              Given the following fields exist:
                | Field  | Type   |
                | Date   | date   |
                | Amount | number |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Data rows should be extracted
        var table = crif.Rules[0].Scenarios[0].Steps[0].DataTable!;
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Rows[0].Cells[0].Value, Is.EqualTo("Date"));
        Assert.That(table.Rows[0].Cells[1].Value, Is.EqualTo("date"));
        Assert.That(table.Rows[1].Cells[0].Value, Is.EqualTo("Amount"));
        Assert.That(table.Rows[1].Cells[1].Value, Is.EqualTo("number"));
    }

    [Test]
    public void Convert_DataTable_SetsLastFlagsOnHeaders()
    {
        // Given: A step with a data table
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Fields

            Scenario: Transaction has required fields
              Given the following fields exist:
                | Field  | Type   |
                | Date   | date   |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Last flag should be set on final header only
        var headers = crif.Rules[0].Scenarios[0].Steps[0].DataTable!.Headers;
        Assert.That(headers[0].Last, Is.False);
        Assert.That(headers[1].Last, Is.True);
    }

    [Test]
    public void Convert_DataTable_SetsLastFlagsOnRows()
    {
        // Given: A step with a data table
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Fields

            Scenario: Transaction has required fields
              Given the following fields exist:
                | Field  | Type   |
                | Date   | date   |
                | Amount | number |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Last flag should be set on final row only
        var rows = crif.Rules[0].Scenarios[0].Steps[0].DataTable!.Rows;
        Assert.That(rows[0].Last, Is.False);
        Assert.That(rows[1].Last, Is.True);
    }

    [Test]
    public void Convert_DataTable_SetsLastFlagsOnCells()
    {
        // Given: A step with a data table
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Fields

            Scenario: Transaction has required fields
              Given the following fields exist:
                | Field  | Type   |
                | Date   | date   |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Last flag should be set on final cell in each row
        var cells = crif.Rules[0].Scenarios[0].Steps[0].DataTable!.Rows[0].Cells;
        Assert.That(cells[0].Last, Is.False);
        Assert.That(cells[1].Last, Is.True);
    }

    [Test]
    public void Convert_DataTable_GeneratesVariableName()
    {
        // Given: A step with a data table
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Fields

            Scenario: Transaction has required fields
              Given the following fields exist:
                | Field  | Type   |
                | Date   | date   |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Variable name should be generated
        var table = crif.Rules[0].Scenarios[0].Steps[0].DataTable!;
        Assert.That(table.VariableName, Is.EqualTo("table1"));
    }

    [Test]
    public void Convert_MultipleDataTablesInScenario_GeneratesUniqueVariableNames()
    {
        // Given: A scenario with multiple data tables
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Fields

            Scenario: Transaction has multiple tables
              Given the following fields exist:
                | Field  | Type   |
                | Date   | date   |
              And the following values exist:
                | Value  | Amount |
                | First  | 100    |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Each table should have a unique variable name
        var steps = crif.Rules[0].Scenarios[0].Steps;
        Assert.That(steps[0].DataTable!.VariableName, Is.EqualTo("table1"));
        Assert.That(steps[1].DataTable!.VariableName, Is.EqualTo("table2"));
    }

    [Test]
    public void Convert_ScenarioOutline_ExtractsParameters()
    {
        // Given: A scenario outline with examples
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario Outline: Create transaction with <amount>
              Given I have <amount> dollars

            Examples:
              | amount |
              | 100    |
              | 200    |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Parameters should be extracted from scenario outline
        var scenario = crif.Rules[0].Scenarios[0];
        Assert.That(scenario.Parameters, Has.Count.EqualTo(1));
        Assert.That(scenario.Parameters[0].Name, Is.EqualTo("amount"));
        Assert.That(scenario.Parameters[0].Type, Is.EqualTo("string"));
    }

    [Test]
    public void Convert_ScenarioOutline_GeneratesTestCases()
    {
        // Given: A scenario outline with examples
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario Outline: Create transaction with <amount>
              Given I have <amount> dollars

            Examples:
              | amount |
              | 100    |
              | 200    |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Test cases should be generated from examples
        var scenario = crif.Rules[0].Scenarios[0];
        Assert.That(scenario.TestCases, Has.Count.EqualTo(2));
        Assert.That(scenario.TestCases[0], Is.EqualTo("\"100\""));
        Assert.That(scenario.TestCases[1], Is.EqualTo("\"200\""));
    }

    [Test]
    public void Convert_ScenarioOutlineWithMultipleParameters_GeneratesMultiArgumentTestCases()
    {
        // Given: A scenario outline with multiple parameters
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario Outline: Create transaction
              Given I have <amount> dollars in <account>

            Examples:
              | amount | account |
              | 100    | Savings |
              | 200    | Checking|
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Test cases should include all parameters
        var scenario = crif.Rules[0].Scenarios[0];
        Assert.That(scenario.TestCases, Has.Count.EqualTo(2));
        Assert.That(scenario.TestCases[0], Is.EqualTo("\"100\", \"Savings\""));
        Assert.That(scenario.TestCases[1], Is.EqualTo("\"200\", \"Checking\""));
    }

    [Test]
    public void Convert_EmptyStepCollection_AllStepsUnimplemented()
    {
        // Given: A feature with steps and empty step collection
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
              When I create a transaction
              Then the transaction is saved
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF with empty step collection
        var crif = _converter.Convert(feature);

        // Then: All steps should be in unimplemented list
        Assert.That(crif.Unimplemented, Has.Count.EqualTo(3));
        Assert.That(crif.Unimplemented[0].Text, Is.EqualTo("I am logged in"));
        Assert.That(crif.Unimplemented[1].Text, Is.EqualTo("I create a transaction"));
        Assert.That(crif.Unimplemented[2].Text, Is.EqualTo("the transaction is saved"));
    }

    [Test]
    public void Convert_UnimplementedSteps_NormalizesKeywords()
    {
        // Given: A feature with And and But steps
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
              And I have a workspace
              But I have no transactions
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Unimplemented steps should have normalized keywords (all Given)
        Assert.That(crif.Unimplemented[0].Keyword, Is.EqualTo("Given"));
        Assert.That(crif.Unimplemented[1].Keyword, Is.EqualTo("Given"));
        Assert.That(crif.Unimplemented[2].Keyword, Is.EqualTo("Given"));
    }

    [Test]
    public void Convert_UnimplementedSteps_GeneratesMethodNames()
    {
        // Given: A feature with steps
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
              When I create a transaction
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Method names should be generated in PascalCase
        Assert.That(crif.Unimplemented[0].Method, Is.EqualTo("IAmLoggedIn"));
        Assert.That(crif.Unimplemented[1].Method, Is.EqualTo("ICreateATransaction"));
    }

    [Test]
    public void Convert_UnimplementedSteps_SetsOwnerToThis()
    {
        // Given: A feature with unimplemented steps
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Step owner should be set to "this" for unimplemented steps
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.Owner, Is.EqualTo("this"));
    }

    [Test]
    public void Convert_ScenarioWithDescription_ExtractsRemarks()
    {
        // Given: A scenario with multi-line description
        var gherkin = """
            Feature: Transaction Management

            Rule: Transaction Creation

            Scenario: Create new transaction
              This is a detailed description
              of what the scenario does

              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = _converter.Convert(feature);

        // Then: Remarks should be extracted from scenario description
        var scenario = crif.Rules[0].Scenarios[0];
        Assert.That(scenario.Remarks, Is.Not.Null);
        Assert.That(scenario.Remarks!.Lines, Has.Count.EqualTo(2));
        Assert.That(scenario.Remarks.Lines[0], Is.EqualTo("This is a detailed description"));
        Assert.That(scenario.Remarks.Lines[1], Is.EqualTo("of what the scenario does"));
    }

    [Test]
    public void Convert_WithFileName_SetsFileNameProperty()
    {
        // Given: A minimal Gherkin feature
        var gherkin = """
            Feature: Transaction Management
            """;
        var feature = ParseGherkin(gherkin);

        // And: A filename
        var fileName = "BankImport";

        // When: Feature is converted to CRIF with filename
        var crif = _converter.Convert(feature, fileName);

        // Then: FileName property should be set
        Assert.That(crif.FileName, Is.EqualTo("BankImport"));
    }

    /// <summary>
    /// Helper method to parse Gherkin text into a GherkinDocument.
    /// </summary>
    private static GherkinDocument ParseGherkin(string gherkinText)
    {
        var parser = new Parser();
        var reader = new StringReader(gherkinText);
        return parser.Parse(reader);
    }
}
