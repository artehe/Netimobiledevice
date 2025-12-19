namespace Netimobiledevice.Remoted.Tunnel;

internal record PairConsentResult(
    byte[] PublicKey,
    string Salt,
    string Pin
);
