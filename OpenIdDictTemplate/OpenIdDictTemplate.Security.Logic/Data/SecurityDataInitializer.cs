using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OpenIdDictTemplate.Security.Logic.Data;

/// <summary>Applies pending migrations, then seeds roles, users, scopes and clients. Runs before the web server starts.</summary>
public class SecurityDataInitializer(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        if (services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
            await services.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync(cancellationToken);

        var seed = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        await services.GetRequiredService<IdentitySeeder>().SeedAsync(seed);
        await services.GetRequiredService<OpenIddictSeeder>().SeedAsync(seed, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
