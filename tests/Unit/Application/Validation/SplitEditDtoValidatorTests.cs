using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Tests.Unit.Application.Validation;

[TestFixture]
public class SplitEditDtoValidatorTests
{
    private SplitEditDtoValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new SplitEditDtoValidator();
    }

    [Test]
    public void Validate_AmountInRange_Passes()
    {
        var amounts = new[] { -999999999m, -0.01m, 0m, 0.01m, 999999999m };

        foreach (var amount in amounts)
        {
            var result = _validator.Validate(new SplitEditDto(amount, null, null));

            Assert.That(result.IsValid, Is.True, $"Expected {amount} to be valid");
        }
    }

    [Test]
    public void Validate_AmountOutOfRange_Fails()
    {
        var amounts = new[] { -999999999.01m, 999999999.01m };

        foreach (var amount in amounts)
        {
            var result = _validator.Validate(new SplitEditDto(amount, null, null));

            Assert.That(result.IsValid, Is.False, $"Expected {amount} to be invalid");
            Assert.That(result.Errors.Single().PropertyName, Is.EqualTo(nameof(SplitEditDto.Amount)));
        }
    }

    [Test]
    public void Validate_BlankCategoryAndOptionalMemo_Passes()
    {
        var result = _validator.Validate(new SplitEditDto(-10m, string.Empty, null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_CategoryTooLong_Fails()
    {
        var result = _validator.Validate(new SplitEditDto(10m, new string('a', 201), null));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Single().PropertyName, Is.EqualTo(nameof(SplitEditDto.Category)));
    }

    [Test]
    public void Validate_MemoTooLong_Fails()
    {
        var result = _validator.Validate(new SplitEditDto(10m, "Groceries", new string('a', 501)));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Single().PropertyName, Is.EqualTo(nameof(SplitEditDto.Memo)));
    }
}
