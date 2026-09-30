using OpenIdDictTemplate.Security.Logic.Abstractions;

namespace OpenIdDictTemplate.Security.Logic.Users;

/// <summary>Creates a user without a password and emails an invitation. Returns the new user id.</summary>
public record CreateSecurityUser(string Email, string FirstName) : ICommand<string>;

public record ChangeSecurityUserName(string UserId, string UserName) : ICommand;

/// <summary>
/// Always succeeds, whatever the state of the account, so callers cannot probe for registered emails.
/// </summary>
public record RequestPasswordReset(string Email) : ICommand;

/// <summary>Always succeeds; resends the invitation/confirmation email when the user exists and is unconfirmed.</summary>
public record ResendEmailConfirmation(string UserId) : ICommand;

/// <summary>Deletes a freshly-invited account that was never confirmed and has no password.</summary>
public record CancelAccountVerification(string UserId, string Token) : ICommand;
