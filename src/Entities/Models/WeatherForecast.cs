using System.ComponentModel.DataAnnotations.Schema;

namespace YoFi.V3.Entities.Models;

[Table("YoFi.V3.WeatherForecasts")]
public record WeatherForecast : IModel
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC * 9 / 5);
}
