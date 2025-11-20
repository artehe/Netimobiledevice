using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Netimobiledevice.Lockdown.Pairing;

public static class CertificateGenerator {
    private static X509Certificate2 CreateRootCertificate(RSA key) {
        // Create a certificate signing request (CSR)
        CertificateRequest csr = new CertificateRequest(string.Empty, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        csr.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        csr.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(csr.PublicKey, false));

        // Generate a self-signed certificate using the CSR
        X509Certificate2 certificate = csr.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(10));
        return certificate;
    }

    private static X509Certificate2 CreateDeviceCertificate(X509Certificate2 signingCert, byte[] publicKey) {
        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048)) {
            string pemKey = Encoding.UTF8.GetString(publicKey);
            rsa.ImportFromPem(pemKey);

            // Create a certificate signing request (CSR)
            CertificateRequest csr = new CertificateRequest(string.Empty, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            X509Certificate2 certificate = csr.Create(signingCert, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(9), BitConverter.GetBytes(1));
            return certificate;
        }
    }

    internal static PairingCertificates GeneratePairingCertificates(byte[] devicePublicKey) {
        // Create a new RSA key pair
        using (RSA rsa = RSA.Create(2048)) {
            X509Certificate2 rootCert = CreateRootCertificate(rsa);
            X509Certificate2 deviceCert = CreateDeviceCertificate(rootCert, devicePublicKey);

            return new PairingCertificates(
                rootCert.ExportCertificatePem(),
                deviceCert.ExportCertificatePem(),
                rsa.ExportPkcs8PrivateKeyPem()
            );
        }
    }

    internal static X509Certificate2 LoadCertificate(string certPem, string privateKeyPem) {
        string tmpPath = Path.GetTempFileName();
        File.WriteAllText(tmpPath, $"{certPem}\n{privateKeyPem}");
        X509Certificate2 certificate = X509Certificate2.CreateFromPemFile(tmpPath);

        // NOTE: For some reason we need to re-export and then import the cert again ¯\_(ツ)_/¯
        // see this for more details: https://github.com/dotnet/runtime/issues/45680
        return new X509Certificate2(certificate.Export(X509ContentType.Pkcs12));
    }
}
