using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using YoFi.V3.Data;

namespace YoFi.V3.Data.MigrationsHost;

public class CatalogContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite(string.Empty);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
