using YoFi.V3.Tests.Generator;

namespace YoFi.V3.Tests.Generator.Tests;

/// <summary>
/// Comprehensive tests for FunctionalTestGenerator covering all template features.
/// </summary>
public class FunctionalTestGeneratorTests
{
    private FunctionalTestGenerator _generator = null!;
    private string _templatePath = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new FunctionalTestGenerator();
        _templatePath = "FunctionalTest.mustache";
    }

    #region Basic Template Tests

    /// <summary>
    /// Generator produces output from template and CRIF.
    /// </summary>
    [Test]
    public void Generate_WithSimpleTemplate_ProducesOutput()
    {
        // Given: A simple Mustache template
        var template = "Hello {{FeatureName}}!";

        // And: A CRIF-like object with data
        var crif = new FunctionalTestCrif
        {
            FeatureName = "World"
        };

        // When: Generating output
        var result = _generator.GenerateString(template, crif);

        // Then: Template variables are replaced
        Assert.That(result, Is.EqualTo("Hello World!"));
    }

    /// <summary>
    /// Generator handles usings collection correctly.
    /// </summary>
    [Test]
    public void Generate_WithUsingsCollection_ExpandsCorrectly()
    {
        // Given: A template with collection iteration
        var template = "{{#Usings}}using {{.}};\n{{/Usings}}";

        // And: A CRIF with multiple usings
        var crif = new FunctionalTestCrif
        {
            Usings = ["System", "NUnit.Framework", "MyApp.Steps"]
        };

        // When: Generating output
        var result = _generator.GenerateString(template, crif);

        // Then: All usings are expanded
        Assert.That(result, Does.Contain("using System;"));
        Assert.That(result, Does.Contain("using NUnit.Framework;"));
        Assert.That(result, Does.Contain("using MyApp.Steps;"));
    }

    #endregion

    #region Full Template Tests

    /// <summary>
    /// Generator produces valid C# code with minimal CRIF.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithMinimalCrif_ProducesValidCode()
    {
        // Given: A minimal CRIF
        var crif = new FunctionalTestCrif
        {
            Usings = ["NUnit.Framework"],
            Namespace = "TestNamespace",
            FeatureName = "MyFeature",
            FeatureDescription = "Test Feature",
            DescriptionLines = ["Test description"],
            BaseClass = "TestBase",
            Classes = ["TestSteps"],
            Rules = []
        };

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: Output contains expected elements
        Assert.That(result, Does.Contain("using NUnit.Framework;"));
        Assert.That(result, Does.Contain("namespace TestNamespace;"));
        Assert.That(result, Does.Contain("public partial class MyFeatureFeature_Tests : TestBase"));
        Assert.That(result, Does.Contain("protected TestSteps TestSteps => _theTestSteps ??= new(this);"));
    }

    /// <summary>
    /// Generator produces background setup method correctly.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithBackground_ProducesSetupMethod()
    {
        // Given: A CRIF with background steps
        var crif = CreateMinimalCrif();
        crif.Background = new BackgroundCrif
        {
            Steps =
            [
                new StepCrif
                {
                    Keyword = "Given",
                    Text = "the application is running",
                    Owner = "NavigationSteps",
                    Method = "GivenLaunchedSite",
                    Arguments = []
                }
            ]
        };

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: SetUp method is generated with background steps
        Assert.That(result, Does.Contain("[SetUp]"));
        Assert.That(result, Does.Contain("public async Task SetupAsync()"));
        Assert.That(result, Does.Contain("// Given the application is running"));
        Assert.That(result, Does.Contain("await NavigationSteps.GivenLaunchedSite();"));
    }

    #endregion

    #region DataTable Tests

    /// <summary>
    /// Generator produces DataTable instantiation correctly.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithDataTable_ProducesDataTableCode()
    {
        // Given: A CRIF with a step containing a DataTable
        var crif = CreateMinimalCrif();
        crif.Rules =
        [
            new RuleCrif
            {
                Name = "Test Rule",
                Description = "Test description",
                Scenarios =
                [
                    new ScenarioCrif
                    {
                        Name = "Test scenario",
                        Method = "TestScenario",
                        Steps =
                        [
                            new StepCrif
                            {
                                Keyword = "Given",
                                Text = "I have a transaction:",
                                Owner = "TransactionSteps",
                                Method = "GivenIHaveATransaction",
                                Arguments = [new ArgumentCrif { Value = "table", Last = true }],
                                DataTable = new DataTableCrif
                                {
                                    VariableName = "table",
                                    Headers =
                                    [
                                        new HeaderCellCrif { Value = "Field", Last = false },
                                        new HeaderCellCrif { Value = "Value", Last = true }
                                    ],
                                    Rows =
                                    [
                                        new DataRowCrif
                                        {
                                            Cells =
                                            [
                                                new DataCellCrif { Value = "Payee", Last = false },
                                                new DataCellCrif { Value = "Coffee Shop", Last = true }
                                            ],
                                            Last = true
                                        }
                                    ]
                                }
                            }
                        ]
                    }
                ]
            }
        ];

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: DataTable instantiation is generated correctly
        Assert.That(result, Does.Contain("var table = new DataTable("));
        Assert.That(result, Does.Contain("[\"Field\", \"Value\"]"));
        Assert.That(result, Does.Contain("[\"Payee\", \"Coffee Shop\"]"));
        Assert.That(result, Does.Contain("await TransactionSteps.GivenIHaveATransaction(table);"));
    }

    /// <summary>
    /// Generator handles multiple rows in DataTable.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithMultiRowDataTable_ProducesAllRows()
    {
        // Given: A CRIF with a multi-row DataTable
        var crif = CreateMinimalCrif();
        crif.Rules =
        [
            new RuleCrif
            {
                Name = "Test Rule",
                Description = "Test",
                Scenarios =
                [
                    new ScenarioCrif
                    {
                        Name = "Test",
                        Method = "Test",
                        Steps =
                        [
                            new StepCrif
                            {
                                Keyword = "Given",
                                Text = "test",
                                Owner = "Steps",
                                Method = "Test",
                                Arguments = [new ArgumentCrif { Value = "table", Last = true }],
                                DataTable = new DataTableCrif
                                {
                                    VariableName = "table",
                                    Headers =
                                    [
                                        new HeaderCellCrif { Value = "Name", Last = true }
                                    ],
                                    Rows =
                                    [
                                        new DataRowCrif
                                        {
                                            Cells = [new DataCellCrif { Value = "First", Last = true }],
                                            Last = false
                                        },
                                        new DataRowCrif
                                        {
                                            Cells = [new DataCellCrif { Value = "Second", Last = true }],
                                            Last = false
                                        },
                                        new DataRowCrif
                                        {
                                            Cells = [new DataCellCrif { Value = "Third", Last = true }],
                                            Last = true
                                        }
                                    ]
                                }
                            }
                        ]
                    }
                ]
            }
        ];

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: All rows are generated with correct comma placement
        Assert.That(result, Does.Contain("[\"First\"],"));
        Assert.That(result, Does.Contain("[\"Second\"],"));
        Assert.That(result, Does.Contain("[\"Third\"]"));
        Assert.That(result, Does.Not.Contain("[\"Third\"],"));
    }

    #endregion

    #region TestCase and Parameters Tests

    /// <summary>
    /// Generator produces TestCase attributes for parameterized tests.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithTestCases_ProducesTestCaseAttributes()
    {
        // Given: A CRIF with TestCase data
        var crif = CreateMinimalCrif();
        crif.Rules =
        [
            new RuleCrif
            {
                Name = "Test Rule",
                Description = "Test",
                Scenarios =
                [
                    new ScenarioCrif
                    {
                        Name = "Parameterized test",
                        Method = "ParameterizedTest",
                        TestCases = ["\"/page1\"", "\"/page2\"", "\"/page3\""],
                        Parameters =
                        [
                            new ParameterCrif { Type = "string", Name = "page", Last = true }
                        ],
                        Steps =
                        [
                            new StepCrif
                            {
                                Keyword = "When",
                                Text = "I navigate to <page>",
                                Owner = "NavSteps",
                                Method = "Navigate",
                                Arguments = [new ArgumentCrif { Value = "page", Last = true }]
                            }
                        ]
                    }
                ]
            }
        ];

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: TestCase attributes are generated
        Assert.That(result, Does.Contain("[TestCase(\"/page1\")]"));
        Assert.That(result, Does.Contain("[TestCase(\"/page2\")]"));
        Assert.That(result, Does.Contain("[TestCase(\"/page3\")]"));
        Assert.That(result, Does.Contain("public async Task ParameterizedTest(string page)"));
    }

    /// <summary>
    /// Generator handles multiple parameters correctly.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithMultipleParameters_ProducesCorrectSignature()
    {
        // Given: A CRIF with multiple parameters
        var crif = CreateMinimalCrif();
        crif.Rules =
        [
            new RuleCrif
            {
                Name = "Test",
                Description = "Test",
                Scenarios =
                [
                    new ScenarioCrif
                    {
                        Name = "Multi param test",
                        Method = "MultiParamTest",
                        Parameters =
                        [
                            new ParameterCrif { Type = "string", Name = "name", Last = false },
                            new ParameterCrif { Type = "int", Name = "count", Last = true }
                        ],
                        Steps = []
                    }
                ]
            }
        ];

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: Method signature has both parameters
        Assert.That(result, Does.Contain("public async Task MultiParamTest(string name, int count)"));
    }

    #endregion

    #region Remarks and Documentation Tests

    /// <summary>
    /// Generator produces remarks section in XML documentation.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithRemarks_ProducesRemarksSection()
    {
        // Given: A CRIF with remarks
        var crif = CreateMinimalCrif();
        crif.Rules =
        [
            new RuleCrif
            {
                Name = "Test",
                Description = "Test",
                Scenarios =
                [
                    new ScenarioCrif
                    {
                        Name = "Test with remarks",
                        Method = "TestWithRemarks",
                        Remarks = new RemarksCrif
                        {
                            Lines =
                            [
                                "This is a remark line 1",
                                "This is a remark line 2"
                            ]
                        },
                        Steps = []
                    }
                ]
            }
        ];

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: Remarks are included in XML comments
        Assert.That(result, Does.Contain("/// <remarks>"));
        Assert.That(result, Does.Contain("/// This is a remark line 1"));
        Assert.That(result, Does.Contain("/// This is a remark line 2"));
        Assert.That(result, Does.Contain("/// </remarks>"));
    }

    #endregion

    #region Explicit Tag Tests

    /// <summary>
    /// Generator produces Explicit attribute when flag is set.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithExplicitTag_ProducesExplicitAttribute()
    {
        // Given: A CRIF with ExplicitTag set to true
        var crif = CreateMinimalCrif();
        crif.Rules =
        [
            new RuleCrif
            {
                Name = "Test",
                Description = "Test",
                Scenarios =
                [
                    new ScenarioCrif
                    {
                        Name = "Explicit test",
                        Method = "ExplicitTest",
                        ExplicitTag = true,
                        Steps = []
                    }
                ]
            }
        ];

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: Explicit attribute is generated
        Assert.That(result, Does.Contain("[Explicit]"));
    }

    /// <summary>
    /// Generator does not produce Explicit attribute when flag is false.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithoutExplicitTag_DoesNotProduceExplicitAttribute()
    {
        // Given: A CRIF with ExplicitTag set to false
        var crif = CreateMinimalCrif();
        crif.Rules =
        [
            new RuleCrif
            {
                Name = "Test",
                Description = "Test",
                Scenarios =
                [
                    new ScenarioCrif
                    {
                        Name = "Normal test",
                        Method = "NormalTest",
                        ExplicitTag = false,
                        Steps = []
                    }
                ]
            }
        ];

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: No Explicit attribute (but [Test] should exist)
        Assert.That(result, Does.Contain("[Test]"));
        Assert.That(result, Does.Not.Contain("[Explicit]"));
    }

    #endregion

    #region Unimplemented Steps Tests

    /// <summary>
    /// Generator produces NotImplementedException stubs for unimplemented steps.
    /// </summary>
    [Test]
    public void GenerateFromFile_WithUnimplementedSteps_ProducesStubs()
    {
        // Given: A CRIF with unimplemented steps
        var crif = CreateMinimalCrif();
        crif.Unimplemented =
        [
            new UnimplementedStepCrif
            {
                Keyword = "Given",
                Text = "a new feature exists",
                Method = "GivenANewFeatureExists",
                Parameters = []
            },
            new UnimplementedStepCrif
            {
                Keyword = "When",
                Text = "I perform action with \"parameter\"",
                Method = "WhenIPerformActionWithParameter",
                Parameters =
                [
                    new ParameterCrif { Type = "string", Name = "parameter", Last = true }
                ]
            }
        ];

        // When: Generating from file
        var result = _generator.GenerateStringFromFile(_templatePath, crif);

        // Then: Stub methods are generated
        Assert.That(result, Does.Contain("#region Unimplemented Steps"));
        Assert.That(result, Does.Contain("/// Given a new feature exists"));
        Assert.That(result, Does.Contain("async Task GivenANewFeatureExists()"));
        Assert.That(result, Does.Contain("throw new NotImplementedException();"));
        Assert.That(result, Does.Contain("async Task WhenIPerformActionWithParameter(string parameter)"));
    }

    #endregion

    #region Stream Tests

    /// <summary>
    /// Generator produces stream that can be read.
    /// </summary>
    [Test]
    public void Generate_ReturnsReadableStream()
    {
        // Given: A simple template and CRIF
        var template = "Test: {{FeatureName}}";
        var crif = new FunctionalTestCrif { FeatureName = "Example" };

        // When: Generating to stream
        using var stream = _generator.Generate(template, crif);
        using var reader = new StreamReader(stream);
        var result = reader.ReadToEnd();

        // Then: Stream contains generated output
        Assert.That(result, Is.EqualTo("Test: Example"));
    }

    #endregion

    #region Full Sample YAML Tests

    /// <summary>
    /// Generator produces complete output from sample YAML file.
    /// </summary>
    [Test]
    public void GenerateFromSampleYaml_ProducesCompleteOutput()
    {
        // Given: Sample CRIF YAML file exists
        var yamlPath = "sample-crif.yaml";
        Assert.That(File.Exists(yamlPath), Is.True, "sample-crif.yaml should exist");

        // And: YAML is loaded and deserialized
        var yamlContent = File.ReadAllText(yamlPath);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .Build();
        var crif = deserializer.Deserialize<FunctionalTestCrif>(yamlContent);

        // And: Output directory exists
        var outputDir = Path.Combine("bin", "GeneratedTests");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "TransactionRecordFieldsFeature_Tests.cs");

        // When: Generating from template file
        _generator.GenerateToFile(_templatePath, crif, outputPath);

        // Then: Output file should exist
        Assert.That(File.Exists(outputPath), Is.True, "Generated file should exist");

        // And: Output file should contain expected elements
        var generatedCode = File.ReadAllText(outputPath);
        Assert.That(generatedCode, Does.Contain("using NUnit.Framework;"));
        Assert.That(generatedCode, Does.Contain("namespace YoFi.V3.Tests.Functional.Features;"));
        Assert.That(generatedCode, Does.Contain("public partial class TransactionRecordFieldsFeature_Tests : FunctionalTestBase"));
        Assert.That(generatedCode, Does.Contain("[SetUp]"));
        Assert.That(generatedCode, Does.Contain("public async Task SetupAsync()"));
        Assert.That(generatedCode, Does.Contain("#region Rule: Quick Edit Modal"));
        Assert.That(generatedCode, Does.Contain("var table = new DataTable("));
        Assert.That(generatedCode, Does.Contain("[TestCase(\"/weather\")]"));
        Assert.That(generatedCode, Does.Contain("[Explicit]"));
        Assert.That(generatedCode, Does.Contain("#region Unimplemented Steps"));

        // And: Attach the generated file to test output
        TestContext.AddTestAttachment(outputPath, "Generated test file from sample YAML");

        // And: Log output location for visibility
        TestContext.WriteLine($"Generated test file: {Path.GetFullPath(outputPath)}");
    }

    #endregion

    #region Helper Methods

    private static FunctionalTestCrif CreateMinimalCrif()
    {
        return new FunctionalTestCrif
        {
            Usings = ["NUnit.Framework"],
            Namespace = "TestNamespace",
            FeatureName = "TestFeature",
            FeatureDescription = "Test Feature",
            DescriptionLines = ["Test description"],
            BaseClass = "TestBase",
            Classes = ["TestSteps"],
            Rules = []
        };
    }

    #endregion
}
