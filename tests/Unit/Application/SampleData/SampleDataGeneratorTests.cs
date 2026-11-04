using System.Collections.Immutable;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.SampleData;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Unit.Application.SampleData;

[TestFixture]
public class SampleDataGeneratorTests
{
    private readonly SampleDataGenerator _generator = new();
    private static readonly DateOnly Start = new(2024, 1, 1);
    private static readonly DateOnly End = new(2024, 12, 31);

    [TestCase("weekly", 52)]
    [TestCase("semi-monthly", 24)]
    [TestCase("monthly", 12)]
    [TestCase("quarterly", 4)]
    [TestCase("yearly", 1)]
    public void Recurrence_DistributesAnnualTargetAcrossPeriodsAndRepeats(string frequency, int periods)
    {
        var scenario = SampleDataTestDefinitions.Scenario(frequency, repeats: 3, annual: (periods * -30).ToString());
        var data = _generator.Generate(scenario, Start, End);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.Transactions, Has.Length.EqualTo(periods * 3));
            Assert.That(data.Transactions.Select(t => t.Amount), Is.All.EqualTo(-10m));
            Assert.That(data.Transactions.Sum(t => t.Amount), Is.EqualTo(periods * -30m));
            Assert.That(data.Transactions.Select(t => t.ExternalId), Is.Unique);
        }
    }

    [Test]
    public void InclusiveDates_FilterCanonicalOccurrencesWithoutProrating()
    {
        var scenario = SampleDataTestDefinitions.Scenario("semi-monthly", annual: "2400");
        var data = _generator.Generate(scenario, new(2024, 2, 1), new(2024, 2, 15));
        Assert.That(data.Transactions.Select(t => t.Date), Is.EqualTo(new[] { new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 15) }));
        Assert.That(data.Transactions.Select(t => t.Amount), Is.All.EqualTo(100m));
        Assert.That(_generator.Generate(scenario, new(2024, 2, 2), new(2024, 2, 14)).Transactions, Is.Empty);
        Assert.That(_generator.Generate(scenario, new(2024, 2, 15), new(2024, 2, 15)).Transactions, Has.Length.EqualTo(1));
    }

    [Test]
    public void WeeklyAnchors_UseExactly52SevenDayPeriodsFromJanuaryFirst()
    {
        var data = _generator.Generate(SampleDataTestDefinitions.Scenario("weekly"), Start, End);
        Assert.That(data.Transactions.Select(t => t.Date), Is.EqualTo(Enumerable.Range(0, 52).Select(i => Start.AddDays(i * 7))));
    }

    [TestCase("none", 0)]
    [TestCase("low", .1)]
    [TestCase("moderate", .4)]
    [TestCase("high", .9)]
    public void Jitter_PreservesSignsAndStaysInsideAmountAndCalendarPeriodBounds(string jitter, decimal proportion)
    {
        foreach (var annual in new[] { "1200", "-1200" })
        {
            var scenario = SampleDataTestDefinitions.Scenario("monthly", annual: annual, amountJitter: jitter, dateJitter: jitter);
            var data = _generator.Generate(scenario, Start, End);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(data.Transactions, Has.Length.EqualTo(12));
                for (var month = 1; month <= 12; month++)
                {
                    var transaction = data.Transactions.Single(t => t.Date.Month == month);
                    Assert.That(transaction.Date.Day, Is.InRange(1, 1 + (int)decimal.Floor((DateTime.DaysInMonth(2024, month) - 1) * proportion)));
                    Assert.That(Math.Abs(transaction.Amount), Is.InRange(100 * (1 - proportion), 100 * (1 + proportion)));
                    Assert.That(Math.Sign(transaction.Amount), Is.EqualTo(annual.StartsWith('-') ? -1 : 1));
                    Assert.That(transaction.Amount, Is.EqualTo(decimal.Round(transaction.Amount, 2)));
                }
            }
        }
    }

    [TestCase("weekly")]
    [TestCase("semi-monthly")]
    [TestCase("monthly")]
    [TestCase("quarterly")]
    [TestCase("yearly")]
    public void OverlappingRanges_AreEqualIncludingLeapDayAndYearBoundaries(string frequency)
    {
        var scenario = SampleDataTestDefinitions.Scenario(frequency, repeats: 5, amountJitter: "high", dateJitter: "high");
        var full = _generator.Generate(scenario, Start, End);
        var left = _generator.Generate(scenario, new(2023, 10, 1), new(2024, 3, 31));
        var right = _generator.Generate(scenario, new(2024, 2, 29), new(2024, 9, 15));
        Assert.That(left.Transactions.Where(t => t.Date >= Start), Is.EqualTo(full.Transactions.Where(t => t.Date <= new DateOnly(2024, 3, 31))));
        Assert.That(right.Transactions, Is.EqualTo(full.Transactions.Where(t => t.Date >= new DateOnly(2024, 2, 29) && t.Date <= new DateOnly(2024, 9, 15))));
    }

    [TestCase("0.125", .13)]
    [TestCase("-0.125", -.13)]
    public void CentRounding_UsesMidpointAwayFromZero(string annual, decimal expected)
    {
        var data = _generator.Generate(SampleDataTestDefinitions.Scenario("yearly", annual: annual), Start, End);
        Assert.That(data.Transactions.Single().Amount, Is.EqualTo(expected));
    }

    [Test]
    public void FormattingMetadataAndRevisionChanges_DoNotChangeFingerprintOrHistory()
    {
        var text = SampleDataTestDefinitions.Document(amountJitter: "high", dateJitter: "high");
        var original = SampleScenarioLoader.Parse(text, "one");
        var modified = SampleScenarioLoader.Parse("# formatting\n" + text
            .Replace("revision = 1", "revision = 2", StringComparison.Ordinal)
            .Replace("name = \"Test Scenario\"", "name = \"Renamed\"", StringComparison.Ordinal)
            .Replace("annual_amount = \"-1200.00\"", "# same parsed decimal\nannual_amount = \"-1200\"", StringComparison.Ordinal)
            .Replace("id = \"spending\"\ncategory = \"Food:Groceries\"", "category = \"Food:Groceries\"\nid = \"spending\"", StringComparison.Ordinal), "two");
        Assert.That(SampleDataGenerator.Fingerprint(modified.Patterns[0]), Is.EqualTo(SampleDataGenerator.Fingerprint(original.Patterns[0])));
        Assert.That(_generator.Generate(modified, Start, End).Transactions, Is.EqualTo(_generator.Generate(original, Start, End).Transactions));
    }

    [Test]
    public void StableHashVectors_LockCanonicalEncodingAndChoiceMapping()
    {
        var scenario = SampleDataTestDefinitions.Scenario();
        var transaction = _generator.Generate(scenario, Start, Start).Transactions.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SampleDataGenerator.Fingerprint(scenario.Patterns[0]),
                Is.EqualTo("732bf85defb559d4a74361b2c056f7b8cb398a86f07d8a74ccdfe385bd78c839"));
            Assert.That(transaction.ExternalId,
                Is.EqualTo("sample:9b618863a62b8e0f1c0ce6d12981e9acda74bae144faf521ab1667382e6036c3"));
            Assert.That(transaction.Payee, Is.EqualTo("Store A"));
            Assert.That(transaction.Amount, Is.EqualTo(-100m));
        }
    }

    [Test]
    public void ReorderingAndUnrelatedPatternEdits_LeaveOtherHistoryUnchanged()
    {
        var catalogScenario = new SampleScenarioLoader().Scenarios[0];
        var reversed = new SampleScenario(catalogScenario.Id, catalogScenario.Revision + 1, "Changed", "Changed", catalogScenario.Patterns.Reverse().ToImmutableArray());
        Assert.That(_generator.Generate(reversed, Start, End).Transactions, Is.EqualTo(_generator.Generate(catalogScenario, Start, End).Transactions));
        var pattern = catalogScenario.Patterns[0];
        var subset = new SampleScenario(catalogScenario.Id, 999, "Subset", "Subset", [pattern]);
        var expected = _generator.Generate(subset, Start, End).Transactions;
        var ids = expected.Select(t => t.ExternalId).ToHashSet();
        Assert.That(_generator.Generate(catalogScenario, Start, End).Transactions.Where(t => ids.Contains(t.ExternalId)), Is.EqualTo(expected));

        var unrelated = catalogScenario.Patterns[1];
        var edited = new SamplePattern(unrelated.Id, unrelated.Category, unrelated.Payees,
            unrelated.AnnualAmount - 100, unrelated.Frequency, unrelated.Repeats, unrelated.AmountJitter, unrelated.DateJitter);
        var revised = new SampleScenario(catalogScenario.Id, catalogScenario.Revision + 1, "Revised", "Revised",
            catalogScenario.Patterns.SetItem(1, edited));
        Assert.That(_generator.Generate(revised, Start, End).Transactions.Where(t => ids.Contains(t.ExternalId)), Is.EqualTo(expected));
    }

    [TestCase("id = \"spending\"", "id = \"other\"")]
    [TestCase("category = \"Food:Groceries\"", "category = \"Food:Away\"")]
    [TestCase("[\"Store A\", \"Store B\"]", "[\"Store B\", \"Store A\"]")]
    [TestCase("\"-1200.00\"", "\"-1201.00\"")]
    [TestCase("frequency = \"monthly\"", "frequency = \"weekly\"")]
    [TestCase("repeats = 1", "repeats = 2")]
    [TestCase("amount_jitter = \"none\"", "amount_jitter = \"low\"")]
    [TestCase("date_jitter = \"none\"", "date_jitter = \"low\"")]
    public void RelevantEdits_ChangeFingerprintAndOccurrenceIds(string before, string after)
    {
        var original = SampleDataTestDefinitions.Scenario();
        var modified = SampleScenarioLoader.Parse(SampleDataTestDefinitions.Document().Replace(before, after, StringComparison.Ordinal), "edited");
        Assert.That(SampleDataGenerator.Fingerprint(modified.Patterns[0]), Is.Not.EqualTo(SampleDataGenerator.Fingerprint(original.Patterns[0])));
        var ids = _generator.Generate(original, Start, End).Transactions.Select(t => t.ExternalId).ToHashSet();
        Assert.That(_generator.Generate(modified, Start, End).Transactions.Any(t => ids.Contains(t.ExternalId)), Is.False);
    }

    [Test]
    public async Task ParallelRequests_HaveNoSharedGenerationState()
    {
        var scenario = new SampleScenarioLoader().Scenarios[0];
        var expected = _generator.Generate(scenario, Start, End);
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => _generator.Generate(scenario, Start, End))));
        foreach (var result in results)
        {
            Assert.That(result.Transactions, Is.EqualTo(expected.Transactions));
            Assert.That(result.Rules, Is.EqualTo(expected.Rules));
        }
    }

    [Test]
    public void Rules_CoverAllMerchantAlternativesUsingActualMatchingAndDoNotDependOnRange()
    {
        foreach (var scenario in new SampleScenarioLoader().Scenarios)
        {
            var data = _generator.Generate(scenario, Start, Start);
            var rules = data.Rules.Select(r => new PayeeMatchingRule { PayeePattern = r.PayeePattern, Category = CategoryHelper.SanitizeCategory(r.Category) }).ToArray();
            foreach (var pattern in scenario.Patterns)
            {
                foreach (var payee in pattern.Payees)
                {
                    Assert.That(PayeeMatchingHelper.FindBestMatch(payee, rules), Is.EqualTo(CategoryHelper.SanitizeCategory(pattern.Category)), payee);
                }
            }
            Assert.That(data.Rules, Is.EqualTo(_generator.Generate(scenario, Start, End).Rules));
        }
    }

    [Test]
    public void RepeatedMerchants_AreDeduplicatedWithinDatasetOnly()
    {
        var document = SampleDataTestDefinitions.Document().Replace("[\"Store A\", \"Store B\"]", "[\"Store A\", \"STORE A\", \"Store B\"]", StringComparison.Ordinal);
        var scenario = SampleScenarioLoader.Parse(document, "repeated");
        Assert.That(_generator.Generate(scenario, Start, End).Rules, Has.Length.EqualTo(2));
    }

    [Test]
    public void EquivalentSanitizedMerchantCategoryPairs_ProduceOnlyOneRuleEach()
    {
        var document = SampleDataTestDefinitions.Document();
        var second = document[document.IndexOf("[[patterns]]", StringComparison.Ordinal)..]
            .Replace("spending", "second", StringComparison.Ordinal)
            .Replace("Food:Groceries", " food :: groceries ", StringComparison.Ordinal);
        var scenario = SampleScenarioLoader.Parse(document + "\n" + second, "equivalent");
        Assert.That(_generator.Generate(scenario, Start, End).Rules, Has.Length.EqualTo(2));
    }

    [Test]
    public void Limits_AllowExactly10000AndReject10001BeforeReturningDataset()
    {
        var exact = SampleDataTestDefinitions.Scenario("yearly", 10000, "-100000");
        Assert.That(_generator.Generate(exact, Start, End).Transactions, Has.Length.EqualTo(10000));
        var extra = new SampleScenario(exact.Id, 1, exact.Name, exact.Description,
            [.. exact.Patterns, SampleDataTestDefinitions.Scenario("yearly").Patterns[0]]);
        Assert.Throws<ValidationException>(() => _generator.Generate(extra, Start, End));
    }

    [Test]
    public void Limits_RejectMissingReversedAndOverYearDatesButAllowLeapYearAndRollingYear()
    {
        var scenario = SampleDataTestDefinitions.Scenario();
        Assert.Throws<ValidationException>(() => _generator.Generate(scenario, default, End));
        Assert.Throws<ValidationException>(() => _generator.Generate(scenario, End, Start));
        Assert.Throws<ValidationException>(() => _generator.Generate(scenario, Start, new(2025, 1, 1)));
        Assert.DoesNotThrow(() => _generator.Generate(scenario, Start, End));
        Assert.DoesNotThrow(() => _generator.Generate(scenario, new(2024, 6, 1), new(2025, 5, 31)));
    }
}
