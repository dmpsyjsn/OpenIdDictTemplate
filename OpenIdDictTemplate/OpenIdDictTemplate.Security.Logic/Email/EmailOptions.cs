namespace OpenIdDictTemplate.Security.Logic.Email;

public enum EmailType
{
    PickupDirectory,
    Smtp
}

public class EmailOptions
{
    public const string SectionName = "Email";

    public EmailType Type { get; set; } = EmailType.PickupDirectory;

    public string FromAddress { get; set; } = "no-reply@example.com";

    public string FromName { get; set; } = string.Empty;

    /// <summary>Substituted for the ||App.Name|| placeholder in email templates and subjects.</summary>
    public string ProductName { get; set; } = "OpenIdDictTemplate";

    /// <summary>Folder for .eml files when <see cref="Type"/> is PickupDirectory. Relative paths resolve against the content root.</summary>
    public string PickupDirectory { get; set; } = "App_Data/Emails";

    public SmtpOptions Smtp { get; set; } = new();
}

public class SmtpOptions
{
    public string Server { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
