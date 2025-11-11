using YoFi.V3.Application;
using YoFi.V3.BackEnd.Startup;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSwagger();
builder.Services.AddApplicationFeatures();

// See ADR 0007 for a disussion of CORS policies.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // TODO: This needs to be specified either as a configuration option or build-time argument.
        // For now, we allow all possible values
        policy.WithOrigins(
            "http://localhost:5173",  // Local (used during development with Aspire)
            "http://localhost:5000",  // Container (used in CI pipeline)
            "https://your-custom-domain.com"  // Production (deployed to Azure)
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

//if (app.Environment.IsDevelopment())
{
    // TODO: if (startupOptions.EnableSwaggerUi)
    // TODO: Logger.Information("Enabling Swagger UI");
    app.UseSwagger();
}

app.UseCors();
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
