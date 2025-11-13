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

    [Test]
    public async Task Get_ReturnsQueryableSet()
    {
        // Given: A data provider with weather forecasts
        IDataProvider provider = _context;
        _context.WeatherForecasts.AddRange(
            new WeatherForecast() { Date = DateOnly.FromDateTime(DateTime.Now), TemperatureC = 20, Summary = "Sunny" },
            new WeatherForecast() { Date = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), TemperatureC = 25, Summary = "Hot" }
        );
        await _context.SaveChangesAsync();

        // When: Retrieving the weather forecasts
        var query = provider.Get<WeatherForecast>();
        var results = await provider.ToListNoTrackingAsync(query);

        // Then: It should return all added forecasts
        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ToListNoTrackingAsync_ReturnsUntrackedEntities()
    {
        // Given: A data provider with a weather forecast
        IDataProvider provider = _context;
        _context.WeatherForecasts.Add(
            new WeatherForecast() { Date = DateOnly.FromDateTime(DateTime.Now), TemperatureC = 20, Summary = "Sunny" }
        );
        await _context.SaveChangesAsync();

        // When: Retrieving the weather forecasts without tracking
        var query = provider.Get<WeatherForecast>();
        var results = await provider.ToListNoTrackingAsync(query);

        // Then: It should return the forecast without tracking
        Assert.That(results, Has.Count.EqualTo(1));
        var entity = results.First();
        var state = _context.Entry(entity).State;
        Assert.That(state, Is.EqualTo(EntityState.Detached)); // Not tracked
    }

    [Test]
    public async Task AddRange_AddsMultipleEntities()
    {
        // Given: A list of weather forecasts
        IDataProvider provider = _context;
        var forecasts = new List<IModel>
        {
            new WeatherForecast() { Date = DateOnly.FromDateTime(DateTime.Now), TemperatureC = 20, Summary = "Sunny" },
            new WeatherForecast() { Date = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), TemperatureC = 25, Summary = "Hot" },
            new WeatherForecast() { Date = DateOnly.FromDateTime(DateTime.Now.AddDays(2)), TemperatureC = 18, Summary = "Cool" }
        };

        // When: Adding the range of forecasts
        provider.AddRange(forecasts);
        await _context.SaveChangesAsync();

        // Then: All forecasts should be saved in the database
        var count = await _context.WeatherForecasts.CountAsync();
        Assert.That(count, Is.EqualTo(3));
    }

}
