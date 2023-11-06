using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Netimobiledevice.Lockdown
{
    public static class CertificateGenerator
    {
        private static byte[] ConvertCertToPem(X509Certificate2 cert)
        {
            byte[] certificateBytes = cert.RawData;
            char[] certificatePem = PemEncoding.Write("CERTIFICATE", certificateBytes);
            return Encoding.UTF8.GetBytes(certificatePem);
        }

        private static byte[] ConvertPrivateKeyToPem(byte[] privKeyBytes)
        {
            char[] privKeyPem = PemEncoding.Write("PRIVATE KEY", privKeyBytes);
            return Encoding.UTF8.GetBytes(privKeyPem);
        }

        private static X509Certificate2 CreateRootCertificate(RSA key)
        {
            // Create a certificate signing request (CSR)
            CertificateRequest csr = new CertificateRequest(string.Empty, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            csr.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            csr.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(csr.PublicKey, false));

            // Generate a self-signed certificate using the CSR
            X509Certificate2 certificate = csr.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(10));

            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }

        private static X509Certificate2 CreateDeviceCertificate(X509Certificate2 signingCert, byte[] publicKey, string subjectName)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048)) {
                string pemKey = Encoding.UTF8.GetString(publicKey);
                rsa.ImportFromPem(pemKey);

                // Create a certificate signing request (CSR)
                CertificateRequest csr = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                X509Certificate2 certificate = csr.Create(signingCert, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(9), BitConverter.GetBytes(1));

                return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
            }
        }

        public static (byte[], byte[], byte[]) GeneratePairingCertificates(byte[] devicePublicKey)
        {
            // Create a new RSA key pair
            using (RSA rsa = RSA.Create(2048)) {
                X509Certificate2 rootCert = CreateRootCertificate(rsa);
                X509Certificate2 deviceCert = CreateDeviceCertificate(rootCert, devicePublicKey, "Device");

                byte[] rootCertPem = ConvertCertToPem(rootCert);
                byte[] deviceCertPem = ConvertCertToPem(deviceCert);

                byte[] privateKeyPem = ConvertPrivateKeyToPem(rsa.ExportPkcs8PrivateKey());

                return (rootCertPem, privateKeyPem, deviceCertPem);
            }
        }
    }
}
