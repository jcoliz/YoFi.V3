namespace YoFi.V3.Tests.Unit.Application;

using YoFi.V3.Application.Helpers;

[TestFixture]
public class TransactionSearchHelperTests
{
    [TestCase("42", 42, false)]
    [TestCase("42.00", 42, false)]
    [TestCase("+42.00", 42, true)]
    [TestCase("-42.00", -42, true)]
    [TestCase("$42", 42, false)]
    [TestCase("$42.00", 42, false)]
    [TestCase("-$42", -42, true)]
    [TestCase("$-42", -42, true)]
    [TestCase("+$42", 42, true)]
    [TestCase("$+42", 42, true)]
    [TestCase("1,234.56", 1234.56, false)]
    [TestCase("$1,234.56", 1234.56, false)]
    [TestCase("-$1,234.56", -1234.56, true)]
    [TestCase("$-1,234.56", -1234.56, true)]
    [TestCase("0", 0, false)]
    [TestCase(".5", 0.5, false)]
    [TestCase("42.", 42, false)]
    public void TryParseAmount_ValidAmount_ReturnsAmountAndSign(
        string term,
        decimal expectedAmount,
        bool expectedHasExplicitSign)
    {
        var parsed = TransactionSearchHelper.TryParseAmount(term, out var result);

        Assert.That(parsed, Is.True);
        Assert.That(result.Amount, Is.EqualTo(expectedAmount));
        Assert.That(result.HasExplicitSign, Is.EqualTo(expectedHasExplicitSign));
    }

    [TestCase("")]
    [TestCase("$")]
    [TestCase("+")]
    [TestCase("-")]
    [TestCase("$$42")]
    [TestCase("--42")]
    [TestCase("+-42")]
    [TestCase("42-")]
    [TestCase("1,2")]
    [TestCase("12,34")]
    [TestCase("1,23,456")]
    [TestCase("1.234,56")]
    [TestCase("42..0")]
    [TestCase("1e2")]
    [TestCase("€42")]
    [TestCase("$42$")]
    [TestCase("79228162514264337593543950336")]
    public void TryParseAmount_InvalidAmount_ReturnsFalse(string term)
    {
        var parsed = TransactionSearchHelper.TryParseAmount(term, out _);

        Assert.That(parsed, Is.False);
    }
}
