using System.Net;
using System.Reflection;
using Microsoft.Extensions.Options;
using MimeKit;

namespace OpenIdDictTemplate.Security.Logic.Email;

public interface ISecurityEmailSender
{
    Task SendAccountCreatedAsync(string toEmail, string firstName, string verifyLink, string cancelLink, CancellationToken cancellationToken = default);

    Task SendResetPasswordAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default);
}

public class SecurityEmailSender(IEmailTransport transport, IOptions<EmailOptions> options) : ISecurityEmailSender
{
    public Task SendAccountCreatedAsync(string toEmail, string firstName, string verifyLink, string cancelLink, CancellationToken cancellationToken = default) =>
        SendAsync(toEmail, "Account Created", "AccountCreated.html", new Dictionary<string, string>
        {
            ["||Account.FirstName||"] = WebUtility.HtmlEncode(firstName),
            ["||Account.VerifyEmailLink||"] = WebUtility.HtmlEncode(verifyLink),
            ["||Account.CancelEmailLink||"] = WebUtility.HtmlEncode(cancelLink)
        }, cancellationToken);

    public Task SendResetPasswordAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default) =>
        SendAsync(toEmail, "Reset Password", "PasswordResetRequested.html", new Dictionary<string, string>
        {
            ["||Account.ResetPasswordLink||"] = WebUtility.HtmlEncode(resetLink)
        }, cancellationToken);

    private async Task SendAsync(string toEmail, string subject, string templateName, Dictionary<string, string> replacements, CancellationToken cancellationToken)
    {
        var emailOptions = options.Value;

        var html = await ReadTemplateAsync(templateName, cancellationToken);
        foreach (var (placeholder, value) in replacements)
            html = html.Replace(placeholder, value);
        html = html.Replace("||App.Name||", WebUtility.HtmlEncode(emailOptions.ProductName));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(emailOptions.FromName, emailOptions.FromAddress));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = $"{emailOptions.ProductName} - {subject}";
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        await transport.SendAsync(message, cancellationToken);
    }

    private static async Task<string> ReadTemplateAsync(string templateName, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Templates.{templateName}", StringComparison.Ordinal));

        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
