using YoFi.V3.Application.Dto;

namespace YoFi.V3.Tests.Unit.Application.Validation;

public class ValidationDebugTest
{
    [Test]
    public void CanCreateDtoWithLongPayee()
    {
        // Given: A payee that exceeds the 200-character validation limit

        // When: DTO is created with long payee
        var longPayee = new string('A', 201);
        var dto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 100m,
            Payee: longPayee,
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null);

        // Then: DTO creation should succeed (validation happens at controller boundary)
        Assert.That(dto.Payee.Length, Is.EqualTo(201));
    }
}
