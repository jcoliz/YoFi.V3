using DotNetNuxt.Abstractions;
using Moq;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Unit.Application.Entities;

[TestFixture]
public class WeatherFeatureTests
{
    private Mock<IDataProvider> _data = null!;
    private List<WeatherForecast> _forecasts = null!;
    private WeatherFeature _feature = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _forecasts = [];
        _data.Setup(data => data.Get<WeatherForecast>()).Returns(() => _forecasts.AsQueryable());
        _data.Setup(data => data.ToListNoTrackingAsync(It.IsAny<IQueryable<WeatherForecast>>()))
            .Returns((IQueryable<WeatherForecast> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _feature = new WeatherFeature(_data.Object);
    }

    [Test]
    public async Task GetWeatherForecasts_DaysZeroReturnsEmptyAndDoesNotWrite()
    {
        var result = await _feature.GetWeatherForecasts(0);

        Assert.That(result, Is.Empty);
        _data.Verify(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()), Times.Never);
    }

    [Test]
    public async Task GetWeatherForecasts_NoExistingForecastsCreatesRequestedRange()
    {
        var added = new List<IModel>();
        _data.Setup(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback((IEnumerable<IModel> models) =>
            {
                var forecasts = models.Cast<WeatherForecast>().ToList();
                added.AddRange(forecasts);
                _forecasts.AddRange(forecasts);
            });

        var result = await _feature.GetWeatherForecasts(3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(added, Has.Count.EqualTo(3));
            Assert.That(result.Select(forecast => forecast.Date).Distinct().Count(), Is.EqualTo(3));
            Assert.That(result.Select(forecast => forecast.Summary), Is.All.Not.Empty);
            Assert.That(result.Select(forecast => forecast.TemperatureC), Is.All.InRange(-20, 55));
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetWeatherForecasts_PartialExistingDataAddsOnlyMissingDates()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        _forecasts.Add(new WeatherForecast { Date = today, TemperatureC = 10, Summary = "Mild" });
        _data.Setup(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback((IEnumerable<IModel> models) => _forecasts.AddRange(models.Cast<WeatherForecast>()));

        var result = await _feature.GetWeatherForecasts(3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result.Select(forecast => forecast.Date), Is.EqualTo(new[]
            {
                today,
                today.AddDays(1),
                today.AddDays(2)
            }));
        }
        _data.Verify(data => data.AddRange(It.Is<IEnumerable<IModel>>(models =>
            models.Cast<WeatherForecast>().Count() == 2)), Times.Once);
    }

    [Test]
    public async Task GetWeatherForecasts_CompleteExistingDataReturnsItWithoutWriting()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        _forecasts.AddRange([
            new WeatherForecast { Date = today, TemperatureC = 1, Summary = "Cold" },
            new WeatherForecast { Date = today.AddDays(1), TemperatureC = 2, Summary = "Cool" }
        ]);

        var result = await _feature.GetWeatherForecasts(2);

        Assert.That(result, Is.EqualTo(_forecasts));
        _data.Verify(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()), Times.Never);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
