using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YoFi.V3.Data;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TenantSeedStageTests
{
    [Test]
    public async Task Seed_NewTenant_PersistsCreationTimestampFromTimeProvider()
    {
        var key = Guid.NewGuid();
        var clock = new FixedTimeProvider();
        using var factory = new BaseTestWebApplicationFactory(new Dictionary<string, string?>
        {
            ["IdentitySeeder:ClockTest:Order"] = "100",
            ["IdentitySeeder:ClockTest:Tenants:0:Key"] = key.ToString(),
            ["IdentitySeeder:ClockTest:Tenants:0:Name"] = "Clock test tenant"
        });
        using var configuredFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = configuredFactory.CreateClient();
        using var scope = configuredFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = await context.Tenants.SingleAsync(t => t.Key == key);

        Assert.That(tenant.CreatedAt, Is.EqualTo(clock.GetUtcNow()));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2040, 7, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
