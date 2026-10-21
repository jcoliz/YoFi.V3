var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(options =>
{
    options.Title = "YoFi.V3 Backend";
    options.Description = "Application boundary between .NET backend and YoFi.V3 frontend.";
});

builder.Services.AddOpenApiDocument(options =>
{
    options.DocumentName = "TestControl";
    options.Title = "ListsWebApp Test Control API";
    options.Description = "Test control services for functional tests.";
    options.ApiGroupNames = new[] { "TestControl" };
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseOpenApi();
app.UseSwaggerUi();
app.MapControllers();

app.Run();
