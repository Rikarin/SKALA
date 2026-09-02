using System.Security.Cryptography;

// Above the floor.
public static class Signing {
    public static RSA Signer() => RSA.Create(4096);
}
