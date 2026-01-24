using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Pgreflex.Server;

public static class CertificateFactory
{
  public static X509Certificate2 CreateSelfSignedServerCert(string subjectName, int validDays)
  {
    using var key = RSA.Create(2048);

    var req = new CertificateRequest(
        new X500DistinguishedName(subjectName),
        key,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1
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
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, // ServerAuth
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

    // ✅ .NET 8–approved way
    return X509CertificateLoader.LoadPkcs12(
        pfx,
        password: null,
        keyStorageFlags: X509KeyStorageFlags.PersistKeySet
    );

  }
}
