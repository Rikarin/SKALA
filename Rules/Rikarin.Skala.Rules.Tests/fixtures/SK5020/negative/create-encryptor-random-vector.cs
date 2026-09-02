using System.Security.Cryptography;

// ⚠ `CA5401` reports this too. The vector is drawn fresh, which is the whole point.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        return cipher.CreateEncryptor(key, RandomNumberGenerator.GetBytes(16));
    }
}
