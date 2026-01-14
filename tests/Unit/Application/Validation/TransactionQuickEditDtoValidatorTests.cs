using NUnit.Framework;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Tests.Unit.Application.Validation;

/// <summary>
/// Unit tests for TransactionQuickEditDtoValidator.
/// </summary>
/// <remarks>
/// Tests verify validation rules for transaction quick edit operations:
/// - Payee is required, cannot be whitespace, max 200 characters
/// - Memo is optional, max 1000 characters
/// - Category is optional, max 200 characters
/// </remarks>
[TestFixture]
public class TransactionQuickEditDtoValidatorTests
{
    private TransactionQuickEditDtoValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new TransactionQuickEditDtoValidator();
    }

    #region Payee Validation Tests

    [Test]
    public void Validate_ValidPayee_Passes()
    {
        // Given: DTO with valid payee
        var dto = new TransactionQuickEditDto(
            Payee: "Amazon",
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_EmptyPayee_Fails()
    {
        // Given: DTO with empty payee
        var dto = new TransactionQuickEditDto(
            Payee: "",
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have error about payee being required
        var payeeErrors = result.Errors.Where(e => e.PropertyName == nameof(TransactionQuickEditDto.Payee)).ToList();
        Assert.That(payeeErrors, Has.Count.GreaterThan(0));
        Assert.That(payeeErrors.Any(e => e.ErrorMessage.Contains("required")), Is.True);
    }

    [Test]
    public void Validate_WhitespaceOnlyPayee_Fails()
    {
        // Given: DTO with whitespace-only payee
        var dto = new TransactionQuickEditDto(
            Payee: "   ",
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have error about payee being empty or whitespace
        var payeeErrors = result.Errors.Where(e => e.PropertyName == nameof(TransactionQuickEditDto.Payee)).ToList();
        Assert.That(payeeErrors, Has.Count.GreaterThan(0));
        Assert.That(payeeErrors.Any(e => e.ErrorMessage.Contains("empty") || e.ErrorMessage.Contains("whitespace")), Is.True);
    }

    [Test]
    public void Validate_PayeeTooLong_Fails()
    {
        // Given: DTO with payee exceeding 200 characters
        var longPayee = new string('A', 201);
        var dto = new TransactionQuickEditDto(
            Payee: longPayee,
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        var payeeErrors = result.Errors.Where(e => e.PropertyName == nameof(TransactionQuickEditDto.Payee)).ToList();
        Assert.That(payeeErrors, Has.Count.GreaterThan(0));
        Assert.That(payeeErrors.Any(e => e.ErrorMessage.Contains("200 characters")), Is.True);
    }

    [Test]
    public void Validate_PayeeExactly200Characters_Passes()
    {
        // Given: DTO with payee exactly at 200 character limit
        var payee = new string('A', 200);
        var dto = new TransactionQuickEditDto(
            Payee: payee,
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_PayeeWithSpecialCharacters_Passes()
    {
        // Given: DTO with payee containing special characters
        var dto = new TransactionQuickEditDto(
            Payee: "Café & Restaurant #123",
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Memo Validation Tests

    [Test]
    public void Validate_ValidMemo_Passes()
    {
        // Given: DTO with valid memo
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: "This is a memo",
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_NullMemo_Passes()
    {
        // Given: DTO with null memo (optional field)
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_EmptyMemo_Passes()
    {
        // Given: DTO with empty string memo (allowed for optional field)
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: "",
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_MemoTooLong_Fails()
    {
        // Given: DTO with memo exceeding 1000 characters
        var longMemo = new string('A', 1001);
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: longMemo,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionQuickEditDto.Memo)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("1000 characters"));
    }

    [Test]
    public void Validate_MemoExactly1000Characters_Passes()
    {
        // Given: DTO with memo exactly at 1000 character limit
        var memo = new string('A', 1000);
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: memo,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Category Validation Tests

    [Test]
    public void Validate_ValidCategory_Passes()
    {
        // Given: DTO with valid category
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: "Groceries"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_NullCategory_Passes()
    {
        // Given: DTO with null category (optional field)
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: "Some memo text",
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void Validate_EmptyCategory_Passes()
    {
        // Given: DTO with empty string category (allowed for optional field)
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: ""
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_CategoryTooLong_Fails()
    {
        // Given: DTO with category exceeding 200 characters
        var longCategory = new string('A', 201);
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: longCategory
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionQuickEditDto.Category)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("200 characters"));
    }

    [Test]
    public void Validate_CategoryExactly200Characters_Passes()
    {
        // Given: DTO with category exactly at 200 character limit
        var category = new string('A', 200);
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: category
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_CategoryWithColons_Passes()
    {
        // Given: DTO with hierarchical category
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: "Shopping:Online:Electronics"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Combined Validation Tests

    [Test]
    public void Validate_MultipleFieldsInvalid_ReturnsAllErrors()
    {
        // Given: DTO with multiple invalid fields
        var dto = new TransactionQuickEditDto(
            Payee: "",
            Memo: new string('A', 1001),
            Category: new string('B', 201)
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have errors for multiple fields
        Assert.That(result.Errors, Has.Count.GreaterThanOrEqualTo(3));
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TransactionQuickEditDto.Payee)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TransactionQuickEditDto.Memo)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TransactionQuickEditDto.Category)), Is.True);
    }

    [Test]
    public void Validate_AllFieldsValid_Passes()
    {
        // Given: DTO with all valid fields
        var dto = new TransactionQuickEditDto(
            Payee: "Amazon",
            Memo: "Office supplies",
            Category: "Business:Office"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void Validate_AllOptionalFieldsNull_Passes()
    {
        // Given: DTO with all optional fields null
        var dto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    #endregion

    #region Real-World Quick Edit Scenarios

    [Test]
    public void Validate_QuickEditPayeeOnly_Passes()
    {
        // Given: Quick edit changing only payee
        var dto = new TransactionQuickEditDto(
            Payee: "Target",
            Memo: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_QuickEditCategoryOnly_Passes()
    {
        // Given: Quick edit changing only category
        var dto = new TransactionQuickEditDto(
            Payee: "Safeway",
            Memo: null,
            Category: "Groceries"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_QuickEditAddMemo_Passes()
    {
        // Given: Quick edit adding memo to transaction
        var dto = new TransactionQuickEditDto(
            Payee: "Costco",
            Memo: "Monthly shopping trip",
            Category: "Groceries"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_QuickEditClearCategory_Passes()
    {
        // Given: Quick edit clearing category (empty string)
        var dto = new TransactionQuickEditDto(
            Payee: "Unknown Vendor",
            Memo: "Need to research",
            Category: ""
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_QuickEditPayeeWithLongName_Passes()
    {
        // Given: Quick edit with realistic long payee name
        var dto = new TransactionQuickEditDto(
            Payee: "The Home Improvement Store and Garden Center #4567",
            Memo: null,
            Category: "Home:Maintenance"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_QuickEditWithDetailedMemo_Passes()
    {
        // Given: Quick edit with detailed memo
        var dto = new TransactionQuickEditDto(
            Payee: "Medical Center",
            Memo: "Annual checkup - Dr. Smith - Insurance claim submitted #12345",
            Category: "Health:Medical"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_QuickEditNestedCategory_Passes()
    {
        // Given: Quick edit with deeply nested category
        var dto = new TransactionQuickEditDto(
            Payee: "Electronics Store",
            Memo: null,
            Category: "Shopping:Electronics:Computers:Accessories"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion
}
