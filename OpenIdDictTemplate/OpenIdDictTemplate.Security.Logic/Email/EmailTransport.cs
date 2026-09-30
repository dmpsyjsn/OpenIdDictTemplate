using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;

namespace OpenIdDictTemplate.Security.Logic.Email;

public interface IEmailTransport
{
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default);
}

public class SmtpEmailTransport(IOptions<EmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        var smtp = options.Value.Smtp;

        // Note: this is MailKit's SmtpClient, not the framework one.
        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Server, smtp.Port, SecureSocketOptions.StartTls, cancellationToken);

        // OAuth isn't used, so don't try it.
        client.AuthenticationMechanisms.Remove("XOAUTH2");

        if (!string.IsNullOrEmpty(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

/// <summary>Writes each message as an .eml file; intended for development.</summary>
public class PickupDirectoryEmailTransport(IOptions<EmailOptions> options, IHostEnvironment environment) : IEmailTransport
{
    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        var folder = options.Value.PickupDirectory;
        if (!Path.IsPathRooted(folder))
            folder = Path.Combine(environment.ContentRootPath, folder);

        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.eml");
        await using var stream = new FileStream(path, FileMode.CreateNew);
        await message.WriteToAsync(stream, cancellationToken);
    }
}
