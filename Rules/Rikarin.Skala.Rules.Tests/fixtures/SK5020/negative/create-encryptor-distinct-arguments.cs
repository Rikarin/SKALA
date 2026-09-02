using System.Security.Cryptography;

// Two different symbols, so the "the key is the IV as well" test must not match.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key, byte[] vector) {
        using var cipher = Aes.Create();
        return cipher.CreateEncryptor(key, vector);
    }
}
