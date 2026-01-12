using System.Reflection;
using YoFi.V3.Application;
using YoFi.V3.BackEnd.Logging;
using YoFi.V3.BackEnd.Setup;
using YoFi.V3.BackEnd.Startup;
using YoFi.V3.Controllers;
using YoFi.V3.Controllers.Extensions;
using YoFi.V3.Controllers.Tenancy;
using YoFi.V3.Data;
using YoFi.V3.Entities.Options;


ILogger? logger = default;
try
{
    //
    // Set up Startup logger
    //

    logger = YoFi.V3.BackEnd.Logging.LoggingBuilderExtensions.CreateStartupLogger();
    logger.LogStarting(
        Assembly.GetExecutingAssembly().FullName ?? "Unknown",
        Environment.ProcessId,
        Environment.CurrentManagedThreadId);

    //
    // Set up Web application
    //
    var builder = WebApplication.CreateBuilder(args);

    // Configure application logging
    builder.Logging.AddApplicationLogging();

    // Get application options, which can be used during startup configuration
    ApplicationOptions applicationOptions = new();
    builder.Configuration.Bind(ApplicationOptions.Section, applicationOptions);

    // Add service defaults & Aspire client integrations.
    builder.AddServiceDefaults(logger);

    // Add version information to the configuration
    builder.AddApplicationOptions(logger);

    //
    // Add services to the container
    //

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddControllerServices();
    builder.Services.AddSwagger();
    builder.Services.AddApplicationFeatures();
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddIdentityConfiguration(builder.Configuration);
    builder.Services.AddTenancy();
    builder.Services.AddCorsServices(applicationOptions, logger);

    //
    // Build and configure the app
    //

    var app = builder.Build();

    // Prepare the database
    app.PrepareDatabaseAsync();

    // Configure the HTTP request pipeline
    app.ConfigureMiddlewarePipeline(app.Environment, logger);

    logger.LogOkEnvironment(app.Environment.EnvironmentName);

    app.Run();

    logger.LogApplicationStopped();

}
catch (Exception ex)
{
    if (logger is not null)
    {
        logger.LogStartupFailed(ex, Assembly.GetExecutingAssembly().FullName ?? "Unknown");
    }
    else
    {
        Console.WriteLine("CRITICAL: Failed to start application");
        Console.WriteLine(ex.ToString());
    }

    // Re-throw to ensure WebApplicationFactory tests can detect startup failures
    throw;
}

// Make Program class accessible to WebApplicationFactory for testing
public partial class Program { }
