namespace Netimobiledevice.Lockdown.Pairing;

internal record PairingCertificates(
     byte[] RootCertificatePem,
     byte[] DeviceCertificatePem,
     byte[] PrivateKeyPem
);
