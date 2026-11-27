using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Unit;

[TestFixture]
public class ModelDateDefaultsTests
{
    [Test]
    public void Transaction_DateDefaultsToMinValue()
    {
        Assert.That(new Transaction().Date, Is.EqualTo(DateOnly.MinValue));
    }

    [Test]
    public void PayeeMatchingRule_AuditTimestampsDefaultToMinValue()
    {
        var rule = new PayeeMatchingRule();

        Assert.That(rule.CreatedAt, Is.EqualTo(DateTimeOffset.MinValue));
        Assert.That(rule.ModifiedAt, Is.EqualTo(DateTimeOffset.MinValue));
    }

    [Test]
    public void Tenant_CreatedAtDefaultsToMinValue()
    {
        Assert.That(new Tenant().CreatedAt, Is.EqualTo(DateTimeOffset.MinValue));
    }
}
