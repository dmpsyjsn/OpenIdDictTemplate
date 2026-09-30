using System.Diagnostics;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using OpenIdDictTemplate.Security.Host.ViewModels;

namespace OpenIdDictTemplate.Security.Host.Controllers;

public class ErrorController : Controller
{
    [HttpGet("error"), HttpPost("error"), IgnoreAntiforgeryToken]
    public IActionResult Error()
    {
        // If the error was caused by an invalid OIDC request, show the OpenIddict error details.
        var response = HttpContext.GetOpenIddictServerResponse();
        var model = new ErrorViewModel
        {
            Error = response?.Error ?? string.Empty,
            ErrorDescription = response?.ErrorDescription ?? string.Empty,
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        };

        return View(model);
    }
}
