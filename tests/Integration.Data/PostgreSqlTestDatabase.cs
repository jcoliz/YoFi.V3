using Testcontainers.PostgreSql;

namespace YoFi.V3.Tests.Integration.Data;

[SetUpFixture]
public class PostgreSqlTestDatabase
{
    private static PostgreSqlContainer? _postgres;

    internal static string ConnectionString =>
        (_postgres ?? throw new InvalidOperationException("The PostgreSQL test container has not been initialized."))
        .GetConnectionString();

    [OneTimeSetUp]
    public async Task StartPostgreSqlAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();
        await _postgres.StartAsync();
        TestContext.Progress.WriteLine($"PostgreSQL test container: {_postgres.Name}");
    }

    [OneTimeTearDown]
    public async Task StopPostgreSqlAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
            _postgres = null;
        }
    }
}
