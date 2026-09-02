using System.Security.Cryptography;

// The floor exactly.
public static class Signing {
    public static RSA Signer() => RSA.Create(2048);
}
