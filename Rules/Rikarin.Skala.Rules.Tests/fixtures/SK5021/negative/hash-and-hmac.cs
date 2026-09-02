using System.Security.Cryptography;

// Neither is an asymmetric algorithm, and `HashSize` is not `KeySize`.
public static class Digests {
    public static int Bits() {
        using var hash = SHA256.Create();
        using var mac = new HMACSHA256(new byte[32]);
        return hash.HashSize + mac.HashSize;
    }
}
