using System;
using System.Security.Cryptography;

// Base64 is an encoding, not a secret.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Convert.FromBase64String("AQIDBAUGBwgJCgsMDQ4PEA==");
        return cipher.CreateEncryptor();
    }
}
