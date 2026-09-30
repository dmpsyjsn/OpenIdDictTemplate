using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenIdDictTemplate.Security.Logic.Data;

public enum DatabaseProvider
{
    Sqlite,
    SqlServer,
    Postgres
}

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>Apply pending EF migrations (and seed) when the host starts.</summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}

public static class SecurityPersistence
{
    public const string SqlServerMigrationsAssembly = "OpenIdDictTemplate.Security.Migrations.SqlServer";
    public const string SqliteMigrationsAssembly = "OpenIdDictTemplate.Security.Migrations.Sqlite";
    public const string PostgresMigrationsAssembly = "OpenIdDictTemplate.Security.Migrations.Postgres";

    /// <summary>
    /// Registers <see cref="ApplicationDbContext"/> for the configured provider. Migrations live in a
    /// separate assembly per provider so the context itself stays provider-agnostic.
    /// </summary>
    public static IServiceCollection AddSecurityPersistence(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            switch (databaseOptions.Provider)
            {
                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(
                        configuration.GetConnectionString("SqlServer"),
                        sql => sql.MigrationsAssembly(SqlServerMigrationsAssembly));
                    break;

                case DatabaseProvider.Postgres:
                    options.UseNpgsql(
                        configuration.GetConnectionString("Postgres"),
                        npgsql => npgsql.MigrationsAssembly(PostgresMigrationsAssembly));
                    break;

                case DatabaseProvider.Sqlite:
                    var connectionString = ResolveSqliteConnectionString(configuration.GetConnectionString("Sqlite"), contentRootPath);
                    options.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(SqliteMigrationsAssembly));
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported database provider '{databaseOptions.Provider}'.");
            }

            // Register the entity sets needed by OpenIddict.
            options.UseOpenIddict();
        });

        return services;
    }

    private static string ResolveSqliteConnectionString(string? connectionString, string contentRootPath)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString ?? "Data Source=App_Data/security.db");
        if (!Path.IsPathRooted(builder.DataSource))
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));

        Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);
        return builder.ToString();
    }
}
