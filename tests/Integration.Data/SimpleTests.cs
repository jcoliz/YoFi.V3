using Microsoft.EntityFrameworkCore;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;

namespace YoFi.V3.Tests.Integration.Data;

public class SimpleTests
{
    private ApplicationDbContext _context;
    private DbContextOptions<ApplicationDbContext> _options;

    [SetUp]
    public void Setup()
    {
        // Use in-memory database for testing
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new ApplicationDbContext(_options);
        _context.Database.OpenConnection(); // Keep in-memory DB alive
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Test]
    public async Task Add_SavesEntityToDatabase()
    {
        // Given: A weather forecast
        var forecast = new WeatherForecast()
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = 20,
            Summary = "Sunny"
        };
        IDataProvider provider = _context;

        // When: Adding the forecast to the provider
        provider.Add(forecast);
        await _context.SaveChangesAsync();

        // Then: It should be saved in the database
        var saved = await _context.WeatherForecasts.FirstOrDefaultAsync();
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.Summary, Is.EqualTo("Sunny"));
        Assert.That(saved.Id, Is.GreaterThan(0)); // ID should be assigned
    }
}
