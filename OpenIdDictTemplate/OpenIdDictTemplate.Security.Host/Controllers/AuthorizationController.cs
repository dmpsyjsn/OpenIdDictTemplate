using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using OpenIdDictTemplate.Security.Host.Infrastructure;
using OpenIdDictTemplate.Security.Host.ViewModels;
using OpenIdDictTemplate.Security.Logic.Data;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdDictTemplate.Security.Host.Controllers;

public class AuthorizationController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictScopeManager scopeManager,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Redirect to the login page when there is no (fresh enough) session, or prompt=login was requested.
        var result = await HttpContext.AuthenticateAsync();
        if (result is not { Succeeded: true } || request.HasPromptValue(PromptValues.Login) ||
            (request.MaxAge != null && result.Properties?.IssuedUtc != null &&
             DateTimeOffset.UtcNow - result.Properties.IssuedUtc > TimeSpan.FromSeconds(request.MaxAge.Value)))
        {
            // With prompt=none the client asked to never show any UI.
            if (request.HasPromptValue(PromptValues.None))
                return ForbidWith(Errors.LoginRequired, "The user is not logged in.");

            // Remove prompt=login so the user isn't sent around the login -> authorize loop forever.
            var prompt = string.Join(" ", request.GetPromptValues().Remove(PromptValues.Login));

            var parameters = Request.HasFormContentType
                ? Request.Form.Where(parameter => parameter.Key != Parameters.Prompt).ToList()
                : Request.Query.Where(parameter => parameter.Key != Parameters.Prompt).ToList();

            parameters.Add(KeyValuePair.Create(Parameters.Prompt, new StringValues(prompt)));

            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parameters)
            });
        }

        var user = await userManager.GetUserAsync(result.Principal) ??
            throw new InvalidOperationException("The user details cannot be retrieved.");

        var application = await applicationManager.FindByClientIdAsync(request.ClientId!) ??
            throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

        var authorizations = await authorizationManager.FindAsync(
            subject: await userManager.GetUserIdAsync(user),
            client: (await applicationManager.GetIdAsync(application))!,
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()).ToListAsync();

        switch (await applicationManager.GetConsentTypeAsync(application))
        {
            // External consent (e.g. granted by an admin): fail unless an authorization already exists.
            case ConsentTypes.External when authorizations.Count is 0:
                return ForbidWith(Errors.ConsentRequired, "The logged in user is not allowed to access this client application.");

            // Implicit consent, or an existing authorization: issue the code without showing the consent form.
            case ConsentTypes.Implicit:
            case ConsentTypes.External when authorizations.Count is not 0:
            case ConsentTypes.Explicit when authorizations.Count is not 0 && !request.HasPromptValue(PromptValues.Consent):
                return await IssueAuthorizationAsync(user, application, request, authorizations.LastOrDefault());

            // The client asked for no UI but consent is needed.
            case ConsentTypes.Explicit when request.HasPromptValue(PromptValues.None):
            case ConsentTypes.Systematic when request.HasPromptValue(PromptValues.None):
                return ForbidWith(Errors.ConsentRequired, "Interactive user consent is required.");

            default:
                return View(new AuthorizeViewModel
                {
                    ApplicationName = await applicationManager.GetLocalizedDisplayNameAsync(application) ?? request.ClientId!,
                    Scope = request.Scope ?? string.Empty
                });
        }
    }

    [Authorize]
    [HttpPost("~/connect/authorize"), FormValueRequired("submit.Accept"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var user = await userManager.GetUserAsync(User) ??
            throw new InvalidOperationException("The user details cannot be retrieved.");

        var application = await applicationManager.FindByClientIdAsync(request.ClientId!) ??
            throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

        var authorizations = await authorizationManager.FindAsync(
            subject: await userManager.GetUserIdAsync(user),
            client: (await applicationManager.GetIdAsync(application))!,
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()).ToListAsync();

        // External consent can't be granted by the user themselves.
        if (authorizations.Count is 0 && await applicationManager.HasConsentTypeAsync(application, ConsentTypes.External))
            return ForbidWith(Errors.ConsentRequired, "The logged in user is not allowed to access this client application.");

        return await IssueAuthorizationAsync(user, application, request, authorizations.LastOrDefault());
    }

    [Authorize]
    [HttpPost("~/connect/authorize"), FormValueRequired("submit.Deny"), ValidateAntiForgeryToken]
    public IActionResult Deny() => Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    [HttpPost("~/connect/token"), IgnoreAntiforgeryToken, Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            throw new InvalidOperationException("The specified grant type is not supported.");

        // Retrieve the claims principal stored in the authorization code/refresh token.
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var user = await userManager.FindByIdAsync(result.Principal!.GetClaim(Claims.Subject)!);
        if (user is null)
            return ForbidWith(Errors.InvalidGrant, "The token is no longer valid.");

        // Ensure the user is still allowed to sign in.
        if (!await signInManager.CanSignInAsync(user))
            return ForbidWith(Errors.InvalidGrant, "The user is no longer allowed to sign in.");

        var identity = new ClaimsIdentity(result.Principal!.Claims,
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        // Refresh the user claims in case they changed since the code/refresh token was issued.
        await SetUserClaimsAsync(identity, user);
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/logout")]
    public IActionResult Logout() => View();

    [ActionName(nameof(Logout)), HttpPost("~/connect/logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        // Delete the local Identity cookie.
        await signInManager.SignOutAsync();

        // Ask OpenIddict to redirect to the post_logout_redirect_uri, or to "/" if none was given.
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    private async Task<IActionResult> IssueAuthorizationAsync(ApplicationUser user, object application, OpenIddictRequest request, object? authorization)
    {
        var identity = await CreateIdentityAsync(user, request);

        // Automatically create a permanent authorization to avoid asking for consent again for the same scopes.
        authorization ??= await authorizationManager.CreateAsync(
            identity: identity,
            subject: await userManager.GetUserIdAsync(user),
            client: (await applicationManager.GetIdAsync(application))!,
            type: AuthorizationTypes.Permanent,
            scopes: identity.GetScopes());

        identity.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization));
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsIdentity> CreateIdentityAsync(ApplicationUser user, OpenIddictRequest request)
    {
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        await SetUserClaimsAsync(identity, user);

        // The granted scopes match the requested ones; restrict the list here to let users uncheck scopes.
        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

        return identity;
    }

    private async Task SetUserClaimsAsync(ClaimsIdentity identity, ApplicationUser user)
    {
        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
            .SetClaim(Claims.Email, await userManager.GetEmailAsync(user))
            .SetClaim(Claims.Name, await userManager.GetUserNameAsync(user))
            .SetClaim(Claims.PreferredUsername, await userManager.GetUserNameAsync(user))
            .SetClaims(Claims.Role, [.. await userManager.GetRolesAsync(user)]);
    }

    private ForbidResult ForbidWith(string error, string description) => Forbid(
        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        properties: new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        }));

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Claims are NOT automatically included in tokens: each needs a destination.
        switch (claim.Type)
        {
            case Claims.Name or Claims.PreferredUsername:
                yield return Destinations.AccessToken;

                if (claim.Subject!.HasScope(Scopes.Profile))
                    yield return Destinations.IdentityToken;

                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;

                if (claim.Subject!.HasScope(Scopes.Email))
                    yield return Destinations.IdentityToken;

                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;

                if (claim.Subject!.HasScope(Scopes.Roles))
                    yield return Destinations.IdentityToken;

                yield break;

            // Never include the security stamp in tokens, as it is a secret value.
            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
