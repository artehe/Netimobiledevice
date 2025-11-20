using Netimobiledevice.Lockdown.Pairing;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NetimobiledeviceTest.Lockdown.Pairing;

public static class CertificateGeneratorTestData {
    public const string ValidCertPem = @"-----BEGIN CERTIFICATE-----
MIICujCCAaKgAwIBAgIBADANBgkqhkiG9w0BAQUFADAAMB4XDTI1MTEyMDExMDE1
M1oXDTM1MTExODExMDE1M1owADCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoC
ggEBAMTFypkQvP91D8mK0dyacKYkH5pfiHPvLz7CE93QI0Yle09sojvLKrXTvvT/
0zulkkRO5d3J/xGusUrA1ZuqPNEojaIhFvwxxilHfp799s0kSU+O7wC9wUfHxeKm
WRT95GqVsY+7bpi7le3EWKY7me4UkUJ7NcXPsADC29vnXw0jH0UGyxTkSTyTEz4c
Mdu0JovtlAplfRu7hcEPHr+VDMWydQA9bUrZTvzk7f+zrpcEq7Hg2AJ1ETPJmDE1
GnBLJr/UcZGsvKgBjoCpz3CLJWqKYByK3dqrT/7gBTiN2TZDT/jt4YqJRWhnVddV
mY3uqG5/x7mctJ1NnFgInnnBO2cCAwEAAaM/MD0wDAYDVR0TAQH/BAIwADAdBgNV
HQ4EFgQUwDbvESrmo+1r5z0ar5dJsml3+BUwDgYDVR0PAQH/BAQDAgWgMA0GCSqG
SIb3DQEBBQUAA4IBAQBhwGzOkVjy2I6SH16JrWT4UVXkih6cxV8rnrdLjnR7FXYQ
6auK5Xei8wWz3H8GnuhWaePT52w8Lgvd5Bp5kx4zDZOhkOHfkO7uszJlUX/v1SuL
hFiA6PQKWPWPEoR7fVl6frMiYbGQMcyD67iJxjg8nwpCh38P5irogLl8VTpD48E9
wI7+ypQOQPJwZacYzJy/NjpRYVXnh2NJK3yJ0Fb2oBfnCW+X0xLH/t6d/4rnzj5x
LLu9gzgUkCvorMX4e7XB4Dp1j7oBFLzRzCug3uVAp7jV8aJgYrF8xbumwhijBrE8
WRMxBIl5II9neY5ifYrT8fve4ZKP8luBXta/ltSp
-----END CERTIFICATE-----
";
    public const string ValidKeyPem = @"-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDExcqZELz/dQ/J
itHcmnCmJB+aX4hz7y8+whPd0CNGJXtPbKI7yyq10770/9M7pZJETuXdyf8RrrFK
wNWbqjzRKI2iIRb8McYpR36e/fbNJElPju8AvcFHx8XiplkU/eRqlbGPu26Yu5Xt
xFimO5nuFJFCezXFz7AAwtvb518NIx9FBssU5Ek8kxM+HDHbtCaL7ZQKZX0bu4XB
Dx6/lQzFsnUAPW1K2U785O3/s66XBKux4NgCdREzyZgxNRpwSya/1HGRrLyoAY6A
qc9wiyVqimAcit3aq0/+4AU4jdk2Q0/47eGKiUVoZ1XXVZmN7qhuf8e5nLSdTZxY
CJ55wTtnAgMBAAECggEAD6MILpG98y8CSinV84nyWcGVIVdmKJBhWMNjgMUBiJmB
6xXe7pF+m2RwUFfkGWFW9kauzak3s9gGDsK0NuKYYqauWqv2f6QU80LTNR9JdZPk
n4XkSFLMAUBG4XQGsDxc4nty2NE0QL7nczWfNdaWrOzFflr8MvQGMIlLn3YLf6ef
K6WX95ga4PFymgy7UL0s2vcrBfs5ljj6dbv3byZaDRfhDRuGV7bEJmqvb+VsLVZG
s9CrcNPTZdUM6R1TI4N28ive1FowWWJcxBEjqF/lBidvLlFueAN2bxyef8aIL6/y
MoDSpr8rfvxlzyexjeNcxBcXFXJ63LPeb0OA5IPnAQKBgQDn759s15unfelBkRlD
eTvaIzRkSeos/mGjcH/rIqlEhHq+NyWT94kS2AFKpYVMbulxRejRr78qGd1ws3IP
cts9v++oef7awTR9xouVIKsh3vl3mIZ/vR6vMM30M/sb6gHED9Z+aHPLuOJq4gpd
O1SnBB+FdEaLfrG80iNRpp4P8QKBgQDZMDR2XYakcilyu5gZX0zEVzkWuX1M5ccr
6gAzdbSrYPmsUO3+6tp3RMyK2Cw3jQx6z31euo/xoyRAv7duC6mYgFhZZquvZByX
Su8NaHmBenAByyniLGXGCUzG31x5WsypSIAhFvvN7gy+WOgan29nr2WbtPCwb0kj
ZldVFPdY1wKBgF2SRvdaZOnF2n0hVNfr6UGwQkrTpy5P0oRltrXeXfvOltZ22SpB
C4QWsS60aHrVpEiWs78k8DLEDJqTSskAxYK8FKwby73lhI/ZsiaP73rSwkKFvn16
hw6W2gBTmFNCrUO0QAzvhwgBpxcH6raCYTygZTcqGevdSUesX6NpXQuBAoGAeXDX
1NuE3syq2mmEqaM9BIgU9tzu8CGHVcm5JQ0K4c0OrkhuW1ysnYYNrREk4EbxFHaS
jnZY6G8lZUMN6O2CfjA9tnePRjn/NqWCt4eUcU6p9IbKO5pBqhMnKAha58xapclR
Q69bSxFxdLm3xrMhkuNjOEZbUvxW9AFUkdFwYO8CgYEAgvbJgFcRREsNignooGb3
o0n9+r0cPBIGvNGERFOLvFtzejivl4GhNl8l8HHZTYd0auleTkcWb+us8qQCpYn2
xRcev9trwCXQ9r9n0YG30++2MZlbNl3gVmnLM9BncnkRo8gBfZVq2cKOX9qYLmr8
BEPpTdWvgATL9U0ezswrJdk=
-----END PRIVATE KEY-----
";

    public const string OtherPrivateKeyPem = @"-----BEGIN PRIVATE KEY-----
MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQDgZJ7bPwKZ2Q2g
tVHbgQ6OZBvtHBj2lEj6TMLg/4yh/dNQVrg6T3K3hq3phCcKbtc8+bHQJw68O//h
EXm6fDUv95KoUlYk3vX9yDQCX27t9f1sgUVtzMXPlskM9OLWSwNciZlE/HA+s8rF
VzGGOZkuRl37e8FkYjmedduZo6XLGVJ7JGX1qQt9cJS24RDdkO0xH3JW11N7jOCp
v37BT4eIf09ShL9KrKwOUT87NohY5JqPusgXw6s2w7z88ZgkE12Mv7GqWzADgqq0
OJnJjdx/KNaGiqMr4RwK8FROjddPGIOblq1f2D0Exb7XBrQobYxR62zTWUrzuCt8
OjTceo0tAgMBAAECggEBAIAPnG6Z8AVjrOLPMSr39gAoO2JZBFxYQX/n5H9JQDg8
KTVgZjE0eFSxC9zzKpHC7sivhi83CI8gL4DmiIOfxT2tcGD3w8RCfGfVsQzOmlMo
rtVQGQSB9ev0falkXMy3s44oAM8XotFSX8kx6AGG0OeQ30Nd1SsSg1AFfOhdA8pZ
8CIi9QEaD/lfOsCjEOMaujs/Dx7GAVNzAyRbkP6hHlglhjradkmn0dK2NOaA2WKU
zwlC3ySn6SrwQH88UB9a2HdsYddmdoyzClW5u+6SOzY7+11d495x4FMAFB3j+qcV
vgwmzD16RwWfJ+c6ioOBP3Wp/S1XKMt3C5CfIeSFfF8CgYEA9J683/C8nPIY6vnp
E95mTQnfdA4GIbgDV25Tq+bGc6VlqxqXeIrORDEfc4FYDd8uPa/UaVTauZbu3Hpk
0zYGUl8gHqykFJ7qYOMuSfJ6YzZoDfjpBLOMK/FP2DFjdLmdvQapqSE2xOyoQshz
n4WoEXtAvcZgX2YgqvNCh0l6ymMCgYEA6ZipQ7ICjrCMUF296VolUxdKhJMZqSh7
G9fnLUe7Z3QXft8xodHG0Y72Q3MBxAQocW/YRDNFzoeCx6KKNYjmnUsoyUm5ivLe
iGIxtnjNHqPjtt4kXtyXiDYxnnHI8530LWnH75xxaauTS7JPxOn3tDxZY/pi8T3u
uM9+J60Epb8CgYAzBpD3yXeTveioH7tjqNW2ZP1Xo4I0VpV8Qv80jHBwvzAf3Gsk
FQkF4yxYG4m4tXd6e8f2HxL28o+n1T6qLIB6N60eWHdt1YpQv2TrU5Gbp7QxSGKO
I9D0pfVIExmmRocM5VO9Nnks5IWWJn6tZk1suWtOMKcJ+oAh8elH/dfZfwKBgCUf
b0tep/PAtvOu+8wQU0K606Pj9SMNTCsq1IV6ErWf9tSzvJ1G9k0OkFcGJ+usQMPC
Yd5t5tjnLHmnvdXU0HRMvi71jKFApBqY6edzgd1NRpSoLkv1NY5oI8pg9Cbq0B2B
b7TzylYkY3qIEPFeGlBQAWLO/XTeu/AyFdskXhsvAoGAVZ9zcZR0KjPXVuVlZ8iA
aKkboUXmXk3+4tMf8WgOA90sRrj0sqgbSLI+Df5xuPLw0SksDyr54+fG0j+g0/7Y
Atz3Tk8Ftx6iSJZnZiFkpDSMvOaUrytYq7o4Smvsz+NhEq0pTQUIqhvIR0f0tgKN
viTP5YOZc8d9UeYzkqRv7Ws=
-----END PRIVATE KEY-----
";
}

[TestClass]
public class CertificateGeneratorTests {
    [TestMethod]
    public void LoadCertificate_ValidPem_Succeeds() {
        // typical self-signed RSA cert & matching private key
        string certPem = CertificateGeneratorTestData.ValidCertPem;
        string keyPem = CertificateGeneratorTestData.ValidKeyPem;

        X509Certificate2 cert = CertificateGenerator.LoadCertificate(certPem, keyPem);

        Assert.IsTrue(cert.HasPrivateKey);
        Assert.IsNotNull(cert.GetRSAPrivateKey());
    }

    [TestMethod]
    public void LoadCertificate_MismatchedKey_Throws() {
        string certPem = CertificateGeneratorTestData.ValidCertPem;
        string wrongKeyPem = CertificateGeneratorTestData.OtherPrivateKeyPem;

        Assert.Throws<CryptographicException>(() => {
            CertificateGenerator.LoadCertificate(certPem, wrongKeyPem);
        });
    }

    [TestMethod]
    public void LoadCertificate_InvalidPem_Throws() {
        string certPem = "-----BEGIN CERTIFICATE-----\nINVALID\n-----END CERTIFICATE-----";
        string keyPem = CertificateGeneratorTestData.ValidKeyPem;

        Assert.Throws<CryptographicException>(() => {
            CertificateGenerator.LoadCertificate(certPem, keyPem);
        });
    }


    [TestMethod]
    public void LoadCertificate_ExportImportRoundTrip_PreservesKey() {
        string certPem = CertificateGeneratorTestData.ValidCertPem;
        string keyPem = CertificateGeneratorTestData.ValidKeyPem;

        X509Certificate2 cert = CertificateGenerator.LoadCertificate(certPem, keyPem);
        byte[] export = cert.Export(X509ContentType.Pkcs12);
        X509Certificate2 imported = new X509Certificate2(export, (string?) null, X509KeyStorageFlags.EphemeralKeySet);

        Assert.IsTrue(imported.HasPrivateKey);
    }
}
