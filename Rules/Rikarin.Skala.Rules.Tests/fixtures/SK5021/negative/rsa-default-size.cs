using System.Security.Cryptography;

// No size argument, so there is no constant to read and nothing to assert.
public static class Signing {
    public static RSA Signer() => RSA.Create();
}
