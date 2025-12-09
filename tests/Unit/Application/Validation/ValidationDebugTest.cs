using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Tests.Unit.Application.Validation;

public class ValidationDebugTest
{
    [Test]
    public void CanReadMaxLengthAttribute()
    {
        // Check if we can read the MaxLength attribute from Payee property
        var payeeProperty = typeof(TransactionEditDto).GetProperty(nameof(TransactionEditDto.Payee))!;
        var maxLengthAttr = payeeProperty.GetCustomAttribute<MaxLengthAttribute>();

        Assert.That(maxLengthAttr, Is.Not.Null, "MaxLengthAttribute should be present");
        Assert.That(maxLengthAttr!.Length, Is.EqualTo(200), "Max length should be 200");
    }

    [Test]
    public void CanCreateDtoWithLongPayee()
    {
        // DTO creation doesn't validate - validation happens in the feature
        var longPayee = new string('A', 201);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: longPayee
        );

        Assert.That(dto.Payee.Length, Is.EqualTo(201), "DTO creation should succeed with 201 chars");
    }
}
