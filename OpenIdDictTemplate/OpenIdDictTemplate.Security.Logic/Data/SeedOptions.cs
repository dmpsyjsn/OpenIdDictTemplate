namespace OpenIdDictTemplate.Security.Logic.Data;

/// <summary>
/// Config-driven seed data (section "Seed"). Users, scopes and clients are keyed dictionaries so that
/// individual values can be overridden from user-secrets, e.g. <c>Seed:Clients:resource_api:ClientSecret</c>.
/// </summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    public List<string> Roles { get; set; } = [];

    public Dictionary<string, SeedUser> Users { get; set; } = [];

    public Dictionary<string, SeedScope> Scopes { get; set; } = [];

    public Dictionary<string, SeedClient> Clients { get; set; } = [];
}

public class SeedUser
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}

public class SeedScope
{
    public string? DisplayName { get; set; }

    /// <summary>Audiences (resource server client ids) that tokens carrying this scope are issued for.</summary>
    public List<string> Resources { get; set; } = [];
}

public class SeedClient
{
    public string? DisplayName { get; set; }

    /// <summary>"public" or "confidential".</summary>
    public string ClientType { get; set; } = "public";

    public string? ClientSecret { get; set; }

    /// <summary>"implicit", "explicit", "external" or "systematic".</summary>
    public string ConsentType { get; set; } = "implicit";

    public bool RequirePkce { get; set; } = true;

    /// <summary>Allows the client (a resource server) to call the introspection endpoint.</summary>
    public bool AllowIntrospection { get; set; }

    /// <summary>Any of "authorization_code", "refresh_token", "client_credentials".</summary>
    public List<string> GrantTypes { get; set; } = [];

    public List<string> Scopes { get; set; } = [];

    public List<string> RedirectUris { get; set; } = [];

    public List<string> PostLogoutRedirectUris { get; set; } = [];
}
