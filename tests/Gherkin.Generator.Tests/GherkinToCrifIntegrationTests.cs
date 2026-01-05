using Gherkin;
using Gherkin.Ast;
using NUnit.Framework;

namespace YoFi.V3.Tests.Generator;

/// <summary>
/// Integration tests for Gherkin-to-CRIF conversion with step matching.
/// Tests that matched steps are properly emitted to CRIF with correct Owner, Method, and Arguments.
/// </summary>
[TestFixture]
public class GherkinToCrifIntegrationTests
{
    [Test]
    public void Convert_WithMatchedStep_EmitsOwnerAndMethod()
    {
        // Given: A step metadata collection with a step definition
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I am logged in",
            Method = "IAmLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = []
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with that step
        var gherkin = """
            Feature: Authentication

            Rule: Login

            Scenario: User logs in
              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Step should have Owner and Method from matched step
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.Owner, Is.EqualTo("AuthSteps"));
        Assert.That(step.Method, Is.EqualTo("IAmLoggedIn"));
    }

    [Test]
    public void Convert_WithMatchedStep_AddsClassToClassesList()
    {
        // Given: A step metadata collection
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I am logged in",
            Method = "IAmLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = []
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature
        var gherkin = """
            Feature: Authentication

            Scenario: User logs in
              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Class should be added to Classes list
        Assert.That(crif.Classes, Contains.Item("AuthSteps"));
    }

    [Test]
    public void Convert_WithMatchedStep_AddsNamespaceToUsings()
    {
        // Given: A step metadata collection
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I am logged in",
            Method = "IAmLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = []
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature
        var gherkin = """
            Feature: Authentication

            Scenario: User logs in
              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Namespace should be added to Usings
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps"));
    }

    [Test]
    public void Convert_WithUnmatchedStep_StaysInUnimplementedList()
    {
        // Given: An empty step metadata collection
        var stepMetadata = new StepMetadataCollection();
        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature
        var gherkin = """
            Feature: Authentication

            Scenario: User logs in
              Given I am logged in
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Step should be in Unimplemented list
        Assert.That(crif.Unimplemented, Has.Count.EqualTo(1));
        Assert.That(crif.Unimplemented[0].Text, Is.EqualTo("I am logged in"));
        Assert.That(crif.Unimplemented[0].Keyword, Is.EqualTo("Given"));

        // And: Step should have Owner="this"
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.Owner, Is.EqualTo("this"));
    }

    [Test]
    public void Convert_WithParameterizedStep_ExtractsArguments()
    {
        // Given: A step with parameters
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have {quantity} dollars",
            Method = "IHaveDollars",
            Class = "AccountSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "int", Name = "quantity" }]
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with that step
        var gherkin = """
            Feature: Accounts

            Scenario: User has money
              Given I have 100 dollars
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Step should have arguments extracted
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.Arguments, Has.Count.EqualTo(1));
        Assert.That(step.Arguments[0].Value, Is.EqualTo("100"));
        Assert.That(step.Arguments[0].Last, Is.True);
    }

    [Test]
    public void Convert_WithMultipleParameters_ExtractsAllArguments()
    {
        // Given: A step with multiple parameters
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have {quantity} dollars in {account}",
            Method = "IHaveDollarsIn",
            Class = "AccountSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [
                new StepParameter { Type = "int", Name = "quantity" },
                new StepParameter { Type = "string", Name = "account" }
            ]
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature
        var gherkin = """
            Feature: Accounts

            Scenario: User has money
              Given I have 100 dollars in Savings
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Step should have all arguments
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.Arguments, Has.Count.EqualTo(2));
        Assert.That(step.Arguments[0].Value, Is.EqualTo("100"));
        Assert.That(step.Arguments[0].Last, Is.False);
        Assert.That(step.Arguments[1].Value, Is.EqualTo("Savings"));
        Assert.That(step.Arguments[1].Last, Is.True);
    }

    [Test]
    public void Convert_WithQuotedArgument_ExtractsWithoutQuotes()
    {
        // Given: A step with a parameter
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have an account named {account}",
            Method = "IHaveAnAccountNamed",
            Class = "AccountSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "string", Name = "account" }]
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with quoted value
        var gherkin = """
            Feature: Accounts

            Scenario: User has account
              Given I have an account named "Ski Village"
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Argument should be extracted without quotes
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.Arguments, Has.Count.EqualTo(1));
        Assert.That(step.Arguments[0].Value, Is.EqualTo("\"Ski Village\""));
    }

    [Test]
    public void Convert_WithMultipleStepsFromSameClass_AddsClassOnce()
    {
        // Given: Multiple steps from same class
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "When",
                Text = "I log out",
                Method = "ILogOut",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with both steps
        var gherkin = """
            Feature: Authentication

            Scenario: User session
              Given I am logged in
              When I log out
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Class should appear only once
        Assert.That(crif.Classes.Count(c => c == "AuthSteps"), Is.EqualTo(1));
    }

    [Test]
    public void Convert_WithStepsFromDifferentClasses_AddsAllClasses()
    {
        // Given: Steps from different classes
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "When",
                Text = "I create a transaction",
                Method = "ICreateATransaction",
                Class = "TransactionSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature
        var gherkin = """
            Feature: Application

            Scenario: User workflow
              Given I am logged in
              When I create a transaction
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Both classes should be added
        Assert.That(crif.Classes, Contains.Item("AuthSteps"));
        Assert.That(crif.Classes, Contains.Item("TransactionSteps"));
    }

    [Test]
    public void Convert_WithMatchedAndUnmatchedSteps_HandlesCorrectly()
    {
        // Given: Partial step metadata
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I am logged in",
            Method = "IAmLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = []
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with matched and unmatched steps
        var gherkin = """
            Feature: Mixed

            Scenario: Partial implementation
              Given I am logged in
              When I do something unimplemented
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Matched step should use step class
        var step1 = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step1.Owner, Is.EqualTo("AuthSteps"));
        Assert.That(step1.Method, Is.EqualTo("IAmLoggedIn"));

        // And: Unmatched step should use "this"
        var step2 = crif.Rules[0].Scenarios[0].Steps[1];
        Assert.That(step2.Owner, Is.EqualTo("this"));

        // And: Only unmatched step in Unimplemented list
        Assert.That(crif.Unimplemented, Has.Count.EqualTo(1));
        Assert.That(crif.Unimplemented[0].Text, Is.EqualTo("I do something unimplemented"));
    }

    [Test]
    public void Convert_WithStepsFromDifferentNamespaces_AddsAllNamespaces()
    {
        // Given: Steps from different namespaces
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "When",
                Text = "I navigate to the page",
                Method = "INavigateToThePage",
                Class = "NavigationSteps",
                Namespace = "YoFi.V3.Tests.Functional.Pages",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature
        var gherkin = """
            Feature: Application

            Scenario: User workflow
              Given I am logged in
              When I navigate to the page
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Both namespaces should be added to Usings
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps"));
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Pages"));
    }

    [Test]
    public void Convert_WithMultipleStepsFromSameNamespace_AddsNamespaceOnce()
    {
        // Given: Multiple steps from same namespace
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "When",
                Text = "I create a transaction",
                Method = "ICreateATransaction",
                Class = "TransactionSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with both steps
        var gherkin = """
            Feature: Application

            Scenario: User workflow
              Given I am logged in
              When I create a transaction
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Namespace should appear only once in Usings
        Assert.That(crif.Usings.Count(ns => ns == "YoFi.V3.Tests.Functional.Steps"), Is.EqualTo(1));
    }

    [Test]
    public void Convert_WithAndStep_AddsClassAndNamespaceCorrectly()
    {
        // Given: Steps with Given keyword
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I have a workspace",
                Method = "IHaveAWorkspace",
                Class = "WorkspaceSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps.Setup",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with And step
        var gherkin = """
            Feature: Setup

            Scenario: Initial setup
              Given I am logged in
              And I have a workspace
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Both classes should be added
        Assert.That(crif.Classes, Contains.Item("AuthSteps"));
        Assert.That(crif.Classes, Contains.Item("WorkspaceSteps"));

        // And: Both namespaces should be added
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps"));
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps.Setup"));
    }

    [Test]
    public void Convert_WithMultipleAndSteps_AddsAllClassesAndNamespaces()
    {
        // Given: Multiple Given steps from different classes/namespaces
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps.Auth",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I have a workspace",
                Method = "IHaveAWorkspace",
                Class = "WorkspaceSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps.Setup",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I have permissions",
                Method = "IHavePermissions",
                Class = "PermissionSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps.Auth",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with multiple And steps
        var gherkin = """
            Feature: Setup

            Scenario: Complete setup
              Given I am logged in
              And I have a workspace
              And I have permissions
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: All three classes should be added
        Assert.That(crif.Classes, Contains.Item("AuthSteps"));
        Assert.That(crif.Classes, Contains.Item("WorkspaceSteps"));
        Assert.That(crif.Classes, Contains.Item("PermissionSteps"));

        // And: Both unique namespaces should be added (Auth namespace used twice, but only appears once)
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps.Auth"));
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps.Setup"));
        Assert.That(crif.Usings.Count(ns => ns == "YoFi.V3.Tests.Functional.Steps.Auth"), Is.EqualTo(1));
    }

    [Test]
    public void Convert_WithBackground_AddsBackgroundStepClassesAndNamespaces()
    {
        // Given: Background steps
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps.Auth",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I have a workspace",
                Method = "IHaveAWorkspace",
                Class = "WorkspaceSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps.Setup",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with background
        var gherkin = """
            Feature: Application

            Background:
              Given I am logged in
              And I have a workspace

            Scenario: Do something
              When I do something
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Background step classes should be in Classes list
        Assert.That(crif.Classes, Contains.Item("AuthSteps"));
        Assert.That(crif.Classes, Contains.Item("WorkspaceSteps"));

        // And: Background step namespaces should be in Usings
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps.Auth"));
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps.Setup"));
    }

    [Test]
    public void Convert_WithAndStep_PreservesAndKeywordInCrif()
    {
        // Given: Steps with Given keyword
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I have a workspace",
                Method = "IHaveAWorkspace",
                Class = "WorkspaceSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps.Setup",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with And step
        var gherkin = """
            Feature: Setup

            Scenario: Initial setup
              Given I am logged in
              And I have a workspace
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: First step should have "Given" keyword
        var step1 = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step1.Keyword, Is.EqualTo("Given"));

        // And: Second step should preserve "And" keyword in CRIF
        var step2 = crif.Rules[0].Scenarios[0].Steps[1];
        Assert.That(step2.Keyword, Is.EqualTo("And"));

        // And: Both steps should be matched correctly
        Assert.That(step1.Owner, Is.EqualTo("AuthSteps"));
        Assert.That(step2.Owner, Is.EqualTo("WorkspaceSteps"));
    }

    [Test]
    public void Convert_WithThenAndAndSteps_PreservesAndKeywordsAndMatchesCorrectly()
    {
        // Given: Steps with Then keyword
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Then",
                Text = "the user should be logged in",
                Method = "TheUserShouldBeLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "Then",
                Text = "the session should be active",
                Method = "TheSessionShouldBeActive",
                Class = "SessionSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "Then",
                Text = "the user should have access",
                Method = "TheUserShouldHaveAccess",
                Class = "AccessSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with Then/And/And steps
        var gherkin = """
            Feature: Authentication

            Scenario: User verification
              Given I am on the login page
              When I log in
              Then the user should be logged in
              And the session should be active
              And the user should have access
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: First assertion step should have "Then" keyword
        var step1 = crif.Rules[0].Scenarios[0].Steps[2];
        Assert.That(step1.Keyword, Is.EqualTo("Then"));
        Assert.That(step1.Owner, Is.EqualTo("AuthSteps"));
        Assert.That(step1.Method, Is.EqualTo("TheUserShouldBeLoggedIn"));

        // And: Second step should preserve "And" keyword but match as "Then"
        var step2 = crif.Rules[0].Scenarios[0].Steps[3];
        Assert.That(step2.Keyword, Is.EqualTo("And"));
        Assert.That(step2.Owner, Is.EqualTo("SessionSteps"));
        Assert.That(step2.Method, Is.EqualTo("TheSessionShouldBeActive"));

        // And: Third step should also preserve "And" keyword and match as "Then"
        var step3 = crif.Rules[0].Scenarios[0].Steps[4];
        Assert.That(step3.Keyword, Is.EqualTo("And"));
        Assert.That(step3.Owner, Is.EqualTo("AccessSteps"));
        Assert.That(step3.Method, Is.EqualTo("TheUserShouldHaveAccess"));

        // And: All three classes should be in the classes list
        Assert.That(crif.Classes, Contains.Item("AuthSteps"));
        Assert.That(crif.Classes, Contains.Item("SessionSteps"));
        Assert.That(crif.Classes, Contains.Item("AccessSteps"));
    }

    [Test]
    public void Convert_WithMixedMatchedAndUnmatchedSteps_IncludesAllStepsInCrif()
    {
        // Given: Only Then step definitions
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Then",
                Text = "the user should be logged in",
                Method = "TheUserShouldBeLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            },
            new StepMetadata
            {
                NormalizedKeyword = "Then",
                Text = "the session should be active",
                Method = "TheSessionShouldBeActive",
                Class = "SessionSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with Given/When/Then/And steps
        var gherkin = """
            Feature: Authentication

            Scenario: User verification
              Given I am on the login page
              When I log in
              Then the user should be logged in
              And the session should be active
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: All 4 steps should be in the CRIF
        Assert.That(crif.Rules[0].Scenarios[0].Steps, Has.Count.EqualTo(4));

        // And: First Given step should be unimplemented
        var givenStep = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(givenStep.Keyword, Is.EqualTo("Given"));
        Assert.That(givenStep.Text, Is.EqualTo("I am on the login page"));
        Assert.That(givenStep.Owner, Is.EqualTo("this"));

        // And: When step should be unimplemented
        var whenStep = crif.Rules[0].Scenarios[0].Steps[1];
        Assert.That(whenStep.Keyword, Is.EqualTo("When"));
        Assert.That(whenStep.Text, Is.EqualTo("I log in"));
        Assert.That(whenStep.Owner, Is.EqualTo("this"));

        // And: Then step should be matched
        var thenStep = crif.Rules[0].Scenarios[0].Steps[2];
        Assert.That(thenStep.Keyword, Is.EqualTo("Then"));
        Assert.That(thenStep.Owner, Is.EqualTo("AuthSteps"));
        Assert.That(thenStep.Method, Is.EqualTo("TheUserShouldBeLoggedIn"));

        // And: And step should preserve "And" keyword and be matched
        var andStep = crif.Rules[0].Scenarios[0].Steps[3];
        Assert.That(andStep.Keyword, Is.EqualTo("And"));
        Assert.That(andStep.Owner, Is.EqualTo("SessionSteps"));
        Assert.That(andStep.Method, Is.EqualTo("TheSessionShouldBeActive"));

        // And: Unimplemented steps should be tracked
        Assert.That(crif.Unimplemented, Has.Count.EqualTo(2));
        Assert.That(crif.Unimplemented.Any(u => u.Keyword == "Given" && u.Text == "I am on the login page"), Is.True);
        Assert.That(crif.Unimplemented.Any(u => u.Keyword == "When" && u.Text == "I log in"), Is.True);

        // And: Only matched step classes should be in the classes list
        Assert.That(crif.Classes, Has.Count.EqualTo(2));
        Assert.That(crif.Classes, Contains.Item("AuthSteps"));
        Assert.That(crif.Classes, Contains.Item("SessionSteps"));
    }

    [Test]
    public void Convert_WithDataTableStep_ExtractsDataTableAndMatchesStep()
    {
        // Given: A step with DataTable parameter
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have the following transactions",
            Method = "IHaveTheFollowingTransactions",
            Class = "TransactionSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "DataTable", Name = "table" }]
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with that step and DataTable
        var gherkin = """
            Feature: Transactions

            Scenario: Multiple transactions
              Given I have the following transactions
                | Date       | Payee      | Amount |
                | 2024-01-01 | Store A    | 100.00 |
                | 2024-01-02 | Store B    | 200.00 |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Step should be matched with correct Owner and Method
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.Owner, Is.EqualTo("TransactionSteps"));
        Assert.That(step.Method, Is.EqualTo("IHaveTheFollowingTransactions"));

        // And: DataTable should be extracted
        Assert.That(step.DataTable, Is.Not.Null);
        Assert.That(step.DataTable!.Headers, Has.Count.EqualTo(3));
        Assert.That(step.DataTable.Rows, Has.Count.EqualTo(2));

        // And: Step should have DataTable variable as argument
        Assert.That(step.Arguments, Has.Count.EqualTo(1));
        Assert.That(step.Arguments[0].Value, Is.EqualTo("table1"));
    }

    [Test]
    public void Convert_WithDataTableStep_AddsClassAndNamespace()
    {
        // Given: A step with DataTable parameter
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "When",
            Text = "I create transactions",
            Method = "ICreateTransactions",
            Class = "TransactionSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "DataTable", Name = "transactions" }]
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with DataTable
        var gherkin = """
            Feature: Transactions

            Scenario: Create multiple
              When I create transactions
                | Payee   | Amount |
                | Store A | 100.00 |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Class should be added to Classes list
        Assert.That(crif.Classes, Contains.Item("TransactionSteps"));

        // And: Namespace should be added to Usings
        Assert.That(crif.Usings, Contains.Item("YoFi.V3.Tests.Functional.Steps"));
    }

    [Test]
    public void Convert_WithMixedParametersAndDataTable_ExtractsBothCorrectly()
    {
        // Given: A step with both regular parameter and DataTable
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have {count} transactions with data",
            Method = "IHaveTransactionsWithData",
            Class = "TransactionSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [
                new StepParameter { Type = "int", Name = "count" },
                new StepParameter { Type = "DataTable", Name = "table" }
            ]
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with parameter and DataTable
        var gherkin = """
            Feature: Transactions

            Scenario: Multiple transactions
              Given I have 5 transactions with data
                | Field  | Value |
                | Type   | Credit |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Step should be matched
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.Owner, Is.EqualTo("TransactionSteps"));
        Assert.That(step.Method, Is.EqualTo("IHaveTransactionsWithData"));

        // And: Both regular argument and DataTable variable should be present
        Assert.That(step.Arguments, Has.Count.EqualTo(2));
        Assert.That(step.Arguments[0].Value, Is.EqualTo("5"));
        Assert.That(step.Arguments[0].Last, Is.False);
        Assert.That(step.Arguments[1].Value, Is.EqualTo("table1"));
        Assert.That(step.Arguments[1].Last, Is.True);

        // And: DataTable should be extracted
        Assert.That(step.DataTable, Is.Not.Null);
        Assert.That(step.DataTable!.Headers, Has.Count.EqualTo(2));
    }

    [Test]
    public void Convert_WithMultipleDataTableSteps_GeneratesUniqueVariableNames()
    {
        // Given: Multiple steps with DataTable parameters
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I have transactions",
                Method = "IHaveTransactions",
                Class = "TransactionSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = [new StepParameter { Type = "DataTable", Name = "transactions" }]
            },
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I have payees",
                Method = "IHavePayees",
                Class = "PayeeSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = [new StepParameter { Type = "DataTable", Name = "payees" }]
            }
        ]);

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with multiple DataTable steps
        var gherkin = """
            Feature: Data Setup

            Scenario: Setup test data
              Given I have transactions
                | Date       | Amount |
                | 2024-01-01 | 100.00 |
              And I have payees
                | Name    | Category |
                | Store A | Shopping |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Each DataTable should have a unique variable name
        var step1 = crif.Rules[0].Scenarios[0].Steps[0];
        var step2 = crif.Rules[0].Scenarios[0].Steps[1];
        Assert.That(step1.DataTable!.VariableName, Is.EqualTo("table1"));
        Assert.That(step2.DataTable!.VariableName, Is.EqualTo("table2"));

        // And: Arguments should reference the correct table variables
        Assert.That(step1.Arguments[0].Value, Is.EqualTo("table1"));
        Assert.That(step2.Arguments[0].Value, Is.EqualTo("table2"));
    }

    [Test]
    public void Convert_WithUnmatchedDataTableStep_StaysInUnimplementedList()
    {
        // Given: An empty step metadata collection
        var stepMetadata = new StepMetadataCollection();
        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with unmatched DataTable step
        var gherkin = """
            Feature: Transactions

            Scenario: Test
              Given I have the following data
                | Field | Value |
                | Name  | Test  |
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Step should be in Unimplemented list
        Assert.That(crif.Unimplemented, Has.Count.EqualTo(1));
        Assert.That(crif.Unimplemented[0].Text, Is.EqualTo("I have the following data"));

        // And: Step should still have DataTable extracted
        var step = crif.Rules[0].Scenarios[0].Steps[0];
        Assert.That(step.DataTable, Is.Not.Null);

        // And: Step should have Owner="this" for unimplemented step
        Assert.That(step.Owner, Is.EqualTo("this"));
    }

    [Test]
    public void Convert_WithDataTableInBackground_ExtractsDataTableAndMatches()
    {
        // Given: A step with DataTable parameter
        var stepMetadata = new StepMetadataCollection();
        stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have test data",
            Method = "IHaveTestData",
            Class = "DataSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "DataTable", Name = "data" }]
        });

        var converter = new GherkinToCrifConverter(stepMetadata);

        // And: A Gherkin feature with DataTable in Background
        var gherkin = """
            Feature: Data Tests

            Background:
              Given I have test data
                | Key   | Value |
                | Test1 | Val1  |

            Scenario: Test scenario
              When I run tests
            """;
        var feature = ParseGherkin(gherkin);

        // When: Feature is converted to CRIF
        var crif = converter.Convert(feature);

        // Then: Background step should be matched
        var bgStep = crif.Background!.Steps[0];
        Assert.That(bgStep.Owner, Is.EqualTo("DataSteps"));
        Assert.That(bgStep.Method, Is.EqualTo("IHaveTestData"));

        // And: DataTable should be extracted in background
        Assert.That(bgStep.DataTable, Is.Not.Null);
        Assert.That(bgStep.DataTable!.Headers, Has.Count.EqualTo(2));

        // And: Class should be added from background step
        Assert.That(crif.Classes, Contains.Item("DataSteps"));
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
