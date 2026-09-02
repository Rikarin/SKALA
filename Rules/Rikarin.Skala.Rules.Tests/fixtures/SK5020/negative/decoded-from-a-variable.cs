using System;
using System.Security.Cryptography;

// The same, through base64.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key, string stored) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Convert.FromBase64String(stored);
        return cipher.CreateEncryptor();
    }
}
