using System.Security.Cryptography;

// The same assignment written inside an object initialiser.
public static class Signing {
    public static RSA Signer() => new RSACryptoServiceProvider { KeySize = 1024 };
}
