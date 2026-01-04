using NUnit.Framework;

namespace YoFi.V3.Tests.Generator;

/// <summary>
/// Tests for step matching logic - matching Gherkin steps to step definition methods.
/// </summary>
[TestFixture]
public class StepMatchingTests
{
    private StepMetadataCollection _stepMetadata = null!;

    [SetUp]
    public void SetUp()
    {
        _stepMetadata = new StepMetadataCollection();
    }

    [Test]
    public void FindMatch_ExactMatch_ReturnsMatchingStep()
    {
        // Given: A step definition with exact text
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I am logged in",
            Method = "IAmLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = []
        });

        // When: Searching for exact match
        var match = _stepMetadata.FindMatch("Given", "I am logged in");

        // Then: Should find the matching step
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("IAmLoggedIn"));
        Assert.That(match.Class, Is.EqualTo("AuthSteps"));
    }

    [Test]
    public void FindMatch_CaseInsensitive_ReturnsMatch()
    {
        // Given: A step definition with specific casing
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I am logged in",
            Method = "IAmLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = []
        });

        // When: Searching with different casing
        var match = _stepMetadata.FindMatch("Given", "I AM LOGGED IN");

        // Then: Should find the match (case insensitive)
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("IAmLoggedIn"));
    }

    [Test]
    public void FindMatch_WrongKeyword_ReturnsNull()
    {
        // Given: A "Given" step definition
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I am logged in",
            Method = "IAmLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = []
        });

        // When: Searching with wrong keyword
        var match = _stepMetadata.FindMatch("When", "I am logged in");

        // Then: Should not find a match
        Assert.That(match, Is.Null);
    }

    [Test]
    public void FindMatch_NoMatch_ReturnsNull()
    {
        // Given: A step definition
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I am logged in",
            Method = "IAmLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = []
        });

        // When: Searching for different text
        var match = _stepMetadata.FindMatch("Given", "I am logged out");

        // Then: Should not find a match
        Assert.That(match, Is.Null);
    }

    [Test]
    public void FindMatch_WithPlaceholder_MatchesSingleWord()
    {
        // Given: A step definition with placeholder
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have an account named {account}",
            Method = "IHaveAnAccountNamed",
            Class = "AccountSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "string", Name = "account" }]
        });

        // When: Searching with single-word value
        var match = _stepMetadata.FindMatch("Given", "I have an account named Savings");

        // Then: Should find the match
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("IHaveAnAccountNamed"));
    }

    [Test]
    public void FindMatch_WithPlaceholder_MatchesHyphenatedWord()
    {
        // Given: A step definition with placeholder
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have an account named {account}",
            Method = "IHaveAnAccountNamed",
            Class = "AccountSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "string", Name = "account" }]
        });

        // When: Searching with hyphenated value (no spaces)
        var match = _stepMetadata.FindMatch("Given", "I have an account named Ski-Village");

        // Then: Should find the match
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("IHaveAnAccountNamed"));
    }

    [Test]
    public void FindMatch_WithPlaceholder_MatchesQuotedPhrase()
    {
        // Given: A step definition with placeholder
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have an account named {account}",
            Method = "IHaveAnAccountNamed",
            Class = "AccountSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "string", Name = "account" }]
        });

        // When: Searching with quoted phrase (contains spaces)
        var match = _stepMetadata.FindMatch("Given", "I have an account named \"Ski Village\"");

        // Then: Should find the match
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("IHaveAnAccountNamed"));
    }

    [Test]
    public void FindMatch_WithPlaceholder_DoesNotMatchUnquotedPhrase()
    {
        // Given: A step definition with placeholder
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have an account named {account}",
            Method = "IHaveAnAccountNamed",
            Class = "AccountSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "string", Name = "account" }]
        });

        // When: Searching with unquoted phrase (contains spaces)
        var match = _stepMetadata.FindMatch("Given", "I have an account named Ski Village");

        // Then: Should not find a match (placeholder requires no spaces or quotes)
        Assert.That(match, Is.Null);
    }

    [Test]
    public void FindMatch_MultiplePlaceholders_MatchesAllValues()
    {
        // Given: A step definition with multiple placeholders
        _stepMetadata.Add(new StepMetadata
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

        // When: Searching with multiple values
        var match = _stepMetadata.FindMatch("Given", "I have 100 dollars in Savings");

        // Then: Should find the match
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("IHaveDollarsIn"));
    }

    [Test]
    public void FindMatch_MultipleDefinitions_ReturnsFirstMatch()
    {
        // Given: Multiple step definitions
        _stepMetadata.AddRange([
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in as {user}",
                Method = "IAmLoggedInAs",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = [new StepParameter { Type = "string", Name = "user" }]
            },
            new StepMetadata
            {
                NormalizedKeyword = "Given",
                Text = "I am logged in",
                Method = "IAmLoggedIn",
                Class = "AuthSteps",
                Namespace = "YoFi.V3.Tests.Functional.Steps",
                Parameters = []
            }
        ]);

        // When: Searching for step that matches the second definition
        var match = _stepMetadata.FindMatch("Given", "I am logged in");

        // Then: Should find the exact match (not the parameterized one)
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("IAmLoggedIn"));
        Assert.That(match.Parameters, Is.Empty);
    }

    [Test]
    public void AddRange_MultipleSteps_AddsAllToCollection()
    {
        // Given: Multiple step definitions
        var steps = new List<StepMetadata>
        {
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
        };

        // When: Adding range to collection
        _stepMetadata.AddRange(steps);

        // Then: Should be able to find both steps
        var match1 = _stepMetadata.FindMatch("Given", "I am logged in");
        var match2 = _stepMetadata.FindMatch("When", "I create a transaction");

        Assert.That(match1, Is.Not.Null);
        Assert.That(match2, Is.Not.Null);
    }

    [Test]
    public void FindMatch_NumericPlaceholder_MatchesNumber()
    {
        // Given: A step definition with numeric placeholder
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "I have {quantity} items",
            Method = "IHaveItems",
            Class = "InventorySteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "int", Name = "quantity" }]
        });

        // When: Searching with numeric value
        var match = _stepMetadata.FindMatch("Given", "I have 42 items");

        // Then: Should find the match
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("IHaveItems"));
    }

    [Test]
    public void FindMatch_PlaceholderAtStart_Matches()
    {
        // Given: A step definition with placeholder at start
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "{user} is logged in",
            Method = "UserIsLoggedIn",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "string", Name = "user" }]
        });

        // When: Searching with value at start
        var match = _stepMetadata.FindMatch("Given", "Alice is logged in");

        // Then: Should find the match
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("UserIsLoggedIn"));
    }

    [Test]
    public void FindMatch_PlaceholderAtEnd_Matches()
    {
        // Given: A step definition with placeholder at end
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "the user is {status}",
            Method = "TheUserIs",
            Class = "AuthSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [new StepParameter { Type = "string", Name = "status" }]
        });

        // When: Searching with value at end
        var match = _stepMetadata.FindMatch("Given", "the user is active");

        // Then: Should find the match
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("TheUserIs"));
    }

    [Test]
    public void FindMatch_AdjacentPlaceholders_Matches()
    {
        // Given: A step definition with adjacent placeholders
        _stepMetadata.Add(new StepMetadata
        {
            NormalizedKeyword = "Given",
            Text = "user {firstName} {lastName} exists",
            Method = "UserExists",
            Class = "UserSteps",
            Namespace = "YoFi.V3.Tests.Functional.Steps",
            Parameters = [
                new StepParameter { Type = "string", Name = "firstName" },
                new StepParameter { Type = "string", Name = "lastName" }
            ]
        });

        // When: Searching with adjacent values
        var match = _stepMetadata.FindMatch("Given", "user John Smith exists");

        // Then: Should find the match
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Method, Is.EqualTo("UserExists"));
    }
}
