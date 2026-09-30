using OpenIddict.Validation.AspNetCore;
using OpenIddict.Abstractions;

const string CorsPolicy = "AllowClient";
const string ApiScopePolicy = "ApiScope";
const string RequiredScope = "api";

var builder = WebApplication.CreateBuilder(args);
var oidc = builder.Configuration.GetSection("Introspection");

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? [])
        .AllowAnyMethod()
        .AllowAnyHeader()));

builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        // The introspection endpoint is discovered from the issuer's OpenID Connect metadata.
        options.SetIssuer(oidc["Issuer"]!);

        // Must equal the resource name of the scope in the security server's seed data.
        options.AddAudiences(oidc["ClientId"]!);

        // Tokens are validated by calling the introspection endpoint with this client's credentials.
        options.UseIntrospection()
            .SetClientId(oidc["ClientId"]!)
            .SetClientSecret(oidc["ClientSecret"]!);

        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization(options =>
    // OpenIddict keeps scopes in private "oi_scp" claims, so check them with HasScope() rather than RequireClaim("scope").
    options.AddPolicy(ApiScopePolicy, policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => context.User.HasScope(RequiredScope))));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/identity", (HttpContext context) =>
        context.User.Claims.Select(claim => new { claim.Type, claim.Value }))
    .RequireAuthorization(ApiScopePolicy)
    .RequireCors(CorsPolicy);

app.Run();
