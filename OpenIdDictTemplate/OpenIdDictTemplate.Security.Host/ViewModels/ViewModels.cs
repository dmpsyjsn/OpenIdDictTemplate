using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace OpenIdDictTemplate.Security.Host.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;

    [Display(Name = "Remember my login")]
    public bool RememberLogin { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordWithTokenViewModel
{
    [Required, HiddenInput]
    public string UserId { get; set; } = string.Empty;

    [Required, HiddenInput]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class EmailConfirmationNeededViewModel
{
    [Required, HiddenInput]
    public string UserId { get; set; } = string.Empty;
}

public class EmailConfirmationSuccessViewModel
{
    public string LoginUrl { get; set; } = string.Empty;
}

public class CancelAccountVerificationViewModel
{
    [Required, HiddenInput]
    public string UserId { get; set; } = string.Empty;

    [Required, HiddenInput]
    public string Token { get; set; } = string.Empty;
}

public class AuthorizeViewModel
{
    [Display(Name = "Application")]
    public string ApplicationName { get; set; } = string.Empty;

    [Display(Name = "Scope")]
    public string Scope { get; set; } = string.Empty;
}

public class ErrorViewModel
{
    public string Error { get; set; } = string.Empty;

    public string ErrorDescription { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;
}
