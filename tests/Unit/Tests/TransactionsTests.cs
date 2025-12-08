namespace YoFi.V3.Tests.Unit.Tests;

using NUnit.Framework;
using NUnit.Framework.Internal;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

[TestFixture]
public class TransactionsTests
{
    private TransactionsFeature _transactionsFeature;
    private InMemoryDataProvider _dataProvider;
    private TestTenantProvider _tenantProvider = new TestTenantProvider();

    [SetUp]
    public void Setup()
    {
        _dataProvider = new InMemoryDataProvider();
        _tenantProvider = new TestTenantProvider();
        _transactionsFeature = new TransactionsFeature(_tenantProvider, _dataProvider);
    }

}
