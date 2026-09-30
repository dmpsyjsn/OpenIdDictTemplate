using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIdDictTemplate.Security.Logic.Abstractions;
using OpenIdDictTemplate.Security.Logic.Data;
using OpenIdDictTemplate.Security.Logic.Email;

namespace OpenIdDictTemplate.Security.Logic.Users;

public class UserCommandHandlers(
    UserManager<ApplicationUser> userManager,
    ISecurityEmailSender emailSender,
    IAccountLinkBuilder linkBuilder,
    ILogger<UserCommandHandlers> logger) :
    IHandleCommandAsync<CreateSecurityUser, string>,
    IHandleCommandAsync<ChangeSecurityUserName>,
    IHandleCommandAsync<RequestPasswordReset>,
    IHandleCommandAsync<ResendEmailConfirmation>,
    IHandleCommandAsync<CancelAccountVerification>
{
    public async Task<Result<string>> HandleAsync(CreateSecurityUser command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            return Result.Fail<string>(ErrorType.Validation, "Email is required.");

        if (await userManager.FindByEmailAsync(command.Email) is not null)
            return Result.Fail<string>(ErrorType.Conflict, "A user with this email already exists.");

        var user = new ApplicationUser { UserName = command.Email, Email = command.Email };
        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded)
            return Result.Fail<string>(ErrorType.Validation, Describe(created));

        try
        {
            await SendInvitationAsync(user, command.FirstName, cancellationToken);
        }
        catch (Exception exception)
        {
            // Don't leave an account behind that nobody has been told about.
            logger.LogError(exception, "Failed to send the invitation email to {Email}; removing the new user", command.Email);
            await userManager.DeleteAsync(user);
            return Result.Fail<string>(ErrorType.Unexpected, "The invitation email could not be sent.");
        }

        return Result.Ok(user.Id);
    }

    public async Task<Result> HandleAsync(ChangeSecurityUserName command, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(command.UserId);
        if (user is null)
            return Result.Fail(ErrorType.NotFound, "User not found.");

        user.Email = command.UserName;
        user.UserName = command.UserName;

        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            var conflict = updated.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName));
            return Result.Fail(conflict ? ErrorType.Conflict : ErrorType.Validation, Describe(updated));
        }

        return Result.Ok();
    }

    public async Task<Result> HandleAsync(RequestPasswordReset command, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = string.IsNullOrWhiteSpace(command.Email) ? null : await userManager.FindByEmailAsync(command.Email);
            if (user?.Email is null)
                return Result.Ok();

            if (user.EmailConfirmed)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                await emailSender.SendResetPasswordAsync(user.Email, linkBuilder.ResetPassword(user.Id, token), cancellationToken);
            }
            else
            {
                await SendInvitationAsync(user, user.Email, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Swallowed on purpose: the caller must not learn anything about the account.
            logger.LogError(exception, "Failed to process a password reset request");
        }

        return Result.Ok();
    }

    public async Task<Result> HandleAsync(ResendEmailConfirmation command, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await userManager.FindByIdAsync(command.UserId);
            if (user?.Email is not null && !user.EmailConfirmed)
                await SendInvitationAsync(user, user.Email, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to resend the confirmation email");
        }

        return Result.Ok();
    }

    public async Task<Result> HandleAsync(CancelAccountVerification command, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(command.UserId);
        if (user is null)
            return Result.Fail(ErrorType.NotFound, "User account not found.");

        var validToken = await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.EmailConfirmationTokenProvider,
            UserManager<ApplicationUser>.ConfirmEmailTokenPurpose,
            command.Token);
        if (!validToken)
            return Result.Fail(ErrorType.Forbidden, "The cancellation link is invalid or has expired.");

        if (user.EmailConfirmed || !string.IsNullOrEmpty(user.PasswordHash))
            return Result.Fail(ErrorType.Forbidden, "This account is already active and cannot be cancelled.");

        var deleted = await userManager.DeleteAsync(user);
        return deleted.Succeeded ? Result.Ok() : Result.Fail(ErrorType.Unexpected, Describe(deleted));
    }

    private async Task SendInvitationAsync(ApplicationUser user, string firstName, CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendAccountCreatedAsync(
            user.Email!,
            firstName,
            linkBuilder.VerifyAccount(user.Id, token),
            linkBuilder.CancelAccountVerification(user.Id, token),
            cancellationToken);
    }

    private static string Describe(IdentityResult result) => string.Join(", ", result.Errors.Select(e => e.Description));
}
