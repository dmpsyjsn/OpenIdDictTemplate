namespace OpenIdDictTemplate.Security.Logic.Users;

/// <summary>Builds absolute links into the account UI; implemented by the host so Logic never hardcodes routes.</summary>
public interface IAccountLinkBuilder
{
    string VerifyAccount(string userId, string token);

    string CancelAccountVerification(string userId, string token);

    string ResetPassword(string userId, string token);
}
