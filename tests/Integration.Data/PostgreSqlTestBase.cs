using Microsoft.EntityFrameworkCore;
using Npgsql;
using YoFi.V3.Data;

namespace YoFi.V3.Tests.Integration.Data;

public abstract class PostgreSqlTestBase
{
    protected ApplicationDbContext _context = null!;
    protected DbContextOptions<ApplicationDbContext> _options = null!;
    private string? _databaseName;

    [SetUp]
    public async Task SetUpDatabaseAsync()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(PostgreSqlTestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        _databaseName = databaseName;

        var connectionString = new NpgsqlConnectionStringBuilder(PostgreSqlTestDatabase.ConnectionString)
        {
            Database = databaseName,
            Pooling = false
        }.ConnectionString;
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        _context = new ApplicationDbContext(_options);

        try
        {
            await _context.Database.MigrateAsync();
        }
        catch
        {
            await TearDownDatabaseAsync();
            throw;
        }
    }

    [TearDown]
    public async Task TearDownDatabaseAsync()
    {
        if (_databaseName is null)
        {
            return;
        }

        await _context.DisposeAsync();
        await using var connection = new NpgsqlConnection(PostgreSqlTestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        _databaseName = null;
    }
}
