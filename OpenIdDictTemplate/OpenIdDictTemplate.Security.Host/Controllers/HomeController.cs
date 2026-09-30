using Microsoft.AspNetCore.Mvc;

namespace OpenIdDictTemplate.Security.Host.Controllers;

[Route("")]
public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
