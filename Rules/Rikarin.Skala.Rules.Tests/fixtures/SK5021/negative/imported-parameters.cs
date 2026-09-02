using System.Security.Cryptography;

// A different single-argument overload whose argument is not a size at all.
public static class Signing {
    public static RSA Signer(RSAParameters parameters) => RSA.Create(parameters);
}
