using System.Reflection;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using YoFi.V3.Application;
using YoFi.V3.BackEnd.Startup;
using YoFi.V3.Data;
using YoFi.V3.Entities.Options;


ILogger? logger = default;
try
{
    //
    // Set up Startup logger
    //

    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddConsole();
        builder.AddEventSourceLogger();
    });
    logger = loggerFactory.CreateLogger("Startup");
    logger.LogInformation("Starting {App} Process ID: {ProcessId}, Thread ID: {ThreadId}",
        Assembly.GetExecutingAssembly().FullName,
        Environment.ProcessId,
        Environment.CurrentManagedThreadId);

    //
    // Set up Web application
    //
    var builder = WebApplication.CreateBuilder(args);

    // Get application options, which can be used during startup configuration
    ApplicationOptions applicationOptions = new();
    builder.Configuration.Bind(ApplicationOptions.Section, applicationOptions);

    // Add service defaults & Aspire client integrations.
    builder.AddServiceDefaults(logger);

    // Add version information to the configuration
    builder.AddApplicationOptions(logger);

    //
    // Add services to the container.
    //

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddSwagger();
    builder.Services.AddApplicationFeatures();
    builder.Services.AddDatabase(builder.Configuration);

    //
    // Add Identity services
    //

    // Configure JWT options
    builder.Services.Configure<JwtOptions>(
        builder.Configuration.GetSection(JwtOptions.SectionName));

    // Add Identity
    builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // Add NuxtIdentity
    builder.Services.AddNuxtIdentity<IdentityUser>();
    builder.Services.AddNuxtIdentityEntityFramework<ApplicationDbContext>();
    builder.Services.AddNuxtIdentityAuthentication();

    // See ADR 0007 for a disussion of CORS policies.
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if ( applicationOptions.AllowedCorsOrigins.Length == 0)
            {
                logger.LogError("No allowed CORS origins configured. Please set Application:AllowedCorsOrigins in configuration.");
            }
            else
            {
                policy.WithOrigins(applicationOptions.AllowedCorsOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
                logger.LogInformation(4,"CORS configured with allowed origins: {Origins}", string.Join(", ", applicationOptions.AllowedCorsOrigins));
            }
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

    //
    // Build the app
    //

    var app = builder.Build();

    // Prepare the database
    app.PrepareDatabaseAsync();

    // Configure the HTTP request pipeline.
    app.UseExceptionHandler();

    if (applicationOptions.Environment == EnvironmentType.Production)
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

// During development phase, we'll keep swagger up even in non-development environments.
#if false
    if (applicationOptions.Environment != EnvironmentType.Production)
#endif
    {
        logger.LogInformation(8,"Enabling Swagger UI");
        app.UseSwagger();
    }

    app.UseCors();
    app.MapDefaultEndpoints();
    app.MapControllers();

    logger.LogInformation(10, "OK. Environment: {Environment}", applicationOptions.Environment);
    logger.LogInformation("[DIAG] ==================== APP READY ====================");

    app.Run();

    logger.LogInformation("[DIAG] ==================== APP STOPPED ====================");

}
catch (Exception ex)
{
    if (logger is not null)
    {
        logger?.LogCritical(ex, "Failed to start {App}", Assembly.GetExecutingAssembly().FullName);
    }
    else
    {
        Console.WriteLine("CRITICAL: Failed to start application");
        Console.WriteLine(ex.ToString());
    }
}
