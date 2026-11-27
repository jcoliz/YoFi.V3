using YoFi.V3.Application.SampleData;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Tests.Unit.Application.SampleData;

internal static class SampleDataTestDefinitions
{
    internal static string Document(string frequency = "monthly", int repeats = 1, string annual = "-1200.00",
        string amountJitter = "none", string dateJitter = "none") => $$"""
        id = "test-scenario"
        revision = 1
        name = "Test Scenario"
        description = "Test definition."

        [[patterns]]
        id = "spending"
        category = "Food:Groceries"
        payees = ["Store A", "Store B"]
        annual_amount = "{{annual}}"
        frequency = "{{frequency}}"
        repeats = {{repeats}}
        amount_jitter = "{{amountJitter}}"
        date_jitter = "{{dateJitter}}"
        """;

    internal static SampleScenario Scenario(string frequency = "monthly", int repeats = 1, string annual = "-1200.00",
        string amountJitter = "none", string dateJitter = "none")
        => SampleScenarioLoader.Parse(Document(frequency, repeats, annual, amountJitter, dateJitter), "test.toml");
}
