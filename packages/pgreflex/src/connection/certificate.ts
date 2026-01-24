import * as x509 from "@peculiar/x509";
import crypto from "node:crypto";

export type ClientCertificate = {
  privateKeyPem: string;
  certificatePem: string;
  spkiSha256Base64: string;
};

export async function generateClientCertificateP256(
  commonName: string,
  daysValid = 7,
): Promise<ClientCertificate> {
  // ECDSA P-256 keypair
  const keyPair = await crypto.webcrypto.subtle.generateKey(
    { name: "ECDSA", namedCurve: "P-256" },
    true,
    ["sign", "verify"],
  );

  const now = new Date();
  const notBefore = new Date(now.getTime() - 5 * 60 * 1000);
  const notAfter = new Date(now.getTime() + daysValid * 24 * 60 * 60 * 1000);

  // Self-signed X.509 cert suitable for TLS client auth
  const cert = await x509.X509CertificateGenerator.createSelfSigned({
    serialNumber: crypto.randomBytes(16).toString("hex"),
    name: `CN=${commonName}`,
    notBefore,
    notAfter,

    // ECDSA signing
    signingAlgorithm: { name: "ECDSA", hash: "SHA-256" },

    keys: {
      publicKey: keyPair.publicKey,
      privateKey: keyPair.privateKey,
    },

    extensions: [
      new x509.BasicConstraintsExtension(false, undefined, true),
      new x509.KeyUsagesExtension(x509.KeyUsageFlags.digitalSignature, true),
      new x509.ExtendedKeyUsageExtension(
        [x509.ExtendedKeyUsage.clientAuth],
        true,
      ),
    ],
  });

  const certificatePem = cert.toString("pem");
  const privateKeyPem = await exportPrivateKeyPem(keyPair.privateKey);

  // SPKI pin: base64(sha256(SPKI DER))
  const spkiDer = Buffer.from(
    await crypto.webcrypto.subtle.exportKey("spki", keyPair.publicKey),
  );
  const spkiSha256Base64 = crypto
    .createHash("sha256")
    .update(spkiDer)
    .digest("base64");

  return { privateKeyPem, certificatePem, spkiSha256Base64 };
}

async function exportPrivateKeyPem(
  key: crypto.webcrypto.CryptoKey,
): Promise<string> {
  const pkcs8 = await crypto.webcrypto.subtle.exportKey("pkcs8", key);
  const b64 = Buffer.from(pkcs8).toString("base64");
  const lines = b64.match(/.{1,64}/g)?.join("\n") ?? b64;
  return `-----BEGIN PRIVATE KEY-----\n${lines}\n-----END PRIVATE KEY-----`;
}
