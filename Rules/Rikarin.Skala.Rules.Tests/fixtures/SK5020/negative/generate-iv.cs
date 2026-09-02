using System.Security.Cryptography;

// The platform draws a fresh vector from its cryptographic generator. This is the answer.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.GenerateIV();
        return cipher.CreateEncryptor();
    }
}
