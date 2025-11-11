# Considering Data Layer tests

Prompt: "Would it make sense to craft specific tests to test the data layer in isolation?"

Based on my analysis of your codebase, **yes, it would make sense to create integration tests specifically for the data layer**. Here's why:

## Current Testing Gaps

Your current test structure has:
- **Unit tests** (tests/Unit) - Testing Application layer in isolation (e.g., `WeatherTests`)
- **Functional tests** (tests/Functional) - End-to-end UI tests using Playwright

**What's missing:** Integration tests for the data layer.

## Why Data Layer Tests Are Needed

1. **Your `ApplicationDbContext` has non-trivial logic**: The `ApplicationDbContext` implements `IDataProvider` with several methods that should be tested:
   - Query building (`Get<T>()`)
   - CRUD operations (`Add`, `AddRange`, `UpdateRange`)
   - Query execution (`ToListNoTrackingAsync`, `ToListAsync`)

2. **SQLite-specific behavior**: According to ADR 0005, you're using SQLite across all environments. You need to verify that:
   - Entity Framework mappings work correctly
   - Queries generate correct SQL
   - Transactions behave as expected
   - In-memory vs. file-based databases work properly

3. **Future model complexity**: When you add more entities and relationships (beyond just `WeatherForecast`), you'll need to test:
   - Navigation properties
   - Cascading deletes
   - Complex queries with joins
   - Custom model configurations in `OnModelCreating`

## Recommended Approach

Create a new test project or folder: **`tests/Integration/`**

````csharp
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;

namespace YoFi.V3.Tests.Integration.Data;

[TestFixture]
public class ApplicationDbContextTests
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
        // Given
        var forecast = new WeatherForecast(0, DateOnly.FromDateTime(DateTime.Now), 20, "Sunny");
        IDataProvider provider = _context;

        // When
        provider.Add(forecast);
        await _context.SaveChangesAsync();

        // Then
        var saved = await _context.WeatherForecasts.FirstOrDefaultAsync();
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.Summary, Is.EqualTo("Sunny"));
        Assert.That(saved.Id, Is.GreaterThan(0)); // ID should be assigned
    }

    [Test]
    public async Task Get_ReturnsQueryableSet()
    {
        // Given
        IDataProvider provider = _context;
        _context.WeatherForecasts.AddRange(
            new WeatherForecast(0, DateOnly.FromDateTime(DateTime.Now), 20, "Sunny"),
            new WeatherForecast(0, DateOnly.FromDateTime(DateTime.Now.AddDays(1)), 25, "Hot")
        );
        await _context.SaveChangesAsync();

        // When
        var query = provider.Get<WeatherForecast>();
        var results = await provider.ToListNoTrackingAsync(query);

        // Then
        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ToListNoTrackingAsync_ReturnsUntrackedEntities()
    {
        // Given
        IDataProvider provider = _context;
        _context.WeatherForecasts.Add(
            new WeatherForecast(0, DateOnly.FromDateTime(DateTime.Now), 20, "Sunny")
        );
        await _context.SaveChangesAsync();

        // When
        var query = provider.Get<WeatherForecast>();
        var results = await provider.ToListNoTrackingAsync(query);

        // Then
        Assert.That(results, Has.Count.EqualTo(1));
        var entity = results.First();
        var state = _context.Entry(entity).State;
        Assert.That(state, Is.EqualTo(EntityState.Detached)); // Not tracked
    }

    [Test]
    public async Task AddRange_AddsMultipleEntities()
    {
        // Given
        IDataProvider provider = _context;
        var forecasts = new List<IModel>
        {
            new WeatherForecast(0, DateOnly.FromDateTime(DateTime.Now), 20, "Sunny"),
            new WeatherForecast(0, DateOnly.FromDateTime(DateTime.Now.AddDays(1)), 25, "Hot"),
            new WeatherForecast(0, DateOnly.FromDateTime(DateTime.Now.AddDays(2)), 18, "Cool")
        };

        // When
        provider.AddRange(forecasts);
        await _context.SaveChangesAsync();

        // Then
        var count = await _context.WeatherForecasts.CountAsync();
        Assert.That(count, Is.EqualTo(3));
    }
}
````

## Benefits

1. **Catches EF Core configuration issues** before they hit production
2. **Documents expected database behavior** through tests
3. **Validates SQLite compatibility** (important since you're using it everywhere)
4. **Complements your architecture**: Your ARCHITECTURE.md mentions considering integration tests
5. **Fast feedback**: In-memory SQLite tests run quickly
6. **Supports refactoring**: Safe to change `ApplicationDbContext` implementation

## Alignment with Your Testing Strategy

Per your ARCHITECTURE.md:
> ### Under consideration
>
> - **Integration Tests** (future) - Database/external service integration

This is the perfect time to add them, especially since your data layer is currently simple with just `WeatherForecast`.
