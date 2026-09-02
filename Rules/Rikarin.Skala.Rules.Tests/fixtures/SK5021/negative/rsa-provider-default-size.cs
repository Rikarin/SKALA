using System.Security.Cryptography;

// The same, for the legacy type.
public static class Signing {
    public static RSA Signer() => new RSACryptoServiceProvider();
}
