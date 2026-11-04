using System.Globalization;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Tests.Unit.Application.SampleData;

[TestFixture]
public class SampleScenarioLoaderTests
{
    [Test]
    public void BundledCatalog_IsValidatedAndHasUniqueStableDefinitions()
    {
        var catalog = new SampleScenarioLoader().Scenarios;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(catalog.Select(s => s.Id), Is.Unique);
            Assert.That(catalog.Select(s => s.Id), Does.Contain("personal-finance"));
            foreach (var scenario in catalog)
            {
                Assert.That(scenario.Patterns.Select(p => p.Id), Is.Unique);
                Assert.That(scenario.Revision, Is.Positive);
                Assert.That(scenario.Patterns, Is.Not.Empty);
            }
        }
    }

    [Test]
    public void QuotedAmounts_AreParsedAsInvariantDecimalWithoutDoubleConversion()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var scenario = SampleDataTestDefinitions.Scenario(annual: "-123456789012345.67");
            Assert.That(scenario.Patterns[0].AnnualAmount, Is.EqualTo(-123456789012345.67m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestCase("annual_amount = \"-1200.00\"", "annual_amount = -1200.00", "annual_amount")]
    [TestCase("annual_amount = \"-1200.00\"", "annual_amount = \"0\"", "annual_amount")]
    [TestCase("annual_amount = \"-1200.00\"", "annual_amount = \"-0.01\"", "annual_amount")]
    [TestCase("annual_amount = \"-1200.00\"", "annual_amount = \"-79228162514264337593543950335\"", "annual_amount")]
    [TestCase("frequency = \"monthly\"", "frequency = \"daily\"", "frequency")]
    [TestCase("repeats = 1", "repeats = 0", "repeats")]
    [TestCase("repeats = 1", "repeats = 10001", "repeats")]
    [TestCase("revision = 1", "revision = 0", "revision")]
    [TestCase("date_jitter = \"none\"", "date_jitter = \"unknown\"", "date_jitter")]
    [TestCase("amount_jitter = \"none\"", "amount_jitter = \"unknown\"", "amount_jitter")]
    [TestCase("category = \"Food:Groceries\"", "category = \"::\"", "category")]
    [TestCase("payees = [\"Store A\", \"Store B\"]", "payees = []", "payees")]
    [TestCase("payees = [\"Store A\", \"Store B\"]", "payees = [123]", "payees")]
    [TestCase("payees = [\"Store A\", \"Store B\"]", "payees = [\" \"]", "payees")]
    [TestCase("id = \"spending\"", "id = \"Not Stable\"", "id")]
    public void MalformedDefinitions_IdentifySourceAndField(string before, string after, string field)
    {
        var document = SampleDataTestDefinitions.Document().Replace(before, after, StringComparison.Ordinal);
        var error = Assert.Throws<InvalidOperationException>(() => SampleScenarioLoader.Parse(document, "bad.toml"));
        Assert.That(error!.Message, Does.Contain("bad.toml").And.Contain($"'{field}'"));
    }

    [TestCase("groups = []\n", "groups")]
    [TestCase("loans = []\n", "loans")]
    [TestCase("budgets = []\n", "budgets")]
    public void UnknownRootFields_AreRejected(string prefix, string field)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SampleScenarioLoader.Parse(prefix + SampleDataTestDefinitions.Document(), "bad.toml"));
        Assert.That(error!.Message, Does.Contain(field).And.Contain("Unknown"));
    }

    [Test]
    public void UnknownPatternFields_AreRejectedWithPatternContext()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SampleScenarioLoader.Parse(SampleDataTestDefinitions.Document() + "\nloan = true", "bad.toml"));
        Assert.That(error!.Message, Does.Contain("spending").And.Contain("loan").And.Contain("bad.toml"));
    }

    [Test]
    public void SyntaxErrors_IncludeSourcePosition()
    {
        var error = Assert.Throws<InvalidOperationException>(() => SampleScenarioLoader.Parse("id = [", "broken.toml"));
        Assert.That(error!.Message, Does.Contain("broken.toml").And.Contain("(1,"));
    }

    [Test]
    public void DuplicateScenarioIds_AreRejected()
    {
        var document = SampleDataTestDefinitions.Document();
        Assert.Throws<InvalidOperationException>(() => new SampleScenarioLoader([("one.toml", document), ("two.toml", document)]));
    }

    [Test]
    public void DuplicatePatternIds_AreRejected()
    {
        var document = SampleDataTestDefinitions.Document();
        document += "\n" + document[document.IndexOf("[[patterns]]", StringComparison.Ordinal)..];
        var error = Assert.Throws<InvalidOperationException>(() => SampleScenarioLoader.Parse(document, "duplicate.toml"));
        Assert.That(error!.Message, Does.Contain("Duplicate pattern"));
    }

    [Test]
    public void ConflictingMerchantCategories_AreRejectedCaseInsensitively()
    {
        var document = SampleDataTestDefinitions.Document();
        var second = document[document.IndexOf("[[patterns]]", StringComparison.Ordinal)..]
            .Replace("spending", "second", StringComparison.Ordinal)
            .Replace("Food:Groceries", "Personal:Care", StringComparison.Ordinal)
            .Replace("Store A", "STORE A", StringComparison.Ordinal);
        var error = Assert.Throws<InvalidOperationException>(() => SampleScenarioLoader.Parse(document + "\n" + second, "conflict.toml"));
        Assert.That(error!.Message, Does.Contain("conflicting").And.Contain("second"));
    }

    [Test]
    public void MissingMetadataAndEmptyCatalog_AreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new SampleScenarioLoader([]));
        Assert.Throws<InvalidOperationException>(() => SampleScenarioLoader.Parse("id = \"empty\"", "empty.toml"));
    }

    [TestCase("Store A", "payees")]
    [TestCase("Food:Groceries", "category")]
    public void ExistingDtoLengthConstraints_AreEnforcedAtCatalogLoad(string before, string field)
    {
        var document = SampleDataTestDefinitions.Document().Replace(before, new string('x', 201), StringComparison.Ordinal);
        var error = Assert.Throws<InvalidOperationException>(() => SampleScenarioLoader.Parse(document, "too-long.toml"));
        Assert.That(error!.Message, Does.Contain(field).And.Contain("200"));
    }
}
