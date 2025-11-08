using YoFi.V3.Application.Features;
using YoFi.V3.BackEnd.Startup;
using YoFi.V3.Entities.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddSwagger();

builder.Services.AddScoped<WeatherFeature>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // TODO: if (startupOptions.EnableSwaggerUi)
    // TODO: Logger.Information("Enabling Swagger UI");
    app.UseSwagger();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/weatherforecast", (WeatherFeature weatherFeature) =>
{
    var forecast = weatherFeature.GetWeatherForecasts(5);
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();