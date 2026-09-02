using System.Security.Cryptography;

// The two-argument overload takes the IV directly, and this one is zeros.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        return cipher.CreateEncryptor(key, new byte[16]);
    }
}
