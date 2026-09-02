using System.Security.Cryptography;

// The size is still fixed at compile time, so the finding stands; the fix replaces the whole
// argument rather than editing a declaration it has not looked at.
public static class Signing {
    const int Bits = 1024;

    public static RSA Signer() => RSA.Create(Bits);
}
