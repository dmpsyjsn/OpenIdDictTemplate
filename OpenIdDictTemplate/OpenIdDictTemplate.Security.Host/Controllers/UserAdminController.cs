using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIdDictTemplate.Security.Host.Configuration;
using OpenIdDictTemplate.Security.Host.Infrastructure;
using OpenIdDictTemplate.Security.Logic.Abstractions;
using OpenIdDictTemplate.Security.Logic.Users;

namespace OpenIdDictTemplate.Security.Host.Controllers;

[ApiController]
[Authorize(Policy = OpenIddictServerSetup.AdminPolicy)]
public class UserAdminController(
    IHandleCommandAsync<CreateSecurityUser, string> createUserHandler,
    IHandleCommandAsync<ChangeSecurityUserName> changeUserNameHandler) : ControllerBase
{
    [HttpPost("admin/users/create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateSecurityUser command)
    {
        var result = await createUserHandler.HandleAsync(command, HttpContext.RequestAborted);
        return result.ToActionResult(() => Ok(new { UserId = result.Value }));
    }

    [HttpPost("admin/users/update-email")]
    public async Task<IActionResult> UpdateUserEmail([FromBody] ChangeSecurityUserName command)
    {
        var result = await changeUserNameHandler.HandleAsync(command, HttpContext.RequestAborted);
        return result.ToActionResult(() => Ok(new { Message = "Email updated successfully." }));
    }
}
