using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Pgreflex;

public static class X509Certificate2Extensions
{
  public static string SpkiBase64Hash(this X509Certificate2 cert)
  {
    byte[] spki = cert.PublicKey.ExportSubjectPublicKeyInfo();
    // Pin = base64(SHA256(SPKI))
    return Convert.ToBase64String(SHA256.HashData(spki));
  }
}
