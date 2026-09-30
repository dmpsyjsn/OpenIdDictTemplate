using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenIdDictTemplate.Security.Host.Configuration;
using OpenIdDictTemplate.Security.Host.ViewModels;
using OpenIdDictTemplate.Security.Host.Infrastructure;
using OpenIdDictTemplate.Security.Logic.Abstractions;
using OpenIdDictTemplate.Security.Logic.Data;
using OpenIdDictTemplate.Security.Logic.Users;
using UsersLogic = OpenIdDictTemplate.Security.Logic.Users;

namespace OpenIdDictTemplate.Security.Host.Controllers;

[Route("[controller]")]
[AllowAnonymous]
public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOptions<SecurityOptions> securityOptions,
    IHandleCommandAsync<RequestPasswordReset> requestPasswordReset,
    IHandleCommandAsync<ResendEmailConfirmation> resendEmailConfirmation,
    IHandleCommandAsync<UsersLogic.CancelAccountVerification> cancelAccountVerification) : Controller
{
    private string DefaultRedirectUrl => securityOptions.Value.DefaultRedirectUrl;

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl ?? DefaultRedirectUrl });

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null)
        {
            // Not PasswordSignInAsync: with RequireConfirmedEmail it would reject unconfirmed users
            // before the password is checked, and we want to tell only genuine owners about that.
            if (await userManager.IsLockedOutAsync(user))
                return LockedOut(model);

            if (await userManager.CheckPasswordAsync(user, model.Password))
            {
                await userManager.ResetAccessFailedCountAsync(user);

                if (!user.EmailConfirmed)
                    return RedirectToAction(nameof(EmailConfirmationNeeded), new { userId = user.Id });

                await signInManager.SignInAsync(user, model.RememberLogin);

                // Only local return URLs are honoured; anything else goes to the home page.
                return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl) : LocalRedirect("~/");
            }

            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
                return LockedOut(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet("verifyAccount")]
    public async Task<IActionResult> VerifyAccount(string userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            ViewBag.VerificationMessage = "User account not found.";
            return View();
        }

        if (user.EmailConfirmed)
            return RedirectToAction(nameof(Login));

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            if (result.Errors.Count() == 1 && result.Errors.First().Code == "InvalidToken")
            {
                ViewBag.VerificationMessage = "InvalidToken";
                ViewBag.EmailConfirmationLink = Url.Action(nameof(EmailConfirmationNeeded), "Account", new { userId });
                return View();
            }

            ViewBag.VerificationMessage = $"Email verification failed with errors: {string.Join(", ", result.Errors.Select(x => x.Description))}";
            return View();
        }

        // Invited users have no password yet, so let them choose one now.
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            return RedirectToAction(nameof(ResetPasswordWithToken), new { userId, token = resetToken });
        }

        return Redirect(DefaultRedirectUrl);
    }

    [HttpGet("emailConfirmationNeeded")]
    public IActionResult EmailConfirmationNeeded(string userId) =>
        View(new EmailConfirmationNeededViewModel { UserId = userId });

    [HttpPost("emailConfirmationNeeded")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailConfirmationNeeded(EmailConfirmationNeededViewModel model)
    {
        await resendEmailConfirmation.HandleAsync(new ResendEmailConfirmation(model.UserId), HttpContext.RequestAborted);
        return View("EmailConfirmationSuccess", new EmailConfirmationSuccessViewModel { LoginUrl = Url.Action(nameof(Login))! });
    }

    [HttpGet("forgotPassword")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("forgotPassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // The same page is shown whether or not the account exists.
        await requestPasswordReset.HandleAsync(new RequestPasswordReset(model.Email), HttpContext.RequestAborted);
        return View("ForgotPasswordConfirmation");
    }

    [HttpGet("resetPasswordWithToken")]
    public async Task<IActionResult> ResetPasswordWithToken(string userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Redirect("~/");

        return View(new ResetPasswordWithTokenViewModel { UserId = userId, Token = token });
    }

    [HttpPost("resetPasswordWithToken")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPasswordWithToken(ResetPasswordWithTokenViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByIdAsync(model.UserId);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "The password reset link is invalid.");
            return View(model);
        }

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        if (!user.EmailConfirmed)
            return RedirectToAction(nameof(EmailConfirmationNeeded), new { userId = user.Id });

        await signInManager.SignInAsync(user, isPersistent: false);
        return Redirect(DefaultRedirectUrl);
    }

    [HttpGet("cancelAccountVerification")]
    public IActionResult CancelAccountVerification(string userId, string token) =>
        View(new CancelAccountVerificationViewModel { UserId = userId, Token = token });

    // POST-only so that email link scanners following the emailed link can't delete the account.
    [HttpPost("cancelAccountVerification")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAccountVerification(CancelAccountVerificationViewModel model)
    {
        var result = await cancelAccountVerification.HandleAsync(
            new UsersLogic.CancelAccountVerification(model.UserId, model.Token), HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            ViewBag.VerificationMessage = result.Error!.Message;
            return View("VerifyAccount");
        }

        return View("CancelAccountVerificationSuccess");
    }

    private IActionResult LockedOut(LoginViewModel model)
    {
        ModelState.AddModelError(string.Empty, "Your account is temporarily locked. Please try again later.");
        return View(model);
    }
}
