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

    [Test]
    public async Task UpdateRange_UpdatesExistingEntities()
    {
        // Given: Existing weather forecasts in the database
        IDataProvider provider = _context;
        var forecasts = new List<WeatherForecast>
        {
            new() { Date = DateOnly.FromDateTime(DateTime.Now), TemperatureC = 20, Summary = "Sunny" },
            new() { Date = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), TemperatureC = 25, Summary = "Hot" }
        };
        
        _context.WeatherForecasts.AddRange(forecasts);
        await _context.SaveChangesAsync();

        // When: Updating the forecasts
        forecasts[0].Summary = "Cloudy";
        forecasts[1].TemperatureC = 30;
        provider.UpdateRange(forecasts.Cast<IModel>());
        await _context.SaveChangesAsync();

        // Then: The entities should be updated in the database
        var updated = await _context.WeatherForecasts.ToListAsync();
        Assert.That(updated.First(f => f.Id == forecasts[0].Id).Summary, Is.EqualTo("Cloudy"));
        Assert.That(updated.First(f => f.Id == forecasts[1].Id).TemperatureC, Is.EqualTo(30));
    }

    [Test]
    public async Task ToListAsync_ReturnsTrackedEntities()
    {
        // Given: A data provider with a weather forecast
        IDataProvider provider = _context;
        _context.WeatherForecasts.Add(
            new WeatherForecast() { Date = DateOnly.FromDateTime(DateTime.Now), TemperatureC = 20, Summary = "Sunny" }
        );
        await _context.SaveChangesAsync();

        // When: Retrieving the weather forecasts with tracking
        var query = provider.Get<WeatherForecast>();
        var results = await provider.ToListAsync(query);

        // Then: It should return the forecast with tracking enabled
        Assert.That(results, Has.Count.EqualTo(1));
        var entity = results.First();
        var state = _context.Entry(entity).State;
        Assert.That(state, Is.EqualTo(EntityState.Unchanged)); // Tracked
    }

    [Test]
    public async Task SaveChangesAsync_SavesTrackedChanges()
    {
        // Given: A tracked entity that has been modified
        IDataProvider provider = _context;
        var forecast = new WeatherForecast() 
        { 
            Date = DateOnly.FromDateTime(DateTime.Now), 
            TemperatureC = 20, 
            Summary = "Sunny" 
        };
        _context.WeatherForecasts.Add(forecast);
        await _context.SaveChangesAsync();

        // When: Modifying the tracked entity and calling SaveChangesAsync
        forecast.Summary = "Rainy";
        var changeCount = await provider.SaveChangesAsync();

        // Then: The change should be persisted
        Assert.That(changeCount, Is.EqualTo(1));
        var updated = await _context.WeatherForecasts.FirstAsync(f => f.Id == forecast.Id);
        Assert.That(updated.Summary, Is.EqualTo("Rainy"));
    }

    [Test]
    public async Task WeatherForecast_HasCorrectTableName()
    {
        // Given: A weather forecast
        var forecast = new WeatherForecast()
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = 20,
            Summary = "Sunny"
        };
        
        _context.WeatherForecasts.Add(forecast);
        await _context.SaveChangesAsync();

        // When: Checking the table name in the database
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='YoFi.V3.WeatherForecasts'";
        var tableName = await command.ExecuteScalarAsync();

        // Then: The table should exist with the correct name
        Assert.That(tableName, Is.EqualTo("YoFi.V3.WeatherForecasts"));
    }

    [Test]
    public async Task WeatherForecast_IdIsAutoGenerated()
    {
        // Given: A weather forecast without an ID set
        var forecast = new WeatherForecast()
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = 20,
            Summary = "Sunny"
        };
        
        Assert.That(forecast.Id, Is.EqualTo(0)); // Initially zero

        // When: Adding and saving the forecast
        _context.WeatherForecasts.Add(forecast);
        await _context.SaveChangesAsync();

        // Then: The ID should be auto-generated
        Assert.That(forecast.Id, Is.GreaterThan(0));
    }

    // TODO: This should move to unit tests
    [Test]
    public async Task WeatherForecast_TemperatureFProperty_IsCalculated()
    {
        // Given: A weather forecast with Celsius temperature
        var forecast = new WeatherForecast()
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = 20,
            Summary = "Sunny"
        };

        // When: Accessing the Fahrenheit property
        var fahrenheit = forecast.TemperatureF;

        // Then: It should be correctly calculated
        Assert.That(fahrenheit, Is.EqualTo(68)); // 20°C = 68°F
    }

    [Test]
    public void Add_WithNullEntity_ThrowsException()
    {
        // Given: A data provider
        IDataProvider provider = _context;

        // When/Then: Adding null should throw an exception
        Assert.ThrowsAsync<ArgumentNullException>(() => 
        {
            provider.Add(null!);
            return _context.SaveChangesAsync();
        });
    }

    [Test]
    public async Task Get_WithEmptyDatabase_ReturnsEmptyQueryable()
    {
        // Given: An empty database
        IDataProvider provider = _context;

        // When: Querying for weather forecasts
        var query = provider.Get<WeatherForecast>();
        var results = await provider.ToListNoTrackingAsync(query);

        // Then: Should return empty list
        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task AddRange_WithEmptyCollection_DoesNotThrow()
    {
        // Given: An empty collection
        IDataProvider provider = _context;
        var emptyList = new List<IModel>();

        // When: Adding empty range
        provider.AddRange(emptyList);
        var changeCount = await _context.SaveChangesAsync();

        // Then: Should complete without error and save no changes
        Assert.That(changeCount, Is.EqualTo(0));
    }

    [Test]
    [Explicit("SQLite doesn't support true concurrency testing with in-memory databases")]
    public async Task ConcurrentUpdates_HandledCorrectly()
    {
        // Given: Two contexts accessing the same entity
        using var context1 = new ApplicationDbContext(_options);
        using var context2 = new ApplicationDbContext(_options);
        
        // Add initial entity
        var forecast = new WeatherForecast()
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = 20,
            Summary = "Sunny"
        };
        context1.WeatherForecasts.Add(forecast);
        await context1.SaveChangesAsync();
        var id = forecast.Id;

        // When: Both contexts modify the same entity
        var entity1 = await context1.WeatherForecasts.FindAsync(id);
        var entity2 = await context2.WeatherForecasts.FindAsync(id);
        
        entity1!.Summary = "Cloudy";
        entity2!.Summary = "Rainy";
        
        await context1.SaveChangesAsync(); // First save succeeds
        
        // Then: Second save should either succeed (last-write-wins) or throw concurrency exception
        // The exact behavior depends on your concurrency strategy
        Assert.DoesNotThrowAsync(async () => await context2.SaveChangesAsync());
    }
}
