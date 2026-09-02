using System.Security.Cryptography;

// The size is the caller's decision and this call site cannot read it.
public static class Signing {
    public static RSA Signer(int bits) => RSA.Create(bits);
}
