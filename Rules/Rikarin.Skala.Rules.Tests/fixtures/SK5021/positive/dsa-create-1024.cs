using System.Security.Cryptography;

// ⚠ `CA5384` reports `DSACryptoServiceProvider` as an algorithm and misses this factory entirely.
public static class Signing {
    public static DSA Signer() => DSA.Create(1024);
}
