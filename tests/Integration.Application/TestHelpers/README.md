# Test Helpers

Supporting classes for Integration.Application tests.

## FeatureTestBase

Base class for all Application Integration tests. Provides:

- **Real ApplicationDbContext** with in-memory SQLite database
- **Real IDataProvider** interface (DbContext implements it)

### Usage

```csharp
[TestFixture]
public class MyFeatureTests : FeatureTestBase
{
    private MyFeature _feature;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _feature = new MyFeature(_dataProvider);
    }

    [Test]
    public async Task MyTest()
    {
        // Test implementation
    }
}
```

### Available Properties

- `_context` - ApplicationDbContext instance
- `_dataProvider` - IDataProvider interface (same as _context)
