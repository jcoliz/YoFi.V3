using DotNetNuxt.Abstractions;

namespace YoFi.V3.Tests.Integration.Application.TestHelpers;

/// <summary>
/// Base class for Integration.Application tests.
/// </summary>
/// <remarks>
/// Provides a real ApplicationDbContext with an isolated PostgreSQL database
/// and real IDataProvider interface for testing Application Features.
/// </remarks>
public abstract class FeatureTestBase : PostgreSqlTestBase
{
    protected IDataProvider _dataProvider = null!;

    [SetUp]
    public Task SetUp()
    {
        // IDataProvider is the DbContext itself (explicit interface implementation)
        _dataProvider = _context;
        return Task.CompletedTask;
    }
}
