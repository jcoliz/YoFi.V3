using Microsoft.EntityFrameworkCore;
using YoFi.V3.Data;
using YoFi.V3.Entities.Providers;

namespace YoFi.V3.Tests.Integration.Application.TestHelpers;

/// <summary>
/// Base class for Integration.Application tests.
/// </summary>
/// <remarks>
/// Provides real ApplicationDbContext with in-memory SQLite database
/// and real IDataProvider interface for testing Application Features.
/// </remarks>
public abstract class FeatureTestBase
{
    protected ApplicationDbContext _context = null!;
    protected IDataProvider _dataProvider = null!;
    private DbContextOptions<ApplicationDbContext> _options = null!;

    [SetUp]
    public async Task SetUp()
    {
        // Use in-memory SQLite database (same as Integration.Data)
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new ApplicationDbContext(_options);
        _context.Database.OpenConnection(); // Keep in-memory DB alive
        await _context.Database.EnsureCreatedAsync();

        // IDataProvider is the DbContext itself (explicit interface implementation)
        _dataProvider = _context;
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}
