using System.Globalization;
using System.Text.Json;
using YoFi.V3.Tests.Functional.Infrastructure.TestControl;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

[TestFixture]
public class TransactionDataStepsTests
{
    [TestCase(0, "2026-11-13")]
    [TestCase(1, "2026-11-12")]
    [TestCase(14, "2026-10-30")]
    [TestCase(-1, "2026-11-14")]
    public void DaysAgoUsesLocalCalendarDateWhenUtcIsTomorrow(int daysAgo, string expectedDate)
    {
        var localNow = new DateTimeOffset(2026, 11, 13, 16, 30, 0, TimeSpan.FromHours(-8));
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var table = new DataTable(
            ["Payee", "Days Ago"],
            ["Coffee Shop", daysAgo.ToString(CultureInfo.InvariantCulture)]);

        var transactions = TransactionDataSteps.ParseTransactions(table, today);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DateOnly.FromDateTime(localNow.UtcDateTime), Is.EqualTo(today.AddDays(1)));
            Assert.That(transactions.Single().Date,
                Is.EqualTo(DateOnly.ParseExact(expectedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
    }

    [Test]
    public void OmittedDateDefaultsToToday()
    {
        var today = new DateOnly(2026, 11, 13);
        var table = new DataTable(["Payee"], ["Coffee Shop"]);

        var transactions = TransactionDataSteps.ParseTransactions(table, today);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transactions.Single().Date, Is.EqualTo(today));
            Assert.That(transactions.Single().Amount, Is.EqualTo(100.00m));
        }
    }

    [Test]
    public void ExplicitDateTakesPrecedenceAndEachRowIsParsed()
    {
        var today = new DateOnly(2026, 11, 13);
        var table = new DataTable(
            ["Payee", "Date", "Days Ago"],
            ["Coffee Shop", "2024-01-05", "0"],
            ["Book Store", "", "1"]);

        var transactions = TransactionDataSteps.ParseTransactions(table, today);

        Assert.That(transactions.Select(transaction => transaction.Date),
            Is.EqualTo(new[] { new DateOnly(2024, 1, 5), today.AddDays(-1) }));
    }

    [Test]
    public void GeneratedClientSerializesAndReadsDateOnlyValues()
    {
        var date = new DateOnly(2026, 11, 13);
        var transaction = new TransactionEditDto { Date = date, Payee = "Coffee Shop", Amount = -5.50m };

        var json = JsonSerializer.Serialize(transaction);
        using var document = JsonDocument.Parse(json);
        var result = JsonSerializer.Deserialize<TransactionResultDto>("""{"date":"2026-11-13"}""");
        var detail = JsonSerializer.Deserialize<TransactionDetailDto>("""{"date":"2026-11-13"}""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(document.RootElement.GetProperty("date").GetString(), Is.EqualTo("2026-11-13"));
            Assert.That(JsonSerializer.Deserialize<TransactionEditDto>(json)!.Date, Is.EqualTo(date));
            Assert.That(result!.Date, Is.EqualTo(date));
            Assert.That(detail!.Date, Is.EqualTo(date));
        }
    }
}
