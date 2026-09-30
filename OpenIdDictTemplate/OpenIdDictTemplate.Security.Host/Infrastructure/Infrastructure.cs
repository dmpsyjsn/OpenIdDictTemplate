using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.Extensions.Options;
using OpenIdDictTemplate.Security.Host.Configuration;
using OpenIdDictTemplate.Security.Logic.Abstractions;
using OpenIdDictTemplate.Security.Logic.Users;

namespace OpenIdDictTemplate.Security.Host.Infrastructure;

/// <summary>Selects an action only when the request form contains a non-empty value for the given name.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class FormValueRequiredAttribute(string name) : ActionMethodSelectorAttribute
{
    public override bool IsValidForRequest(Microsoft.AspNetCore.Routing.RouteContext context, ActionDescriptor action)
    {
        var request = context.HttpContext.Request;
        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(request.ContentType))
            return false;

        if (!request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            return false;

        return !string.IsNullOrEmpty(request.Form[name]);
    }
}

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result, Func<IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess?.Invoke() ?? new OkResult();

        var error = result.Error!;
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        return new ObjectResult(new ProblemDetails { Status = status, Title = error.Type.ToString(), Detail = error.Message })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}

public class AccountLinkBuilder(LinkGenerator linkGenerator, IOptions<SecurityOptions> options) : IAccountLinkBuilder
{
    private readonly string _baseUrl = options.Value.Issuer.TrimEnd('/');

    public string VerifyAccount(string userId, string token) => Build("VerifyAccount", userId, token);

    public string CancelAccountVerification(string userId, string token) => Build("CancelAccountVerification", userId, token);

    public string ResetPassword(string userId, string token) => Build("ResetPasswordWithToken", userId, token);

    private string Build(string action, string userId, string token)
    {
        var path = linkGenerator.GetPathByAction(action, "Account", new { userId, token })
            ?? throw new InvalidOperationException($"No route found for Account/{action}.");
        return _baseUrl + path;
    }
}
