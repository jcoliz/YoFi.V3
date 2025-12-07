using System.ComponentModel.DataAnnotations.Schema;

namespace YoFi.V3.Entities.Models;

[Table("YoFi.V3.WeatherForecasts")]
public record WeatherForecast : IModel
{
    public long Id { get; set; }
    public Guid Key { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }

    public int TemperatureF => 32 + (TemperatureC * 9 / 5);
}
