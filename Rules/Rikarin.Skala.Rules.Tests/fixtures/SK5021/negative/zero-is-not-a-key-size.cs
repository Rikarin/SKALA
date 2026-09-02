using System.Security.Cryptography;

// ⚠ Zero is what "not configured yet" looks like, and it is not a weak key. Reporting it would tell
// the reader that their 0-bit key should be 2048 bits, which is not a sentence anybody can act on.
public static class Signing {
    public static RSA Unconfigured() {
        var signer = RSA.Create();
        signer.KeySize = 0;
        return signer;
    }
}
