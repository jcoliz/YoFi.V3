using System.Globalization;
using System.Xml.Linq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Unit.Application.SampleData;

[TestFixture]
public class SampleOfxWriterTests
{
    [Test]
    public async Task RoundTrip_PreservesEscapedCompleteMerchantTextSignsDatesAndIdsUnderNonEnglishCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            TransactionEditDto[] transactions =
            [
                new(new(2024, 2, 29), -12.34m, "A & B <Market> \"Special\" 'Shop' " + new string('x', 100), null, SampleDataGenerator.Source, "sample:one", "Food"),
                new(new(2024, 3, 1), 456.78m, "Income & Co", null, SampleDataGenerator.Source, "sample:two", "Income")
            ];
            using var stream = SampleOfxWriter.Write(transactions, new(2024, 2, 29), new(2024, 3, 1));
            var xml = XDocument.Load(stream);
            stream.Position = 0;
            var parsed = await OfxParsingHelper.ParseAsync(stream, "sample.ofx");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(parsed.Errors.Select(e => e.Message), Is.Empty);
                Assert.That(parsed.Transactions.Select(t => (t.Date, t.Amount, t.Payee, t.ExternalId, t.Source)),
                    Is.EqualTo(transactions.Select(t => (t.Date, t.Amount, t.Payee, t.ExternalId, t.Source))));
            }
            Assert.That(xml.Descendants("BALAMT").Single().Value, Is.EqualTo("444.44"));
            Assert.That(xml.Descendants("DTSTART").Single().Value, Does.StartWith("20240229"));
            Assert.That(xml.Descendants("DTEND").Single().Value, Does.StartWith("20240301"));
            Assert.That(xml.Descendants("CATEGORY"), Is.Empty);
            Assert.That(xml.Descendants("NAME").First().Value.Length, Is.EqualTo(32));
            Assert.That(xml.Descendants("MEMO").First().Value, Is.EqualTo(transactions[0].Payee));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public async Task EmptyStatement_StillParsesAndHasRequestedDatesAndZeroBalance()
    {
        using var stream = SampleOfxWriter.Write([], new(2024, 1, 2), new(2024, 1, 3));
        var xml = XDocument.Load(stream);
        stream.Position = 0;
        var parsed = await OfxParsingHelper.ParseAsync(stream, "empty.ofx");
        Assert.That(parsed.Errors.Select(e => e.Message), Is.Empty);
        Assert.That(parsed.Transactions, Is.Empty);
        Assert.That(xml.Descendants("BALAMT").Single().Value, Is.EqualTo("0.00"));
    }

    [Test]
    public async Task OverlappingOfx_ExercisesExistingDuplicateDetectionAndMatchingRules()
    {
        var scenario = new SampleScenarioLoader().Scenarios[0];
        var generator = new SampleDataGenerator();
        var loaded = generator.Generate(scenario, new(2024, 1, 1), new(2024, 2, 29));
        var ofx = generator.Generate(scenario, new(2024, 2, 1), new(2024, 3, 31));
        using var stream = SampleOfxWriter.Write(ofx.Transactions, new(2024, 2, 1), new(2024, 3, 31));
        var parsed = await OfxParsingHelper.ParseAsync(stream, "overlap.ofx");
        Assert.That(parsed.Errors.Select(e => e.Message), Is.Empty);
        var existing = loaded.Transactions.ToDictionary(t => t.ExternalId!, t =>
            new Transaction { ExternalId = t.ExternalId, Date = t.Date, Payee = t.Payee, Amount = t.Amount });
        var rules = loaded.Rules.Select(r => new PayeeMatchingRule { PayeePattern = r.PayeePattern, Category = CategoryHelper.SanitizeCategory(r.Category) }).ToArray();
        foreach (var transaction in parsed.Transactions)
        {
            var (status, _) = ImportReviewFeature.DetectDuplicate(transaction, existing, new Dictionary<string, ImportReviewTransaction>());
            Assert.That(status, Is.EqualTo(transaction.Date.Month == 2 ? DuplicateStatus.ExactDuplicate : DuplicateStatus.New));
            var expected = ofx.Transactions.Single(t => t.ExternalId == transaction.ExternalId);
            Assert.That(PayeeMatchingHelper.FindBestMatch(transaction.Payee, rules), Is.EqualTo(CategoryHelper.SanitizeCategory(expected.Category)));
        }
    }
}
