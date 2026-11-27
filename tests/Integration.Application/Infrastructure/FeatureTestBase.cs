using DotNetNuxt.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using YoFi.V3.Data;
using YoFi.V3.Tests.Integration;

namespace YoFi.V3.Tests.Integration.Application.TestHelpers;

/// <summary>
/// Base class for Integration.Application tests.
/// </summary>
/// <remarks>
/// Provides one migrated PostgreSQL database per derived fixture. Tables are
/// truncated and identities reset between tests, while the database and migrations
/// are created only once for the fixture lifetime.
/// </remarks>
public abstract class FeatureTestBase
{
    protected ApplicationDbContext _context = null!;
    protected IDataProvider _dataProvider = null!;

    protected DbContextOptions<ApplicationDbContext> _options = null!;
    private string? _databaseName;

    [OneTimeSetUp]
    public async Task SetUpFixtureDatabaseAsync()
    {
        _databaseName = $"test_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(PostgreSqlTestDatabase.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(PostgreSqlTestDatabase.ConnectionString)
        {
            Database = _databaseName,
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
            await TearDownFixtureDatabaseAsync();
            throw;
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        await _context.DisposeAsync();
        _context = new ApplicationDbContext(_options);

        await _context.Database.ExecuteSqlRawAsync("""
            DO $reset$
            DECLARE reset_record record;
                table_list text;
            BEGIN
                FOR reset_record IN
                    SELECT n.nspname AS schema_name,
                           c.relname AS table_name,
                           t.tgname AS trigger_name
                    FROM pg_trigger t
                    JOIN pg_class c ON c.oid = t.tgrelid
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE NOT t.tgisinternal
                      AND n.nspname = 'public'
                LOOP
                    EXECUTE format('DROP TRIGGER IF EXISTS %I ON %I.%I',
                        reset_record.trigger_name,
                        reset_record.schema_name,
                        reset_record.table_name);
                END LOOP;

                FOR reset_record IN
                    SELECT n.nspname AS schema_name,
                           p.proname AS function_name,
                           pg_get_function_identity_arguments(p.oid) AS arguments
                    FROM pg_proc p
                    JOIN pg_namespace n ON n.oid = p.pronamespace
                    WHERE n.nspname = 'public'
                LOOP
                    EXECUTE format('DROP FUNCTION IF EXISTS %I.%I(%s)',
                        reset_record.schema_name,
                        reset_record.function_name,
                        reset_record.arguments);
                END LOOP;

                SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
                INTO table_list
                FROM pg_tables
                WHERE schemaname = 'public'
                  AND tablename <> '__EFMigrationsHistory';

                IF table_list IS NOT NULL THEN
                    EXECUTE 'TRUNCATE TABLE ' || table_list || ' RESTART IDENTITY CASCADE';
                END IF;
            END
            $reset$;
            """);

        _dataProvider = _context;
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
    }

    [OneTimeTearDown]
    public async Task TearDownFixtureDatabaseAsync()
    {
        if (_databaseName is null)
        {
            return;
        }

        if (_context is not null)
        {
            await _context.DisposeAsync();
        }

        await using var connection = new NpgsqlConnection(PostgreSqlTestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        _databaseName = null;
    }
}
