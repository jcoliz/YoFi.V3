var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApiDocument(options =>
{
    options.Title = "YoFi.V3 Backend";
    options.Description = "Application boundary between .NET backend and YoFi.V3 frontend.";
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseOpenApi();
app.UseSwaggerUi();
app.MapControllers();

app.Run();
