using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdDictTemplate.Security.Logic.Data;

/// <summary>
/// Idempotently upserts the configured scopes and clients. Nothing is ever deleted, and a client
/// secret is only rewritten when the configured value no longer matches the stored hash.
/// </summary>
public class OpenIddictSeeder(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    ILogger<OpenIddictSeeder> logger)
{
    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken)
    {
        foreach (var (name, seedScope) in options.Scopes)
            await UpsertScopeAsync(name, seedScope, cancellationToken);

        foreach (var (clientId, seedClient) in options.Clients)
            await UpsertClientAsync(clientId, seedClient, cancellationToken);
    }

    private async Task UpsertScopeAsync(string name, SeedScope seed, CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictScopeDescriptor { Name = name, DisplayName = seed.DisplayName ?? name };
        foreach (var resource in seed.Resources)
            descriptor.Resources.Add(resource);

        var existing = await scopeManager.FindByNameAsync(name, cancellationToken);
        if (existing is null)
        {
            await scopeManager.CreateAsync(descriptor, cancellationToken);
            logger.LogInformation("Seeded scope {Scope}", name);
        }
        else
        {
            var current = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(current, existing, cancellationToken);

            if (current.DisplayName != descriptor.DisplayName || !current.Resources.SetEquals(descriptor.Resources))
                await scopeManager.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    private async Task UpsertClientAsync(string clientId, SeedClient seed, CancellationToken cancellationToken)
    {
        var descriptor = BuildDescriptor(clientId, seed);

        var existing = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (existing is null)
        {
            await applicationManager.CreateAsync(descriptor, cancellationToken);
            logger.LogInformation("Seeded client {ClientId}", clientId);
            return;
        }

        var current = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(current, existing, cancellationToken);

        // The stored secret is a hash: keep it when the configured secret still matches, otherwise replace it.
        var secretChanged = !string.IsNullOrEmpty(seed.ClientSecret) &&
            !await applicationManager.ValidateClientSecretAsync(existing, seed.ClientSecret, cancellationToken);
        if (!secretChanged)
            descriptor.ClientSecret = current.ClientSecret;
        else
            logger.LogInformation("Updating the secret of client {ClientId}", clientId);

        if (secretChanged || !IsSame(current, descriptor))
        {
            await applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
            logger.LogInformation("Updated client {ClientId}", clientId);
        }
    }

    private static bool IsSame(OpenIddictApplicationDescriptor a, OpenIddictApplicationDescriptor b) =>
        a.DisplayName == b.DisplayName &&
        a.ClientType == b.ClientType &&
        a.ConsentType == b.ConsentType &&
        a.ClientSecret == b.ClientSecret &&
        a.RedirectUris.SetEquals(b.RedirectUris) &&
        a.PostLogoutRedirectUris.SetEquals(b.PostLogoutRedirectUris) &&
        a.Permissions.SetEquals(b.Permissions) &&
        a.Requirements.SetEquals(b.Requirements);

    private static OpenIddictApplicationDescriptor BuildDescriptor(string clientId, SeedClient seed)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = seed.DisplayName ?? clientId,
            ClientType = seed.ClientType.Equals("confidential", StringComparison.OrdinalIgnoreCase) ? ClientTypes.Confidential : ClientTypes.Public,
            ClientSecret = string.IsNullOrEmpty(seed.ClientSecret) ? null : seed.ClientSecret,
            ConsentType = seed.ConsentType.ToLowerInvariant()
        };

        if (descriptor.ClientType == ClientTypes.Confidential && descriptor.ClientSecret is null)
            throw new InvalidOperationException($"Seed client '{clientId}' is confidential and needs a ClientSecret (set Seed:Clients:{clientId}:ClientSecret, e.g. via user-secrets).");

        foreach (var uri in seed.RedirectUris)
            descriptor.RedirectUris.Add(new Uri(uri));
        foreach (var uri in seed.PostLogoutRedirectUris)
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));

        // Permissions are derived from the client definition.
        var usesAuthorizationCode = seed.GrantTypes.Contains(GrantTypes.AuthorizationCode);
        if (usesAuthorizationCode)
        {
            descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
        }

        if (seed.GrantTypes.Count > 0)
            descriptor.Permissions.Add(Permissions.Endpoints.Token);

        foreach (var grantType in seed.GrantTypes)
            descriptor.Permissions.Add(Permissions.Prefixes.GrantType + grantType);

        if (seed.PostLogoutRedirectUris.Count > 0)
            descriptor.Permissions.Add(Permissions.Endpoints.EndSession);

        if (seed.AllowIntrospection)
            descriptor.Permissions.Add(Permissions.Endpoints.Introspection);

        foreach (var scope in seed.Scopes)
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);

        if (usesAuthorizationCode && seed.RequirePkce)
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        return descriptor;
    }
}
