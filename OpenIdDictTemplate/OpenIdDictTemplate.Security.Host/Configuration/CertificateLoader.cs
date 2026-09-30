using System.Security.Cryptography.X509Certificates;

namespace OpenIdDictTemplate.Security.Host.Configuration;

internal static class CertificateLoader
{
    public static X509Certificate2 Load(CertificateFileOptions options, string name)
    {
        if (string.IsNullOrWhiteSpace(options.Path))
            throw new InvalidOperationException($"Security:Certificates:{name}:Path is required when development certificates are not used.");

        return X509CertificateLoader.LoadPkcs12FromFile(options.Path, options.Password);
    }
}
