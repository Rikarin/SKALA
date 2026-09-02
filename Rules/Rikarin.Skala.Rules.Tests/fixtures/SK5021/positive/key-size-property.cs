using System.Security.Cryptography;

// ⚠ The property spelling, which `CA5385` misses even on `RSACryptoServiceProvider`.
public static class Signing {
    public static RSA Signer() {
        var signer = RSA.Create();
        signer.KeySize = 1024;
        return signer;
    }
}
