using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using YoFi.V3.Entities.Providers;

namespace YoFi.V3.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=:memory:";

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IDataProvider, ApplicationDbContext>();

        return services;
    }
    
    public static void PrepareDatabaseAsync(this WebApplication app)
    {
        var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Sqlite databases are always brought current by the application,
        // because there is only one client accesing the database at a time.

        db.Database.Migrate();
    }
}