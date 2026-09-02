using System.Security.Cryptography;

// Configuring the cipher without touching the vector.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.Mode = CipherMode.CBC;
        cipher.Padding = PaddingMode.PKCS7;
        cipher.GenerateIV();
        return cipher.CreateEncryptor();
    }
}
