# Migrations Main for Data.Postgres

In order to run migrations, EF Core needs a "main" app to run. To keep this simple,
we dedicate a whole app, with very minimal things going on, so we don't have to worry
about configuration, secrets, etc, which have nothing to do with the migration.

See: https://erwinstaal.nl/posts/db-per-tenant-catalog-database-ef-core-migrations/

## Make a migration

After making changes to the `ApplicationDbContext`, we need to add a migration
to describe how those changes will show up in the database. Use the helper script
from the repository root:

```Powershell
.\scripts\Add-Migration.ps1 -Name "AddWeatherForecasts"
```

The repository pins `dotnet-ef` to the same version as its EF Core packages.
The helper scripts restore the local tool automatically. Before running EF commands
directly, run `dotnet tool restore` from the repository root.

If the migration has not been applied to any database, remove it with EF tooling so both
the migration files and model snapshot are updated together:

```Powershell
dotnet ef migrations remove --project .\src\Data\PostgreSQL\ --startup-project .\tools\Postgres.MigrationsMain\ --context ApplicationDbContext
```

Then correct the model and add the migration again. Do not delete only the model snapshot.
If it has been applied to a local development database and you intend to remove it, first
roll that database back to the previous migration. If it has been applied to a shared or
production database, do not remove or rewrite it; add a new migration to correct the schema.

## Update database automatically

For Postgres database, no further action is needed. Application Main() is expected
to call `Database.Migrate()` at launch, to automatically apply the latest
migrations.

This is allowed for non-production environments. Production database migrations
are applied by deployment pipeline scripts.

## Generate a migrations SQL script to update database

Again, we don't normally need to migrate the database manually.
That said, if we want to see what the migrations script looks like, we can
create one using the helper script from the repository root:

```Powershell
.\scripts\Export-PostgresMigration.ps1
```

This is the same command the CD pipeline runs to publish the migration as an
artifact (see `.azure/pipelines/steps/publish/publish-postgres-migration.yaml`).

## Never EnsureCreated

Note that databases created with "EnsureCreated" can never be migrated. That's definitely not OK
for production, so "EnsureCreated" is explicitly forbidden for code headed to production.

Calling "Migrate" is allowed for non-production Postgres environments.
