using System.Security.Cryptography;

// The legacy spelling — the one the SDK does cover, kept so the rule is not silently narrower.
public static class Signing {
    public static RSA Signer() => new RSACryptoServiceProvider(1024);
}
