using YoFi.V3.Application;
using YoFi.V3.BackEnd.Startup;
using YoFi.V3.Data;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add version information to the configuration
builder.AddVersion(); // TODO: pass logger

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSwagger();
builder.Services.AddApplicationFeatures();
builder.Services.AddDatabase(builder.Configuration);

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

#if false
// TODO: Add Authorization policies - Updated to match ADR 0009
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AccountView", policy =>
        policy.Requirements.Add(new AccountAccessRequirement("viewer", "editor", "owner")));
        
    options.AddPolicy("AccountEdit", policy =>
        policy.Requirements.Add(new AccountAccessRequirement("editor", "owner")));
        
    options.AddPolicy("AccountOwn", policy =>
        policy.Requirements.Add(new AccountAccessRequirement("owner")));
});
#endif

var app = builder.Build();

// Prepare the database
app.PrepareDatabaseAsync();
    
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
