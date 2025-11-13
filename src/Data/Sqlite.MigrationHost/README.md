# Migrations Host for Data.Sqlite

In order to run migrations, EF Core needs a "main" app to run. To keep this simple,
we dedicate a whole app, with very minimal things going on, so we don't have to worry
about configuration, secrets, etc, which have nothing to do with the migration.

See: https://erwinstaal.nl/posts/db-per-tenant-catalog-database-ef-core-migrations/

## Make a migration

After making changes to the `ApplicationDbContext`, we need to add a migration
to describe how those changes will show up in the database. Migrations do need
to be added separately for Sql Server and Postgres.

From the root of the project, set `$env:MIGRATION` a name for this migration, and run:

```Powershell
dotnet build
dotnet ef migrations add $env:MIGRATION -o .\Migrations\ -n YoFi.V3.Data.Sqlite.Migrations --project .\src\Data\Sqlite\ --startup-project .\src\Data\Sqlite.MigrationsHost\ --context ApplicationDbContext
```

If you make a mistake and need to re-do it, be sure to remove the `ApplicationDbContextModelSnapshot.cs` file.

## Update database automatically

For Sqlite database, no further action is needed. Application Main() is expected
to call `Database.Migrate()` at launch, to automatically apply the latest
migrations.

## Generate a migrations SQL script to update database

Again, we don't normally need to migrate the database manually.
That said, if we want to see what the migrations script looks like, we can
create one:

```Powershell
PS ListsWebApp.V3> dotnet ef migrations script --project .\src\Data\Sqlite\ --startup-project .\src\Data\Sqlite.MigrationsHost\ --context ApplicationDbContext -i -o out\sqlite-migration.sql
```

## Never EnsureCreated

Note that databases created with "EnsureCreated" can never be migrated. That's definitely not OK
for production, so "EnsureCreated" is explicitly forbidden for code headed to production.
