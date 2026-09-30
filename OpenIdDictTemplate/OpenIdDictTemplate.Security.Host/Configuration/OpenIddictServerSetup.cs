using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIdDictTemplate.Security.Logic.Data;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdDictTemplate.Security.Host.Configuration;

public static class OpenIddictServerSetup
{
    public const string AdminPolicy = "SecurityAdmin";
    public const string AdminRole = "Admin";
    public const string AdminScope = "security_admin";
    public const string CorsPolicy = "AllowConfiguredOrigins";

    /// <summary>Registers Identity, the OpenIddict server/validation stack, the admin policy and CORS.</summary>
    public static IServiceCollection AddSecurityServer(this IServiceCollection services, IConfiguration configuration, SecurityOptions security)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options => options.LoginPath = "/account/login");

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>())
            .AddServer(server =>
            {
                server.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetIntrospectionEndpointUris("connect/introspect")
                    .SetEndSessionEndpointUris("connect/logout")
                    .SetUserInfoEndpointUris("connect/userinfo");

                server.SetIssuer(new Uri(security.Issuer));
                server.SetAccessTokenLifetime(security.AccessTokenLifetime);

                server.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow();

                // Scopes advertised in discovery; the seeded scopes (e.g. "api") are registered alongside.
                var seedScopes = configuration.GetSection($"{SeedOptions.SectionName}:Scopes").GetChildren().Select(c => c.Key);
                server.RegisterScopes([Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, .. seedScopes]);

                if (security.Certificates.UseDevelopmentCertificates)
                {
                    server.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    server.AddEncryptionCertificate(CertificateLoader.Load(security.Certificates.Encryption, nameof(CertificateOptions.Encryption)))
                        .AddSigningCertificate(CertificateLoader.Load(security.Certificates.Signing, nameof(CertificateOptions.Signing)));
                }

                var aspNetCore = server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();

                if (security.DisableTransportSecurityRequirement)
                    aspNetCore.DisableTransportSecurityRequirement();
            })
            .AddValidation(validation =>
            {
                // Used by the admin endpoints to validate tokens issued by this same server.
                validation.UseLocalServer();
                validation.UseAspNetCore();
            });

        services.AddAuthorization(options =>
            options.AddPolicy(AdminPolicy, policy => policy
                .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(AdminRole)
                .RequireAssertion(context => context.User.HasScope(AdminScope))));

        services.AddCors(options =>
            options.AddPolicy(CorsPolicy, policy => policy
                .WithOrigins(security.CorsOrigins.Select(o => o.TrimEnd('/')).ToArray())
                .AllowAnyMethod()
                .AllowAnyHeader()));

        return services;
    }
}
