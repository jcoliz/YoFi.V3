using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using YoFi.V3.Entities.Providers;

namespace YoFi.V3.Data;

public static class ServiceCollectionExtensions
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

        return services;
    }

    public static void PrepareDatabaseAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<ApplicationDbContext>>();
        logger.LogInformation("[DIAG] PrepareDatabaseAsync called - checking if migration needed");

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Sqlite databases are always brought current by the application,
            // because there is only one client accesing the database at a time.

            var pendingMigrations = db.Database.GetPendingMigrations().ToList();
            if (pendingMigrations.Any())
            {
                logger.LogWarning("[DIAG] Applying {Count} pending migrations: {Migrations}",
                    pendingMigrations.Count, string.Join(", ", pendingMigrations));
            }
            else
            {
                logger.LogInformation("[DIAG] No pending migrations");
            }

            db.Database.Migrate();
            logger.LogInformation("[DIAG] Database migration completed");

            // Explicitly close the connection after migration
            db.Database.CloseConnection();
            logger.LogInformation("[DIAG] Database connection closed");
        }

        // Force garbage collection to ensure resources are released
        logger.LogInformation("[DIAG] Forcing GC to release database resources");
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
