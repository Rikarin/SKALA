using System.Security.Cryptography;

// The key is constant across messages by definition, so using it as the IV makes the IV constant too.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = cipher.Key;
        return cipher.CreateEncryptor();
    }
}
