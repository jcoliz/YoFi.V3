using System.Reflection;
using YoFi.V3.Application;
using YoFi.V3.BackEnd.Setup;
using YoFi.V3.BackEnd.Startup;
using YoFi.V3.Controllers.Extensions;
using YoFi.V3.Controllers.Tenancy;
using YoFi.V3.Data;
using NuxtIdentity.AspNetCore.Extensions;
using Microsoft.AspNetCore.Identity;
using static DotNetNuxt.Hosting.Logging.BootLoggerExtensions;
using static DotNetNuxt.Hosting.VersionHelpers;
using DotNetNuxt.Hosting.Logging;
using DotNetNuxt.Hosting.Options;
using DotNetNuxt.Hosting;

ILogger? logger = default;
try
{
    //
    // Set up Startup logger
    //

    logger = CreateStandardBootLogger();
    logger.LogStarting(
        Assembly.GetExecutingAssembly().FullName ?? "Unknown",
        Environment.ProcessId,
        Environment.CurrentManagedThreadId);

    //
    // Set up Web application
    //
    var builder = WebApplication.CreateBuilder(args);

    // Configure application logging
    builder.Logging.AddTerseConsoleLogFormatter(options => options.IncludeScopes = false);

    // Add service defaults & Aspire client integrations.
    builder.AddServiceDefaults(logger);

    // Retrieve app version from the assembly
    var version = GetVersion(typeof(Program).Assembly);
    logger.LogVersion(version);

    // Add the usual Startup options
    StartupOptions startupOptions = builder.AddStandardStartupOptions();

    //
    // Add services to the container
    //
    logger.LogCheckpointReached("Adding Services");

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddControllerServices(version);
    builder.Services.AddSwagger();
    builder.Services.AddApplicationFeatures();
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddIdentityConfiguration(builder.Configuration);
    builder.Services.AddTenancy();
    builder.Services.AddStandardCorsPolicy(startupOptions.AllowedCorsOrigins);

    logger.LogCheckpointReached("Added All Services");

    //
    // Build and configure the app
    //

    var app = builder.Build();
    logger.LogCheckpointReached("Built Application");

    // Prepare the database
    app.PrepareDatabaseAsync();
    logger.LogCheckpointReached("Prepared Database");

    await app.SeedNuxtIdentityAsync<IdentityUser>();
    logger.LogCheckpointReached("Completed NuxtIdentity Seeding");

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
