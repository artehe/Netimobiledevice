namespace Netimobiledevice.Lockdown.Pairing;

internal record PairingCertificates(
     string RootCertificatePem,
     string DeviceCertificatePem,
     string PrivateKeyPem
);
