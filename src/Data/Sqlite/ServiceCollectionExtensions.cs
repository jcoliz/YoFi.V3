using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using NuxtIdentity.Core.Abstractions;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Data;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=app.db";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                // Set command timeout to prevent long-running operations
                sqliteOptions.CommandTimeout(30);
            });

            // Disable connection pooling to prevent Swagger UI issues
            options.EnableSensitiveDataLogging(false);
        });

        services.AddScoped<IDataProvider, ApplicationDbContext>();
        services.AddScoped<ITenantRepository, ApplicationDbContext>();

        return services;
    }

    public static void PrepareDatabaseAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<ApplicationDbContext>>();
        LogPrepareDatabaseCalled(logger);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Sqlite databases are always brought current by the application,
            // because there is only one client accesing the database at a time.

            var pendingMigrations = db.Database.GetPendingMigrations().ToList();
            if (pendingMigrations.Any())
            {
                LogApplyingPendingMigrations(logger, pendingMigrations.Count, string.Join(", ", pendingMigrations));
            }
            else
            {
                LogNoPendingMigrations(logger);
            }

            db.Database.Migrate();
            LogDatabaseMigrationCompleted(logger);

            // Explicitly close the connection after migration
            db.Database.CloseConnection();
            LogDatabaseConnectionClosed(logger);
        }

// Enable this for debugging resource release issues
#if false
        // Force garbage collection to ensure resources are released
        LogForcingGC(logger);
        GC.Collect();
        GC.WaitForPendingFinalizers();
#endif
    }

    [LoggerMessage(1, LogLevel.Debug, "PrepareDatabaseAsync called - checking if migration needed")]
    private static partial void LogPrepareDatabaseCalled(ILogger logger);

    [LoggerMessage(2, LogLevel.Debug, "Applying {Count} pending migrations: {Migrations}")]
    private static partial void LogApplyingPendingMigrations(ILogger logger, int count, string migrations);

    [LoggerMessage(3, LogLevel.Debug, "No pending migrations")]
    private static partial void LogNoPendingMigrations(ILogger logger);

    [LoggerMessage(4, LogLevel.Debug, "Database migration completed")]
    private static partial void LogDatabaseMigrationCompleted(ILogger logger);

    [LoggerMessage(5, LogLevel.Debug, "Database connection closed")]
    private static partial void LogDatabaseConnectionClosed(ILogger logger);

    [LoggerMessage(6, LogLevel.Debug, "Forcing GC to release database resources")]
    private static partial void LogForcingGC(ILogger logger);

}
