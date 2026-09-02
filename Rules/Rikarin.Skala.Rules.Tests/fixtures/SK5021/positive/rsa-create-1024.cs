using System.Security.Cryptography;

// ⚠ The modern factory, and the spelling `CA5385` does not cover.
public static class Signing {
    public static RSA Signer() => RSA.Create(1024);
}
