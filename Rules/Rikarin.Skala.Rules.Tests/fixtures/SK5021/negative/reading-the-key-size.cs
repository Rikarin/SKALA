using System.Security.Cryptography;

// A read, not a write.
public static class Signing {
    public static int Bits(RSA signer) => signer.KeySize;
}
