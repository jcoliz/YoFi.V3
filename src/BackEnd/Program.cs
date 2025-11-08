using YoFi.V3.Application.Features;
using YoFi.V3.BackEnd.Startup;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers();
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

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();