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
    logger.LogCheckpointReached("Adding Services");
    builder.Services.AddControllers();
    logger.LogCheckpointReached("Added Controllers");
    builder.Services.AddProblemDetails();
    logger.LogCheckpointReached("Added ProblemDetails");
    builder.Services.AddControllerServices();
    logger.LogCheckpointReached("Added Controller Services");
    builder.Services.AddSwagger();
    logger.LogCheckpointReached("Added Swagger");
    builder.Services.AddApplicationFeatures();
    logger.LogCheckpointReached("Added Application Features");
    builder.Services.AddDatabase(builder.Configuration);
    logger.LogCheckpointReached("Added Database");
    builder.Services.AddIdentityConfiguration(builder.Configuration);
    logger.LogCheckpointReached("Added Identity Configuration");
    builder.Services.AddTenancy();
    logger.LogCheckpointReached("Added Tenancy");
    builder.Services.AddCorsServices(applicationOptions, logger);
    logger.LogCheckpointReached("Added CORS Services");

    //
    // Build and configure the app
    //

    var app = builder.Build();
    logger.LogCheckpointReached("Built Application");

    // Prepare the database
    app.PrepareDatabaseAsync();
    logger.LogCheckpointReached("Prepared Database");

    // Configure the HTTP request pipeline
    app.ConfigureMiddlewarePipeline(app.Environment, logger);
    logger.LogCheckpointReached("Configured Middleware Pipeline");

    logger.LogOkEnvironment(app.Environment.EnvironmentName);

    app.Run();

    logger.LogApplicationStopped();

}
catch (Exception ex)
{
    if (logger is not null)
    {
        logger.LogStartupFailed(ex);
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
