using NUnit.Framework;
using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Application.Tenancy.Validation;

namespace YoFi.V3.Tests.Unit.Application.Tenancy;

/// <summary>
/// Unit tests for TenantEditDtoValidator.
/// </summary>
/// <remarks>
/// Tests verify validation rules for tenant creation and updates:
/// - Name is required, cannot be whitespace, max 100 characters
/// - Description is required, cannot be whitespace, max 500 characters
/// </remarks>
[TestFixture]
public class TenantEditDtoValidatorTests
{
    private TenantEditDtoValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new TenantEditDtoValidator();
    }

    #region Name Validation Tests

    [Test]
    public void Validate_ValidName_Passes()
    {
        // Given: Valid DTO with proper name
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: "My personal budget workspace"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_EmptyName_Fails()
    {
        // Given: DTO with empty name
        var dto = new TenantEditDto(
            Name: "",
            Description: "My personal budget workspace"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have error about name being required
        var nameErrors = result.Errors.Where(e => e.PropertyName == nameof(TenantEditDto.Name)).ToList();
        Assert.That(nameErrors, Has.Count.GreaterThan(0));
        Assert.That(nameErrors.Any(e => e.ErrorMessage.Contains("required")), Is.True);
    }

    [Test]
    public void Validate_WhitespaceOnlyName_Fails()
    {
        // Given: DTO with whitespace-only name
        var dto = new TenantEditDto(
            Name: "   ",
            Description: "My personal budget workspace"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have error about name being empty
        var nameErrors = result.Errors.Where(e => e.PropertyName == nameof(TenantEditDto.Name)).ToList();
        Assert.That(nameErrors, Has.Count.GreaterThan(0));
        Assert.That(nameErrors.Any(e => e.ErrorMessage.Contains("empty") || e.ErrorMessage.Contains("whitespace")), Is.True);
    }

    [Test]
    public void Validate_NameTooLong_Fails()
    {
        // Given: DTO with name exceeding 100 characters
        var longName = new string('A', 101);
        var dto = new TenantEditDto(
            Name: longName,
            Description: "My personal budget workspace"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        var nameErrors = result.Errors.Where(e => e.PropertyName == nameof(TenantEditDto.Name)).ToList();
        Assert.That(nameErrors, Has.Count.GreaterThan(0));
        Assert.That(nameErrors.Any(e => e.ErrorMessage.Contains("100 characters")), Is.True);
    }

    [Test]
    public void Validate_NameExactly100Characters_Passes()
    {
        // Given: DTO with name exactly at 100 character limit
        var name = new string('A', 100);
        var dto = new TenantEditDto(
            Name: name,
            Description: "My personal budget workspace"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_NameWithSpecialCharacters_Passes()
    {
        // Given: DTO with name containing special characters
        var dto = new TenantEditDto(
            Name: "Family Budget 2026 - Q1 & Q2",
            Description: "My personal budget workspace"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_NameWithLeadingAndTrailingSpaces_Passes()
    {
        // Given: DTO with name having leading and trailing spaces (but not only whitespace)
        var dto = new TenantEditDto(
            Name: "  Personal Budget  ",
            Description: "My personal budget workspace"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass (trimming is expected at application layer)
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Description Validation Tests

    [Test]
    public void Validate_ValidDescription_Passes()
    {
        // Given: Valid DTO with proper description
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: "My personal budget workspace for tracking expenses"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_EmptyDescription_Fails()
    {
        // Given: DTO with empty description
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: ""
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have error about description being required
        var descriptionErrors = result.Errors.Where(e => e.PropertyName == nameof(TenantEditDto.Description)).ToList();
        Assert.That(descriptionErrors, Has.Count.GreaterThan(0));
        Assert.That(descriptionErrors.Any(e => e.ErrorMessage.Contains("required")), Is.True);
    }

    [Test]
    public void Validate_WhitespaceOnlyDescription_Fails()
    {
        // Given: DTO with whitespace-only description
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: "   "
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have error about description being empty
        var descriptionErrors = result.Errors.Where(e => e.PropertyName == nameof(TenantEditDto.Description)).ToList();
        Assert.That(descriptionErrors, Has.Count.GreaterThan(0));
        Assert.That(descriptionErrors.Any(e => e.ErrorMessage.Contains("empty") || e.ErrorMessage.Contains("whitespace")), Is.True);
    }

    [Test]
    public void Validate_DescriptionTooLong_Fails()
    {
        // Given: DTO with description exceeding 500 characters
        var longDescription = new string('A', 501);
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: longDescription
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        var descriptionErrors = result.Errors.Where(e => e.PropertyName == nameof(TenantEditDto.Description)).ToList();
        Assert.That(descriptionErrors, Has.Count.GreaterThan(0));
        Assert.That(descriptionErrors.Any(e => e.ErrorMessage.Contains("500 characters")), Is.True);
    }

    [Test]
    public void Validate_DescriptionExactly500Characters_Passes()
    {
        // Given: DTO with description exactly at 500 character limit
        var description = new string('A', 500);
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: description
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_DescriptionWithSpecialCharacters_Passes()
    {
        // Given: DTO with description containing special characters
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: "Budget workspace for 2026 - includes all expenses & income (personal/business)"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_DescriptionWithMultipleLines_Passes()
    {
        // Given: DTO with description containing line breaks
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: "Personal budget workspace.\nIncludes all monthly expenses.\nTracks income and savings goals."
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Combined Validation Tests

    [Test]
    public void Validate_BothFieldsInvalid_ReturnsAllErrors()
    {
        // Given: DTO with both fields invalid
        var dto = new TenantEditDto(
            Name: "",
            Description: "   "
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have errors for both fields
        Assert.That(result.Errors, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TenantEditDto.Name)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TenantEditDto.Description)), Is.True);
    }

    [Test]
    public void Validate_BothFieldsTooLong_ReturnsAllErrors()
    {
        // Given: DTO with both fields exceeding maximum length
        var dto = new TenantEditDto(
            Name: new string('A', 101),
            Description: new string('B', 501)
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have errors for both fields
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TenantEditDto.Name)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(TenantEditDto.Description)), Is.True);
    }

    [Test]
    public void Validate_AllFieldsValid_Passes()
    {
        // Given: DTO with all valid fields
        var dto = new TenantEditDto(
            Name: "Personal Budget",
            Description: "My personal budget workspace for tracking all monthly expenses and income"
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
    public void Validate_RealWorldPersonalBudget_Passes()
    {
        // Given: Real-world personal budget tenant
        var dto = new TenantEditDto(
            Name: "Smith Family Budget",
            Description: "Budget workspace for the Smith family to track household expenses, income, and savings goals."
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldBusinessBudget_Passes()
    {
        // Given: Real-world business budget tenant
        var dto = new TenantEditDto(
            Name: "ACME Corp Q1 2026",
            Description: "Financial tracking workspace for ACME Corporation's first quarter 2026 operations, including departmental budgets and expenses."
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldProjectBudget_Passes()
    {
        // Given: Real-world project budget tenant
        var dto = new TenantEditDto(
            Name: "Website Redesign Project",
            Description: "Budget workspace for tracking all expenses related to the company website redesign project, including contractor payments, software licenses, and hosting costs."
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldSharedBudget_Passes()
    {
        // Given: Real-world shared budget tenant
        var dto = new TenantEditDto(
            Name: "Roommate Expenses 2026",
            Description: "Shared budget for apartment #402 roommates to track rent, utilities, groceries, and other shared expenses."
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldMinimalDescription_Passes()
    {
        // Given: Real-world tenant with minimal description
        var dto = new TenantEditDto(
            Name: "Test",
            Description: "Testing workspace"
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldLongDescription_Passes()
    {
        // Given: Real-world tenant with detailed description near character limit
        var dto = new TenantEditDto(
            Name: "Enterprise Budget",
            Description: "Comprehensive financial tracking workspace for Enterprise Operations Division. " +
                        "This workspace consolidates all departmental budgets, tracks project expenses, " +
                        "monitors vendor payments, and provides real-time visibility into spending patterns. " +
                        "Includes integration with procurement systems, automated expense categorization, " +
                        "and monthly reconciliation workflows. Used by Finance, Accounting, and Department Heads."
        );

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion
}
