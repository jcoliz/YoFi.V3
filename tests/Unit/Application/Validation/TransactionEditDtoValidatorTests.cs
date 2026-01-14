using NUnit.Framework;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Tests.Unit.Application.Validation;

/// <summary>
/// Unit tests for TransactionEditDtoValidator.
/// </summary>
/// <remarks>
/// Tests verify validation rules for transaction creation and updates:
/// - Date must be within 50 years in the past and 5 years in the future
/// - Amount must be non-zero (can be negative for credits/refunds)
/// - Payee is required, cannot be whitespace, max 200 characters
/// - Memo, Source, ExternalId, Category are optional with max length constraints
/// </remarks>
[TestFixture]
public class TransactionEditDtoValidatorTests
{
    private TransactionEditDtoValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new TransactionEditDtoValidator();
    }

    #region Date Validation Tests

    [Test]
    public void Validate_ValidDate_Passes()
    {
        // Given: Valid DTO with date within valid range
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_DateExactly50YearsAgo_Passes()
    {
        // Given: DTO with date exactly at minimum range (50 years ago)
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-50)),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_DateExactly5YearsInFuture_Passes()
    {
        // Given: DTO with date exactly at maximum range (5 years in future)
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5)),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_DateMoreThan50YearsAgo_Fails()
    {
        // Given: DTO with date more than 50 years in the past
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-51)),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention date range
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionEditDto.Date)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("50 years"));
    }

    [Test]
    public void Validate_DateMoreThan5YearsInFuture_Fails()
    {
        // Given: DTO with date more than 5 years in the future
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(6)),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention date range
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionEditDto.Date)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("5 years"));
    }

    #endregion

    #region Amount Validation Tests

    [Test]
    public void Validate_NegativeAmount_Passes()
    {
        // Given: DTO with negative amount (credit/refund)
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: -50.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_ZeroAmount_Fails()
    {
        // Given: DTO with zero amount
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 0m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention zero amount
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionEditDto.Amount)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("zero"));
    }

    [Test]
    public void Validate_VerySmallPositiveAmount_Passes()
    {
        // Given: DTO with very small positive amount
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 0.01m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_VerySmallNegativeAmount_Passes()
    {
        // Given: DTO with very small negative amount
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: -0.01m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_VeryLargeAmount_Passes()
    {
        // Given: DTO with very large amount
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 999999999.99m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Payee Validation Tests

    [Test]
    public void Validate_ValidPayee_Passes()
    {
        // Given: DTO with valid payee
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Amazon",
            Memo: null,
            Source: null,
            ExternalId: null,
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have error about payee being required
        var payeeErrors = result.Errors.Where(e => e.PropertyName == nameof(TransactionEditDto.Payee)).ToList();
        Assert.That(payeeErrors, Has.Count.GreaterThan(0));
        Assert.That(payeeErrors.Any(e => e.ErrorMessage.Contains("required")), Is.True);
    }

    [Test]
    public void Validate_WhitespaceOnlyPayee_Fails()
    {
        // Given: DTO with whitespace-only payee
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "   ",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have error about payee being empty
        var payeeErrors = result.Errors.Where(e => e.PropertyName == nameof(TransactionEditDto.Payee)).ToList();
        Assert.That(payeeErrors, Has.Count.GreaterThan(0));
        Assert.That(payeeErrors.Any(e => e.ErrorMessage.Contains("empty")), Is.True);
    }

    [Test]
    public void Validate_PayeeTooLong_Fails()
    {
        // Given: DTO with payee exceeding 200 characters
        var longPayee = new string('A', 201);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: longPayee,
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        var payeeErrors = result.Errors.Where(e => e.PropertyName == nameof(TransactionEditDto.Payee)).ToList();
        Assert.That(payeeErrors, Has.Count.GreaterThan(0));
        Assert.That(payeeErrors.Any(e => e.ErrorMessage.Contains("200 characters")), Is.True);
    }

    [Test]
    public void Validate_PayeeExactly200Characters_Passes()
    {
        // Given: DTO with payee exactly at 200 character limit
        var payee = new string('A', 200);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: payee,
            Memo: null,
            Source: null,
            ExternalId: null,
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Café & Restaurant #123",
            Memo: null,
            Source: null,
            ExternalId: null,
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: "This is a memo",
            Source: null,
            ExternalId: null,
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: longMemo,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionEditDto.Memo)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("1000 characters"));
    }

    [Test]
    public void Validate_MemoExactly1000Characters_Passes()
    {
        // Given: DTO with memo exactly at 1000 character limit
        var memo = new string('A', 1000);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: memo,
            Source: null,
            ExternalId: null,
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: "",
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Source Validation Tests

    [Test]
    public void Validate_ValidSource_Passes()
    {
        // Given: DTO with valid source
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: "Bank Import",
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_SourceTooLong_Fails()
    {
        // Given: DTO with source exceeding 200 characters
        var longSource = new string('A', 201);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: longSource,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionEditDto.Source)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("200 characters"));
    }

    [Test]
    public void Validate_SourceExactly200Characters_Passes()
    {
        // Given: DTO with source exactly at 200 character limit
        var source = new string('A', 200);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: source,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region ExternalId Validation Tests

    [Test]
    public void Validate_ValidExternalId_Passes()
    {
        // Given: DTO with valid external ID
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: "TXN-123456",
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_ExternalIdTooLong_Fails()
    {
        // Given: DTO with external ID exceeding 100 characters
        var longExternalId = new string('A', 101);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: longExternalId,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionEditDto.ExternalId)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("100 characters"));
    }

    [Test]
    public void Validate_ExternalIdExactly100Characters_Passes()
    {
        // Given: DTO with external ID exactly at 100 character limit
        var externalId = new string('A', 100);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: externalId,
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Groceries"
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: longCategory
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(TransactionEditDto.Category)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("200 characters"));
    }

    [Test]
    public void Validate_CategoryExactly200Characters_Passes()
    {
        // Given: DTO with category exactly at 200 character limit
        var category = new string('A', 200);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: category
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_EmptyCategory_Passes()
    {
        // Given: DTO with empty string category (allowed for optional field)
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: ""
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-60)),
            Amount: 0m,
            Payee: "",
            Memo: new string('A', 1001),
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have errors for multiple fields
        Assert.That(result.Errors, Has.Count.GreaterThanOrEqualTo(3));
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TransactionEditDto.Date)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TransactionEditDto.Amount)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TransactionEditDto.Payee)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TransactionEditDto.Memo)), Is.True);
    }

    [Test]
    public void Validate_AllFieldsValid_Passes()
    {
        // Given: DTO with all valid fields
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Amazon",
            Memo: "Office supplies",
            Source: "Bank Import",
            ExternalId: "TXN-123456",
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
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100.00m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    #endregion

    #region Real-World Scenarios

    [Test]
    public void Validate_RealWorldGroceryTransaction_Passes()
    {
        // Given: Real-world grocery transaction
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 67.42m,
            Payee: "Safeway #1234",
            Memo: "Weekly groceries",
            Source: null,
            ExternalId: null,
            Category: "Groceries"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldRefundTransaction_Passes()
    {
        // Given: Real-world refund transaction with negative amount
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            Amount: -25.00m,
            Payee: "Amazon",
            Memo: "Return - item was damaged",
            Source: null,
            ExternalId: null,
            Category: "Returns"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldBankImportTransaction_Passes()
    {
        // Given: Real-world transaction from bank import with external ID
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            Amount: 1234.56m,
            Payee: "Payroll Deposit - ACME Corp",
            Memo: "Salary payment",
            Source: "Chase_Checking.ofx",
            ExternalId: "20260109-TXN-987654321",
            Category: "Income:Salary"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldHistoricalTransaction_Passes()
    {
        // Given: Real-world historical transaction from 30 years ago
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Amount: 15.99m,
            Payee: "Blockbuster Video",
            Memo: "Movie rental",
            Source: null,
            ExternalId: null,
            Category: "Entertainment"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldScheduledFutureTransaction_Passes()
    {
        // Given: Real-world scheduled transaction 1 year in future
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Amount: 500.00m,
            Payee: "Insurance Company",
            Memo: "Annual premium",
            Source: null,
            ExternalId: null,
            Category: "Insurance:Auto"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion
}
