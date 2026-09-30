namespace OpenIdDictTemplate.Security.Host.Configuration;

public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Public base URL of this server; used as the token issuer and as the base of emailed links.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Where users land after confirming their account when no return URL is known.</summary>
    public string DefaultRedirectUrl { get; set; } = "/";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(4);

    public string[] CorsOrigins { get; set; } = [];

    /// <summary>Only for local HTTP development; never enable in production.</summary>
    public bool DisableTransportSecurityRequirement { get; set; }

    public CertificateOptions Certificates { get; set; } = new();
}

public class CertificateOptions
{
    /// <summary>Use OpenIddict's auto-generated development certificates instead of PFX files.</summary>
    public bool UseDevelopmentCertificates { get; set; }

    public CertificateFileOptions Encryption { get; set; } = new();

    public CertificateFileOptions Signing { get; set; } = new();
}

public class CertificateFileOptions
{
    public string Path { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
