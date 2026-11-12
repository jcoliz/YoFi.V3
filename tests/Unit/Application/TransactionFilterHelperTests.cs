using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Unit.Application;

[TestFixture]
public class TransactionFilterHelperTests
{
    [Test]
    public void Apply_SanitizesCategoryAndTrimsTextFilters()
    {
        var transaction = new Transaction
        {
            Payee = "  Local Market  ",
            Memo = "Trip expenses",
            Splits =
            [
                new Split { Category = "Home:Utilities" }
            ]
        };

        var result = TransactionFilterHelper.Apply(
                new[] { transaction }.AsQueryable(),
                fromDate: null,
                toDate: null,
                payee: " Market ",
                category: " home :: utilities ",
                memo: " expenses ",
                amount: null,
                uncategorizedOnly: false)
            .ToArray();

        Assert.That(result, Is.EqualTo(new[] { transaction }));
    }

    [Test]
    public void Apply_UsesSharedAmountParserAndKeepsZeroActive()
    {
        var zero = new Transaction { Payee = "Zero", Amount = 0m };
        var positive = new Transaction { Payee = "Positive", Amount = 42m };
        var negative = new Transaction { Payee = "Negative", Amount = -42m };

        var zeroResult = TransactionFilterHelper.Apply(
            new[] { zero, positive, negative }.AsQueryable(),
            fromDate: null, toDate: null, payee: null, category: null, memo: null, amount: " $0.00 ",
            uncategorizedOnly: false).ToArray();
        var signedResult = TransactionFilterHelper.Apply(
            new[] { zero, positive, negative }.AsQueryable(),
            fromDate: null, toDate: null, payee: null, category: null, memo: null, amount: "+$42",
            uncategorizedOnly: false).ToArray();
        var unsignedResult = TransactionFilterHelper.Apply(
            new[] { zero, positive, negative }.AsQueryable(),
            fromDate: null, toDate: null, payee: null, category: null, memo: null, amount: "42",
            uncategorizedOnly: false).ToArray();

        Assert.That(zeroResult, Is.EqualTo(new[] { zero }));
        Assert.That(signedResult, Is.EqualTo(new[] { positive }));
        Assert.That(unsignedResult, Is.EquivalentTo(new[] { positive, negative }));
    }

    [TestCase("not-an-amount")]
    [TestCase("79228162514264337593543950336")]
    public void Apply_InvalidAmountThrowsFieldKeyedValidationException(string amount)
    {
        var exception = Assert.Throws<ValidationException>(() =>
            TransactionFilterHelper.Apply(
                Array.Empty<Transaction>().AsQueryable(),
                fromDate: null, toDate: null, payee: null, category: null, memo: null, amount,
                uncategorizedOnly: false));

        Assert.That(exception!.ParameterName, Is.EqualTo("amount"));
    }

    [Test]
    public void Apply_IncludesDateBoundsAndRejectsReversedRange()
    {
        var first = new Transaction { Date = new DateOnly(2024, 1, 1) };
        var second = new Transaction { Date = new DateOnly(2024, 1, 2) };
        var third = new Transaction { Date = new DateOnly(2024, 1, 3) };
        var transactions = new[] { first, second, third }.AsQueryable();

        var inclusiveRange = TransactionFilterHelper.Apply(
            transactions,
            fromDate: new DateOnly(2024, 1, 1),
            toDate: new DateOnly(2024, 1, 2),
            payee: null,
            category: null,
            memo: null,
            amount: null,
            uncategorizedOnly: false).ToArray();
        var exception = Assert.Throws<ValidationException>(() =>
            TransactionFilterHelper.Apply(
                transactions,
                fromDate: new DateOnly(2024, 1, 3),
                toDate: new DateOnly(2024, 1, 2),
                payee: null,
                category: null,
                memo: null,
                amount: null,
                uncategorizedOnly: false));

        Assert.That(inclusiveRange, Is.EqualTo(new[] { first, second }));
        Assert.That(exception!.ParameterName, Is.EqualTo("fromDate"));
    }

    [Test]
    public void ApplyQuickSearch_SanitizesOnlyCategoryPredicateAndOmitsEmptyCategoryPredicate()
    {
        var categoryMatch = new Transaction
        {
            Payee = "Unrelated payee",
            Splits = [new Split { Category = "Home:Utilities" }]
        };
        var literalMatch = new Transaction { Payee = "home : utilities" };
        var unrelated = new Transaction
        {
            Payee = "Unrelated",
            Splits = [new Split { Category = "Food" }]
        };
        var transactions = new[] { categoryMatch, literalMatch, unrelated }.AsQueryable();

        var sanitizedCategoryResult = TransactionSearchHelper.Apply(transactions, " home : utilities ").ToArray();
        var emptyCategoryResult = TransactionSearchHelper.Apply(transactions, ":").ToArray();

        Assert.That(sanitizedCategoryResult, Is.EquivalentTo(new[] { categoryMatch, literalMatch }));
        Assert.That(emptyCategoryResult, Is.EqualTo(new[] { literalMatch }));
    }
}
