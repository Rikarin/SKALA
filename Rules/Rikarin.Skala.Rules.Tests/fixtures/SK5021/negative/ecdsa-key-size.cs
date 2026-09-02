using System.Security.Cryptography;

// ⚠ A 256-bit elliptic-curve key is *stronger* than a 2048-bit RSA one. A bit-count floor applied
// across algorithm families would make this rule report the replacement it recommends.
public static class Signing {
    public static ECDsa Signer() {
        var signer = ECDsa.Create();
        signer.KeySize = 256;
        return signer;
    }
}
