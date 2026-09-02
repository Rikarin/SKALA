using System.Security.Cryptography;

// Half again as weak, and reported for the same reason.
public static class Signing {
    public static RSA Signer() => RSA.Create(512);
}
