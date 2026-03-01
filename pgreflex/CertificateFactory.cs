using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Pgreflex;

public static class CertificateFactory
{
    public static X509Certificate2 CreateSelfSignedServerCert(string subjectName, int validDays)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var req = new CertificateRequest(
            new X500DistinguishedName(subjectName),
            key,
            HashAlgorithmName.SHA256
        );

        req.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true)
        );

        req.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true
            )
        );

        req.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new("1.3.6.1.5.5.7.3.1")], // ServerAuth
                true
            )
        );

        req.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(req.PublicKey, false)
        );

        using var created = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(validDays)
        );

        var pfx = created.Export(X509ContentType.Pfx);


        var cert = X509CertificateLoader.LoadPkcs12(
            pfx,
            password: null,
            keyStorageFlags: X509KeyStorageFlags.PersistKeySet
        );

        return cert;
    }

}
