using System.Security.Cryptography;

// The same two-argument overload, at the floor.
public static class Signing {
    public static RSA Signer(CspParameters parameters) => new RSACryptoServiceProvider(2048, parameters);
}
