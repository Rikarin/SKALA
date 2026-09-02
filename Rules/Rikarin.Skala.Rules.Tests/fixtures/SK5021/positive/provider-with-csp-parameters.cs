using System.Security.Cryptography;

// ⚠ The two-argument overload. This was silently declined until a surviving sabotage exposed the
// arity check that was rejecting it, and it is as much a 1024-bit key as the one-argument spelling.
public static class Signing {
    public static RSA Signer(CspParameters parameters) => new RSACryptoServiceProvider(1024, parameters);
}
